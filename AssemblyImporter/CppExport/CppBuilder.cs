using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using System.IO;
using Clarity.Pdb;
using Clarity.Rpa;

namespace AssemblyImporter.CppExport
{
    public class CppBuilder
    {
        public CLRAssemblyCollection Assemblies { get { return m_assemblies; } }
        public IDictionary<CLRAssembly, PdbDebugInfo> AssemblyPdbs { get { return m_pdbs; } }

        private CLRAssemblyCollection m_assemblies;
        private string m_exportPath;
        private string m_stubDir;
        private Dictionary<CLRTypeSpec, CppClass> m_typeSpecClasses;
        private Dictionary<string, CppClass> m_fullNameClasses;
        private Dictionary<CLRAssembly, PdbDebugInfo> m_pdbs;

        public CppBuilder(string exportDir, string stubDir, CLRAssemblyCollection assemblies, IDictionary<CLRAssembly, string> pdbPaths)
        {
            m_assemblies = assemblies;
            m_exportPath = exportDir;
            m_stubDir = stubDir;
            m_typeSpecClasses = new Dictionary<CLRTypeSpec, CppClass>();
            m_fullNameClasses = new Dictionary<string, CppClass>();
            m_pdbs = new Dictionary<CLRAssembly, PdbDebugInfo>();

            foreach (KeyValuePair<CLRAssembly, string> pdbPath in pdbPaths)
            {
                using (System.IO.FileStream fs = new FileStream(pdbPath.Value, FileMode.Open, FileAccess.Read))
                {
                    PdbDebugInfo pdbDebugInfo = new PdbDebugInfo(fs);
                    m_pdbs.Add(pdbPath.Key, pdbDebugInfo);
                }
            }

            // Generate type def cache
            foreach (CLRAssembly assm in assemblies)
            {
                CLRMetaDataTables tables = assm.MetaData.MetaDataTables;
                ICLRTable typeDefs = tables.GetTable(CLRMetaDataTables.TableIndex.TypeDef);

                for (uint i = 0; i < typeDefs.NumRows; i++)
                {
                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefs.GetRow(i);
                    CacheTypeDef(typeDef);
                }
            }

            // Export everything
            foreach (CLRAssembly assm in assemblies)
            {
                CLRMetaDataTables tables = assm.MetaData.MetaDataTables;
                ICLRTable typeDefs = tables.GetTable(CLRMetaDataTables.TableIndex.TypeDef);

                ICLRTable assemblyTable = tables.GetTable(CLRMetaDataTables.TableIndex.Assembly);
                CLRAssemblyRow assemblyRow = (CLRAssemblyRow)assemblyTable.GetRow(0);

                Clarity.Rpa.HighFileBuilder fileBuilder = new Clarity.Rpa.HighFileBuilder();

                using (FileStream fs = new FileStream(exportDir + assemblyRow.Name + ".rpa", FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(fs))
                    {
                        using (MemoryStream objDataStream = new MemoryStream())
                        {
                            using (BinaryWriter objDataWriter = new BinaryWriter(objDataStream))
                            {
                                uint numUsableTypes = 0;

                                for (uint i = 0; i < typeDefs.NumRows; i++)
                                {
                                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefs.GetRow(i);
                                    if (typeDef.Extends == null)
                                    {
                                        if (typeDef.TypeNamespace == "" && typeDef.TypeName == "<Module>")
                                            continue;

                                        if (typeDef.Semantics != CLRTypeDefRow.TypeSemantics.Interface && (typeDef.TypeNamespace != "System" || typeDef.TypeName != "Object"))
                                            throw new Exception("Unexpected parentless type");
                                    }
                                    numUsableTypes++;
                                }

                                objDataWriter.Write(numUsableTypes);

                                for (uint i = 0; i < typeDefs.NumRows; i++)
                                {
                                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefs.GetRow(i);

                                    if (typeDef.Extends == null && typeDef.TypeNamespace == "" && typeDef.TypeName == "<Module>")
                                        continue;

                                    ExportTypeDef(fileBuilder, objDataWriter, typeDef);
                                }

                                uint assemblyNameIndex = fileBuilder.IndexString(assemblyRow.Name);

                                writer.Write(0x41503252);   // R2PA
                                fileBuilder.FlushAndWriteCatalogs(writer);
                                writer.Write(assemblyNameIndex);
                                writer.Flush();

                                objDataWriter.Flush();
                                objDataStream.WriteTo(fs);
                            }
                        }
                    }
                }
            }
        }

        private bool FieldNeedsDependencyDefs(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecComplexArray ||
                typeSpec is CLRTypeSpecSZArray)
                return false;

            if (typeSpec is CLRTypeSpecVarOrMVar)
                return false;

            if (typeSpec is CLRTypeSpecClass || typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CppClass cls = GetCachedClass(typeSpec);
                return cls.IsValueType;
            }

            throw new ArgumentException();
        }

        public CppTraceabilityEnum GetCachedTraceability(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecComplexArray ||
                typeSpec is CLRTypeSpecSZArray)
                return CppTraceabilityEnum.DefinitelyTraced;

            if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                // TODO: Maybe promote this to DefinitelyTraced by checking constraints
                return CppTraceabilityEnum.MaybeTraced;
            }

            if (typeSpec is CLRTypeSpecClass || typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CppClass cls = GetCachedClass(typeSpec);
                if (!cls.IsValueType)
                    return CppTraceabilityEnum.DefinitelyTraced;

                bool maybeTraceable = false;
                foreach (CppField field in cls.Fields)
                {
                    if (field.Field.Static)
                        continue;

                    CppTraceabilityEnum fieldTraceability = GetCachedTraceability(field.Type);
                    if (fieldTraceability == CppTraceabilityEnum.DefinitelyTraced)
                        return CppTraceabilityEnum.DefinitelyTraced;
                    if (fieldTraceability == CppTraceabilityEnum.MaybeTraced)
                        maybeTraceable = true;
                }

                if (maybeTraceable)
                    return CppTraceabilityEnum.MaybeTraced;
                return CppTraceabilityEnum.NotTraced;
            }

            throw new ArgumentException();
        }

        public CppClass GetCachedClass(CLRTypeSpec typeSpec)
        {
            CppClass cls;
            if (m_typeSpecClasses.TryGetValue(typeSpec, out cls))
                return cls;

            cls = CreateClassFromType(typeSpec);
            m_typeSpecClasses.Add(typeSpec, cls);
            return cls;
        }

        public CppClass CreateClassFromType(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecClass)
            {
                // TODO: Check typedef premades
                CLRTypeSpecClass tsClass = (CLRTypeSpecClass)typeSpec;
                CLRTypeDefRow typeDef = tsClass.TypeDef;

                CppClass cls = new CppClass(tsClass);
                foreach (CLRFieldRow fieldRow in typeDef.Fields)
                    cls.AddField(m_assemblies, fieldRow);
                foreach (CLRMethodDefRow methodRow in typeDef.MethodDefs)
                    cls.AddMethod(m_assemblies, methodRow);

                CppClass parentClass = null;
                CLRTypeSpec parentTypeSpec = null;
                if (typeDef.Extends != null)
                {
                    parentTypeSpec = m_assemblies.InternTypeDefOrRefOrSpec(typeDef.Extends);
                    parentClass = GetCachedClass(parentTypeSpec);
                }

                List<CLRTypeSpec> interfaces = new List<CLRTypeSpec>();

                foreach (CLRTableRow ii in typeDef.ImplementedInterfaces)
                {
                    CLRTypeSpec ifc = m_assemblies.InternTypeDefOrRefOrSpec(ii);
                    cls.AddExplicitInterface(this, ifc);
                    interfaces.Add(ifc);
                }
                cls.ResolveInherit(this, parentClass, interfaces, parentTypeSpec);

                return cls;
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)typeSpec;
                CppClass baseClass = GetCachedClass(gi.GenericType);
                return baseClass.Instantiate(gi.ArgTypes, null);
            }
            else
                throw new NotImplementedException();
        }

        public void CacheTypeDef(CLRTypeDefRow typeDef)
        {
            CppClass cls = GetCachedClass(new CLRTypeSpecClass(typeDef));
            m_fullNameClasses[cls.FullName] = cls;
        }

        private void CreateDirectoriesForFile(string path)
        {
            string dirPath = path.Substring(0, path.LastIndexOf('\\'));
            Directory.CreateDirectory(dirPath);
        }

        public IEnumerable<CLRSigCustomAttribute> CustomAttribsOfType(ICLRHasCustomAttributes hasCA, string ns, string name)
        {
            List<CLRSigCustomAttribute> attribs = new List<CLRSigCustomAttribute>();

            foreach (CLRCustomAttributeRow caRow in hasCA.CustomAttributes)
            {
                CLRSigCustomAttribute ca = caRow.CustomAttribute;
                CppMethodSpec ctorSpec = ResolveMethodDefOrRef(ca.Constructor);

                CLRTypeDefRow declClass = ctorSpec.CppMethod.DeclaredInClass;
                if (declClass.ContainerClass == null && declClass.TypeNamespace == ns && declClass.TypeName == name)
                    attribs.Add(ca);
            }

            return attribs;
        }

        private void ExportClassStubs(CppClass cls)
        {
            //CppStubExporter.ExportStub(this, m_stubDir, cls);
        }

        public static string SimpleTypeFullName(CLRSigType.ElementType basicType)
        {
            switch (basicType)
            {
                case CLRSigType.ElementType.BOOLEAN:
                    return "System.Boolean";
                case CLRSigType.ElementType.CHAR:
                    return "System.Char";
                case CLRSigType.ElementType.R4:
                    return "System.Single";
                case CLRSigType.ElementType.R8:
                    return "System.Double";
                case CLRSigType.ElementType.I1:
                    return "System.SByte";
                case CLRSigType.ElementType.U1:
                    return "System.Byte";
                case CLRSigType.ElementType.I2:
                    return "System.Int16";
                case CLRSigType.ElementType.U2:
                    return "System.UInt16";
                case CLRSigType.ElementType.I4:
                    return "System.Int32";
                case CLRSigType.ElementType.U4:
                    return "System.UInt32";
                case CLRSigType.ElementType.I8:
                    return "System.Int64";
                case CLRSigType.ElementType.U8:
                    return "System.UInt16";
                case CLRSigType.ElementType.STRING:
                    return "System.String";
                case CLRSigType.ElementType.OBJECT:
                    return "System.Object";
                case CLRSigType.ElementType.I:
                    return "System.IntPtr";
                case CLRSigType.ElementType.U:
                    return "System.UIntPtr";
            }
            throw new ArgumentException();
        }

        public enum MethodParameterMapping
        {
            ClassImpl,
            DelegateDef,
        }

        private struct BoundInterfaceMethodImpl
        {
            public CLRTypeSpec DefinedInType { get; private set; }
            public CppVtableSlot InterfaceSlot { get; private set; }
            public CppVtableSlot ClassSlot { get; private set; }

            public BoundInterfaceMethodImpl(CLRTypeSpec definedInType, CppVtableSlot interfaceSlot, CppVtableSlot classSlot)
                : this()
            {
                DefinedInType = definedInType;
                InterfaceSlot = interfaceSlot;
                ClassSlot = classSlot;
            }
        }

        public CLRTypeSpec ResolveTypeDefOrRefOrSpec(CLRTableRow tableRow)
        {
            return m_assemblies.InternTypeDefOrRefOrSpec(tableRow);
        }

        public CppMethodSpec ResolveMethodDefOrRef(CLRTableRow tableRow)
        {
            if (tableRow is CLRMethodDefRow)
            {
                CLRMethodDefRow methodDef = (CLRMethodDefRow)tableRow;

                return new CppMethodSpec(new CppMethod(this.Assemblies, methodDef.Owner, methodDef));
            }
            if (tableRow is CLRMemberRefRow)
            {
                CLRMemberRefRow memberRef = (CLRMemberRefRow)tableRow;
                CLRTypeSpec declaredIn = this.ResolveTypeDefOrRefOrSpec(memberRef.Class);

                if (declaredIn is CLRTypeSpecComplexArray)
                    throw new NotImplementedException();

                CppClass cachedClass = this.GetCachedClass(declaredIn);

                CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(this.Assemblies, memberRef.MethodSig);

                foreach (CppMethod method in cachedClass.Methods)
                {
                    if (method.Name == memberRef.Name && method.DeclaredMethodSignature.Equals(sig))
                        return new CppMethodSpec(method);
                }
                throw new ParseFailedException("Unresolved method reference");
            }
            if (tableRow is CLRMethodSpecRow)
            {
                CLRMethodSpecRow methodSpec = (CLRMethodSpecRow)tableRow;
                CppMethod method = ResolveMethodDefOrRef(methodSpec.Method).CppMethod;
                List<CLRTypeSpec> types = new List<CLRTypeSpec>();
                foreach (CLRSigType type in methodSpec.Instantiation.Types)
                    types.Add(this.Assemblies.InternVagueType(type));
                return new CppMethodSpec(method, types.ToArray());
            }
            throw new ArgumentException();
        }

        public CppVtableSlot ResolveMethodImplReference(CLRTableRow methodDecl)
        {
            if (methodDecl is CLRMethodDefRow)
            {
                CLRMethodDefRow mdef = (CLRMethodDefRow)methodDecl;

                CppClass cls = GetCachedClass(CreateInstanceTypeSpec(m_assemblies, mdef.Owner));
                CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_assemblies, mdef.Signature);

                foreach (CppMethod method in cls.Methods)
                {
                    if (method.MethodDef == mdef)
                    {
                        if (method.CreatesSlot != null)
                            return method.CreatesSlot;
                        if (method.ReplacesStandardSlot != null)
                            return method.ReplacesStandardSlot;
                        throw new ParseFailedException("Override was linked to a non-virtual method");
                    }
                }

                throw new ParseFailedException("Failed to match a MethodImpl");
            }
            else if (methodDecl is CLRMemberRefRow)
            {
                CLRMemberRefRow mref = (CLRMemberRefRow)methodDecl;
                if (mref.MethodSig == null)
                    throw new ParseFailedException("Strange method override encountered");

                CppClass declaredInClass = this.GetCachedClass(m_assemblies.InternTypeDefOrRefOrSpec(mref.Class));
                CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_assemblies, mref.MethodSig);

                foreach (CppVtableSlot slot in declaredInClass.OverrideVisibleVtableSlots)
                {
                    if (slot.Name == mref.Name && sig.Equals(slot.DeclaredSignature))
                        return slot;
                }
                throw new ParseFailedException("Couldn't match method reference");
            }
            else
                throw new NotSupportedException();
        }

        private void WriteInterfaceBinding(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppVtableSlot decl, CppVtableSlot body)
        {
            writer.Write(fileBuilder.IndexMethodDeclTag(decl.VtableSlotTag));
            writer.Write(fileBuilder.IndexMethodDeclTag(body.VtableSlotTag));
        }

        private void WriteInterfaceImplementations(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls)
        {
            if (cls.TypeDef.Semantics != CLRTypeDefRow.TypeSemantics.Class)
                throw new ArgumentException();

            List<CppVtableSlot> requiredVTableSlots = new List<CppVtableSlot>();

            uint numNewInterfaces = (uint)cls.NewlyImplementedInterfaces.Count;
            uint numReimplementedInterfaces = (uint)cls.ReimplementedInterfaces.Count;

            List<KeyValuePair<CLRTypeSpec, bool>> reqBindings = new List<KeyValuePair<CLRTypeSpec, bool>>();

            foreach (CLRTypeSpec ts in cls.NewlyImplementedInterfaces)
                reqBindings.Add(new KeyValuePair<CLRTypeSpec, bool>(ts, false));
            foreach (CLRTypeSpec ts in cls.ReimplementedInterfaces)
                reqBindings.Add(new KeyValuePair<CLRTypeSpec, bool>(ts, true));

            writer.Write((uint)reqBindings.Count);

            List<BoundInterfaceMethodImpl> boundImpls = new List<BoundInterfaceMethodImpl>();

            foreach (CLRMethodImplRow methodImpl in cls.TypeDef.MethodImplementations)
            {
                CppVtableSlot decl = ResolveMethodImplReference(methodImpl.MethodDeclaration);
                CppVtableSlot body = ResolveMethodImplReference(methodImpl.MethodBody);
                CLRTypeSpec spec = m_assemblies.InternTypeDefOrRefOrSpec(methodImpl.Class);

                if (decl.Equals(body))
                {
                    // Roslyn sometimes emits redundant MethodImpls that override the vtable slot
                    // that the method already occupies via ReuseSlot.  We want to ignore these.
                    continue;
                }

                boundImpls.Add(new BoundInterfaceMethodImpl(spec, decl, body));
            }

            foreach (KeyValuePair<CLRTypeSpec, bool> convPair in reqBindings)
            {
                CLRTypeSpec conv = convPair.Key;
                bool isReimpl = convPair.Value;

                writer.Write(fileBuilder.IndexTypeSpecTag(RpaTagFactory.CreateTypeTag(conv)));

                CppClass ifcClass = this.GetCachedClass(conv);

                writer.Write((uint)ifcClass.Methods.Count);

                foreach (CppMethod method in ifcClass.Methods)
                {
                    CppVtableSlot slot = method.CreatesSlot;
                    if (slot == null)
                        throw new ArgumentException();

                    bool isExplicitlyBound = false;
                    for (int i = 0; i < boundImpls.Count; i++)
                    {
                        BoundInterfaceMethodImpl bimi = boundImpls[i];
                        if (bimi.InterfaceSlot.DisambigSpec.Equals(conv) &&
                            bimi.InterfaceSlot.Name == slot.Name && bimi.InterfaceSlot.Signature.Equals(slot.Signature))
                        {
                            isExplicitlyBound = true;

                            writer.Write(true);    // HACK - FIXME
                            WriteInterfaceBinding(fileBuilder, writer, bimi.InterfaceSlot, bimi.ClassSlot);
                            boundImpls.RemoveAt(i);
                            break;
                        }
                    }

                    if (!isExplicitlyBound)
                    {
                        // Look for a matching slot
                        // This should follow the rules of II.12.2
                        // Because Clarity does not support virtual generic methods, maintaining a
                        // per-slot implementation list is not necessary.  However, duplicate slots
                        // from generic type substitution are still allowed.
                        //
                        // We depend on visible vtable slots being in method declaration order already,
                        // so the only thing we really need to do is return the first one.
                        //
                        // .NET has some additional non-standardized behavior: If a reimplemented interface
                        // doesn't have a new match since the last time it was implemented, then the implementation
                        // has NO MATCHES.  This matters because since interface dispatch is done per-class,
                        // per-method, a variant interface that does have a match will take priority if it's
                        // higher in the class hierarchy.
                        //
                        // See TestInheritedImplementationDeprioritization for an example of this.
                        bool haveMatch = WriteSignatureMatchedBinding(fileBuilder, writer, cls, slot, conv);

                        // If there's no match, but this is a reimplementation, then use the old implementation
                        // Allows TestInheritedReimpl to pass.
                        if (!haveMatch)
                        {
                            if (!isReimpl)
                                throw new ParseFailedException("Unmatched interface method");

                            writer.Write(false);    // HACK - FIXME
                        }
                    }
                }
            }

            if (boundImpls.Count > 0)
                throw new NotSupportedException("Don't support non-interface override thunks yet");
        }

        private bool WriteSignatureMatchedBinding(HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls, CppVtableSlot ifcSlot, CLRTypeSpec ifcTypeSpec)
        {
            bool haveMatch = false;
            foreach (CppVtableSlot vtSlot in cls.NewImplementationVisibleVtableSlots)
            {
                if (ifcSlot.Name == vtSlot.Name && ifcSlot.Signature.Equals(vtSlot.Signature))
                {
                    if (haveMatch)
                    {
                        Console.WriteLine("WARNING: Class " + cls.FullName + " has multiple matches for the same interface implementation");
                        break;
                    }
                    haveMatch = true;

                    writer.Write(true);    // HACK - FIXME
                    WriteInterfaceBinding(fileBuilder, writer, ifcSlot, vtSlot);
                }
            }

            if (haveMatch == true)
                return true;

            CLRTypeSpec parentSpec = cls.ParentTypeSpec;
            if (parentSpec == null)
                return false;

            CppClass parentClass = GetCachedClass(parentSpec);

            // Look for prior implementations of this interface, if any are found, STOP and return no-match.
            // See TestInheritedImplementationDeprioritization.  Matches are only recorded if they're new.
            foreach (CLRTypeSpec ifc in parentClass.NewlyImplementedInterfaces)
                if (ifc.Equals(ifcTypeSpec))
                    return false;
            foreach (CLRTypeSpec ifc in parentClass.ReimplementedInterfaces)
                if (ifc.Equals(ifcTypeSpec))
                    return false;
            return WriteSignatureMatchedBinding(fileBuilder, writer, parentClass, ifcSlot, ifcTypeSpec);
        }

        private void WriteVtableThunk(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls, CppMethod method, CppVtableSlot slot)
        {
            CLRMethodSignatureInstance slotSig = slot.Signature;

            writer.Write(fileBuilder.IndexMethodDeclTag(slot.VtableSlotTag));
            writer.Write(fileBuilder.IndexMethodSignatureTag(RpaTagFactory.CreateMethodSignature(slot.Signature)));

            if (cls.TypeDef.Semantics != CLRTypeDefRow.TypeSemantics.Interface)
            {
                writer.Write(method.Abstract);

                if (!method.Abstract)
                {
                    if (method.NumGenericParameters == 0)
                        writer.Write(method.Final);

                    if (method.Static)
                        throw new Exception("VTable slot implemented by static method");

                    writer.Write(fileBuilder.IndexMethodDeclTag(method.VtableSlotTag));
                }
            }
        }

        private void WriteVtableThunks(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls)
        {
            uint numNewSlots = 0;
            uint numReplacedSlots = 0;

            foreach (CppMethod method in cls.Methods)
            {
                if (method.CreatesSlot != null)
                    numNewSlots++;
                else if (method.ReplacesStandardSlot != null)
                    numReplacedSlots++;
            }

            if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
            {
                if (numReplacedSlots != 0)
                    throw new Exception();
            }
            else
            {
                writer.Write(numReplacedSlots);

                foreach (CppMethod method in cls.Methods)
                {
                    if (method.CreatesSlot == null && method.ReplacesStandardSlot != null)
                        WriteVtableThunk(fileBuilder, writer, cls, method, method.ReplacesStandardSlot);
                }
            }

            writer.Write(numNewSlots);

            foreach (CppMethod method in cls.Methods)
            {
                if (method.CreatesSlot != null)
                    WriteVtableThunk(fileBuilder, writer, cls, method, method.CreatesSlot);
            }
        }

        public static void WriteTemplateDualParamCluster(bool conditionalBrackets, int numTypeParameters, int numMethodParameters, string typePrefix, string methodPrefix, StreamWriter writer)
        {
            if (conditionalBrackets && numTypeParameters + numMethodParameters > 0)
                writer.Write("< ");
            WriteTemplateParamCluster(false, numTypeParameters, typePrefix, writer);
            if (numMethodParameters > 0 && numTypeParameters > 0)
                writer.Write(", ");
            WriteTemplateParamCluster(false, numMethodParameters, methodPrefix, writer);
            if (conditionalBrackets && numTypeParameters + numMethodParameters > 0)
                writer.Write(" >");
        }

        public static void WriteTemplateParamCluster(bool conditionalBrackets, int numParameters, string prefix, StreamWriter writer)
        {
            if (conditionalBrackets && numParameters > 0)
                writer.Write("< ");
            for (int i = 0; i < numParameters; i++)
            {
                if (i != 0)
                    writer.Write(", ");
                writer.Write(prefix);
                writer.Write(i.ToString());
            }
            if (conditionalBrackets && numParameters > 0)
                writer.Write(" >");
        }

        public static bool TypeSpecIsVoid(CLRTypeSpec ts)
        {
            return (ts is CLRTypeSpecVoid);
        }

        public static CLRTypeSpec CreateInstanceTypeSpec(CLRAssemblyCollection assemblies, CLRTypeDefRow typeDefRow)
        {
            int numClassGenericParams = 0;
            if (typeDefRow.GenericParameters != null)
                numClassGenericParams = typeDefRow.GenericParameters.Length;

            if (numClassGenericParams == 0)
                return new CLRTypeSpecClass(typeDefRow);
            else
            {
                List<CLRTypeSpec> genericParams = new List<CLRTypeSpec>();
                for (int i = 0; i < numClassGenericParams; i++)
                    genericParams.Add(new CLRTypeSpecVarOrMVar(CLRSigType.ElementType.VAR, (uint)i));

                // Sigh...
                // This would be easier if we just resolved from the class down, but we currently allow
                // free-floating methods to be resolved.
                CLRSigTypeGenericInstantiation.InstType instType = CLRSigTypeGenericInstantiation.InstType.Class;
                {
                    CLRTableRow extendsRow = typeDefRow.Extends;
                    if (extendsRow != null)
                    {
                        CLRTypeSpec parentTypeSpec = assemblies.InternTypeDefOrRefOrSpec(typeDefRow.Extends);
                        if (parentTypeSpec is CLRTypeSpecClass)
                        {
                            CLRTypeDefRow parentClassDef = ((CLRTypeSpecClass)parentTypeSpec).TypeDef;
                            if (parentClassDef.ContainerClass == null && parentClassDef.TypeNamespace == "System" && (parentClassDef.TypeName == "ValueType" || parentClassDef.TypeName == "Enum"))
                                instType = CLRSigTypeGenericInstantiation.InstType.ValueType;
                        }
                    }
                }

                return new CLRTypeSpecGenericInstantiation(instType, new CLRTypeSpecClass(typeDefRow), genericParams.ToArray());
            }
        }


        private void ExportMethodCode(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls, CppMethod method)
        {
            if (method.Abstract)
                throw new ArgumentException("Can't export code of an abstract method");

            if (method.MethodDef.Method == null)
                writer.Write(true);
            else
            {
                writer.Write(false);

                if (!method.Abstract && method.MethodDef.Method != null)
                    CppCilExporter.WriteMethodCode(fileBuilder, writer, this, cls, method);
            }
        }

        private bool ClassHasNewTraceableFields(CppClass cls)
        {
            if (cls.IsValueType == false && cls.ParentTypeSpec == null)
                return true;    // Special case for System.Object so it overrides GCObject

            foreach (CppField field in cls.Fields)
            {
                if (field.Field.Static)
                    continue;
                if (GetCachedTraceability(field.Type) != CppTraceabilityEnum.NotTraced)
                    return true;
            }
            return false;
        }

        private void ExportClassStatics(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls)
        {
            if (!cls.HaveNewStaticFields)
            {
                writer.Write((uint)0);
                return;
            }

            uint numStaticFields = 0;
            foreach (CppField field in cls.Fields)
                if (field.Field.Static && !field.Field.Literal)
                    numStaticFields++;

            writer.Write(numStaticFields);
            foreach (CppField field in cls.Fields)
            {
                if (field.Field.Static && !field.Field.Literal)
                {
                    writer.Write(fileBuilder.IndexTypeSpecTag(RpaTagFactory.CreateTypeTag(field.Type)));
                    writer.Write(fileBuilder.IndexString(field.Name));
                }
            }
        }

        private void ExportEnum(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls)
        {
            CLRTypeSpecClass underlyingType = (CLRTypeSpecClass)cls.GetEnumUnderlyingType();

            bool foundUnderlyingType = false;
            uint numLiterals = 0;
            foreach (CppField fld in cls.Fields)
            {
                CLRFieldRow fieldRow = fld.Field;
                if (fieldRow.Literal)
                    numLiterals++;
            }

            Clarity.Rpa.HighTypeDef.EnumUnderlyingType underlyingTypeSymbol;
            string typeName = underlyingType.TypeDef.TypeName;
            switch (typeName)
            {
                case "Byte":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.UInt8;
                    break;
                case "SByte":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.Int8;
                    break;
                case "Int16":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.Int16;
                    break;
                case "UInt16":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.UInt16;
                    break;
                case "Int32":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.Int32;
                    break;
                case "UInt32":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.UInt32;
                    break;
                case "Int64":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.Int64;
                    break;
                case "UInt64":
                    underlyingTypeSymbol = Clarity.Rpa.HighTypeDef.EnumUnderlyingType.UInt64;
                    break;
                default:
                    throw new ArgumentException();
            }

            writer.Write((byte)underlyingTypeSymbol);
            writer.Write(numLiterals);
            foreach (CppField fld in cls.Fields)
            {
                CLRFieldRow fieldRow = fld.Field;
                if (fieldRow.Literal)
                {
                    writer.Write(fileBuilder.IndexString(fld.Name));
                    ArraySegment<byte> constantValue = fieldRow.AttachedConstants[0].Value;
                    writer.Write(constantValue.Array, constantValue.Offset, constantValue.Count);
                }
            }
        }

        private void ExportDelegate(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls)
        {
            writer.Write(cls.IsMulticastDelegate);

            ExportGenericVariance(cls, writer);

            foreach (CppMethod method in cls.Methods)
            {
                if (method.Name == "Invoke")
                {
                    writer.Write(fileBuilder.IndexMethodSignatureTag(RpaTagFactory.CreateMethodSignature(method.MethodSignature)));
                    return;
                }
            }
            throw new ParseFailedException("Malformed delegate");
        }

        private void ExportGenericVariance(CppClass cls, BinaryWriter writer)
        {
            if (cls.NumGenericParameters > 0)
            {
                foreach (CLRGenericParamRow genericParam in cls.TypeDef.GenericParameters)
                {
                    Clarity.Rpa.HighVariance variance;
                    switch (genericParam.Variance)
                    {
                        case CLRGenericParamRow.VarianceEnum.Contravariant:
                            variance = Clarity.Rpa.HighVariance.Contravariant;
                            break;
                        case CLRGenericParamRow.VarianceEnum.Covariant:
                            variance = Clarity.Rpa.HighVariance.Covariant;
                            break;
                        case CLRGenericParamRow.VarianceEnum.None:
                            variance = Clarity.Rpa.HighVariance.None;
                            break;
                        default:
                            throw new Exception();
                    }
                    writer.Write((byte)variance);
                }
            }
        }

        private void ExportClassDefinitions(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppClass cls)
        {
            if (cls.IsDelegate || cls.IsEnum)
                throw new ArgumentException();

            if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                ExportGenericVariance(cls, writer);

            if (!cls.IsValueType && cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Class)
            {
                writer.Write(cls.TypeDef.IsSealed);
                writer.Write(cls.TypeDef.IsAbstract);
                if (cls.ParentTypeSpec == null)
                    writer.Write((uint)0);
                else
                    writer.Write(1 + fileBuilder.IndexTypeSpecTag(RpaTagFactory.CreateTypeTag(cls.ParentTypeSpec)));
            }

            writer.Write((uint)cls.ExplicitInterfaces.Count);
            foreach (CLRTypeSpec typeSpec in cls.ExplicitInterfaces)
                writer.Write(fileBuilder.IndexTypeSpecTag(RpaTagFactory.CreateTypeTag(typeSpec)));

            WriteVtableThunks(fileBuilder, writer, cls);

            if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Class)
            {
                uint numNonAbstractMethods = 0;

                foreach (CppMethod method in cls.Methods)
                {
                    if (!method.Abstract)
                        numNonAbstractMethods++;
                }

                writer.Write(numNonAbstractMethods);

                foreach (CppMethod method in cls.Methods)
                {
                    if (method.Abstract)
                        continue;

                    writer.Write(method.Static);
                    writer.Write(fileBuilder.IndexMethodSignatureTag(RpaTagFactory.CreateMethodSignature(method.MethodSignature)));
                    writer.Write(fileBuilder.IndexString(method.Name));
                    ExportMethodCode(fileBuilder, writer, cls, method);
                }

                uint numInstanceFields = 0;
                foreach (CppField field in cls.Fields)
                {
                    CLRFieldRow fieldDef = field.Field;
                    if (!fieldDef.Literal && !fieldDef.Static)
                        numInstanceFields++;
                }

                writer.Write(numInstanceFields);
                foreach (CppField field in cls.Fields)
                {
                    CLRFieldRow fieldDef = field.Field;
                    if (!fieldDef.Literal && !fieldDef.Static)
                    {
                        writer.Write(fileBuilder.IndexTypeSpecTag(RpaTagFactory.CreateTypeTag(field.Type)));
                        writer.Write(fileBuilder.IndexString(field.Name));
                    }
                }

                WriteInterfaceImplementations(fileBuilder, writer, cls);
            }
        }

        public void ExportTypeDef(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CLRTypeDefRow typeDef)
        {
            CppClass cls = GetCachedClass(new CLRTypeSpecClass(typeDef));

            Clarity.Rpa.TypeSemantics sem;
            if (cls.IsDelegate)
                sem = Clarity.Rpa.TypeSemantics.Delegate;
            else if (cls.IsEnum)
                sem = Clarity.Rpa.TypeSemantics.Enum;
            else if (cls.IsValueType)
                sem = Clarity.Rpa.TypeSemantics.Struct;
            else if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                sem = Clarity.Rpa.TypeSemantics.Interface;
            else
                sem = Clarity.Rpa.TypeSemantics.Class;

            writer.Write((byte)sem);
            writer.Write(fileBuilder.IndexTypeNameTag(RpaTagFactory.CreateTypeNameTag(typeDef)));

            if (sem == Clarity.Rpa.TypeSemantics.Delegate)
            {
                ExportDelegate(fileBuilder, writer, cls);
            }
            else if (sem == Clarity.Rpa.TypeSemantics.Enum)
            {
                ExportEnum(fileBuilder, writer, cls);
            }
            else
            {
                ExportClassDefinitions(fileBuilder, writer, cls);
                ExportClassStubs(cls);

                if (sem == Clarity.Rpa.TypeSemantics.Class || sem == Clarity.Rpa.TypeSemantics.Struct)
                    ExportClassStatics(fileBuilder, writer, cls);
            }
        }
    }
}
