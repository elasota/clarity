using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using System.IO;

namespace AssemblyImporter.CppExport
{
    public class CppBuilder
    {
        private CLRAssemblyCollection m_assemblies;
        private string m_exportPath;
        private Dictionary<CLRTypeDefRow, CppClass> m_typeDefClasses;
        private Dictionary<string, CppClass> m_fullNameClasses;

        public CppBuilder(CLRAssemblyCollection assemblies)
        {
            m_assemblies = assemblies;
            m_exportPath = "D:\\clarityexport\\";
            m_typeDefClasses = new Dictionary<CLRTypeDefRow, CppClass>();
            m_fullNameClasses = new Dictionary<string, CppClass>();

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

                for (uint i = 0; i < typeDefs.NumRows; i++)
                {
                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefs.GetRow(i);
                    ExportTypeDef(typeDef);
                }
            }
        }

        public CppClass CreateClassFromType(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecClass)
            {
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
                    parentClass = CreateClassFromType(parentTypeSpec);
                }

                List<CLRTypeSpec> interfaces = new List<CLRTypeSpec>();

                if (cls.FullName == "Tests.TestInterfaceOverrideCollision.MyDerived")
                {
                    int bp = 0;
                }

                foreach (CLRTableRow ii in typeDef.ImplementedInterfaces)
                {
                    CLRTypeSpec ifc = m_assemblies.InternTypeDefOrRefOrSpec(ii);
                    cls.AddExplicitInterface(this, ifc);
                    interfaces.Add(ifc);
                }
                cls.ResolveInherit(parentClass, interfaces, parentTypeSpec);

                return cls;
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)typeSpec;
                CppClass baseClass = CreateClassFromType(gi.GenericType);
                return baseClass.Instantiate(gi.ArgTypes, null);
            }
            else
                throw new NotImplementedException();
        }

        public void CacheTypeDef(CLRTypeDefRow typeDef)
        {
            CppClass cls = CreateClassFromType(new CLRTypeSpecClass(typeDef));
            m_typeDefClasses[typeDef] = cls;
            m_fullNameClasses[cls.FullName] = cls;
        }

        private void CreateDirectoriesForFile(string path)
        {
            string dirPath = path.Substring(0, path.LastIndexOf('\\'));
            Directory.CreateDirectory(dirPath);
        }

        private void ExportClassPrototypes(CppClass cls)
        {
            string protoPath = cls.GeneratePrototypePath();
            CreateDirectoriesForFile(m_exportPath + protoPath);
            using (StreamWriter writer = new StreamWriter(m_exportPath + protoPath, false, System.Text.Encoding.ASCII))
            {
                writer.WriteLine("#pragma once");

                {
                    CppMangleBuilder builder = new CppMangleBuilder();
                    builder.Add(protoPath);
                    string headerId = builder.Finish();
                    writer.WriteLine("#ifndef __CLARITY_CPPEXPORT_PROTO_" + headerId + "__");
                    writer.WriteLine("#define __CLARITY_CPPEXPORT_PROTO_" + headerId + "__");
                }

                writer.WriteLine();
                writer.WriteLine("#include \"ClarityCore.h\"");

                string classCppName = cls.GenerateCppClassName();
                string[] path = classCppName.Split(new string[] { "::" }, StringSplitOptions.None);
                for (int i = 1; i < path.Length - 1; i++)
                {
                    writer.WriteLine("namespace " + path[i]);
                    writer.WriteLine("{");
                }

                string clsName = path[path.Length - 1];

                if (cls.NumGenericParameters > 0)
                {
                    writer.Write("\ttemplate<");
                    for (int i = 0; i < cls.NumGenericParameters; i++)
                    {
                        if (i != 0)
                            writer.Write(", ");
                        writer.Write("class T" + i.ToString());
                    }
                    writer.WriteLine(">");
                }

                writer.WriteLine("\tstruct " + clsName + ";");

                for (int i = 1; i < path.Length - 1; i++)
                    writer.WriteLine("}");
                writer.Write("template<");
                for (int i = 0; i < cls.NumGenericParameters; i++)
                {
                    if (i != 0)
                        writer.Write(", ");
                    writer.Write("class T" + i.ToString());
                }

                writer.Write("> struct ::CLRTI::TypeTraits<");
                writer.Write(classCppName);

                if (cls.NumGenericParameters > 0)
                {
                    writer.Write("<");
                    for (int i = 0; i < cls.NumGenericParameters; i++)
                    {
                        if (i != 0)
                            writer.Write(", ");
                        writer.Write("T" + i.ToString());
                    }
                    writer.Write(" >");
                }

                writer.WriteLine(" >");
                writer.WriteLine("{");

                writer.WriteLine("\tenum");
                writer.WriteLine("\t{");
                writer.WriteLine("\t\tIsValueType = " + (cls.IsValueType ? "1" : "0") + ",");
                writer.WriteLine("\t};");

                writer.WriteLine("};");

                writer.WriteLine("#endif");
            }
        }

        private static string MakeRefName(string name, bool usesGenericParams)
        {
            string result = "::CLRUtil::Ref<" + name + " >::Type";
            if (usesGenericParams)
                result = "typename " + result;
            return result;
        }

        private string SpecToValueType(CLRTypeSpec ts)
        {
            if (ts is CLRTypeSpecClass)
            {
                CLRTypeSpecClass cls = (CLRTypeSpecClass)ts;
                CppClass cppClass = m_typeDefClasses[cls.TypeDef];
                string clsName = cppClass.GenerateCppClassName();
                if (!cppClass.IsValueType)
                    return MakeRefName(clsName, ts.UsesAnyGenericParams);
                return clsName;
            }
            else if (ts is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)ts;
                string name = SpecToClassName(gi.GenericType);
                name += " <";
                for (int i = 0; i < gi.ArgTypes.Length; i++)
                {
                    if (i != 0)
                        name += ",";
                    name += SpecToClassName(gi.ArgTypes[i]);
                }
                name += " >";
                CppClass cppClass = m_typeDefClasses[gi.GenericType.TypeDef];
                if (!cppClass.IsValueType)
                    return MakeRefName(name, ts.UsesAnyGenericParams);
                return name;
            }
            else if (ts is CLRTypeSpecSimple)
            {
                CLRTypeSpecSimple simple = (CLRTypeSpecSimple)ts;
                switch (simple.BasicType)
                {
                    case CLRSigType.ElementType.BOOLEAN:
                    case CLRSigType.ElementType.CHAR:
                    case CLRSigType.ElementType.R4:
                    case CLRSigType.ElementType.R8:
                    case CLRSigType.ElementType.I1:
                    case CLRSigType.ElementType.U1:
                    case CLRSigType.ElementType.I2:
                    case CLRSigType.ElementType.U2:
                    case CLRSigType.ElementType.I4:
                    case CLRSigType.ElementType.U4:
                    case CLRSigType.ElementType.I8:
                    case CLRSigType.ElementType.U8:
                    case CLRSigType.ElementType.I:
                    case CLRSigType.ElementType.U:
                        return SpecToClassName(ts);
                    case CLRSigType.ElementType.STRING:
                    case CLRSigType.ElementType.OBJECT:
                        return MakeRefName(SpecToClassName(ts), false);
                    case CLRSigType.ElementType.VOID:
                        return "void";
                }
                throw new ArgumentException();
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                return MakeRefName(SpecToClassName(ts), ts.UsesAnyGenericParams);
            }
            else if (ts is CLRTypeSpecVarOrMVar)
            {
                return "typename ::CLRUtil::Val<" + SpecToClassName(ts) + ">::Type";
            }
            else
                throw new ArgumentException();
        }

        private void AddTypeSpecDependencies(CLRTypeSpec ts, CppDependencySet depSet, bool defIfValue, bool defAlways)
        {
            if (ts is CLRTypeSpecClass)
            {
                CLRTypeSpecClass cls = (CLRTypeSpecClass)ts;
                CppClass cppClass = m_typeDefClasses[cls.TypeDef];
                depSet.AddDependency(cppClass.FullName, defAlways || (defIfValue && cppClass.IsValueType));
            }
            else if (ts is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)ts;
                CppClass genericClass = m_typeDefClasses[gi.GenericType.TypeDef];

                if (defAlways == false && defIfValue == true && !genericClass.IsValueType)
                    return;

                AddTypeSpecDependencies(gi.GenericType, depSet, defIfValue, defAlways);

                CppClass expandedClass = CreateClassFromType(ts);

                foreach (CppField field in expandedClass.Fields)
                    if (field.OriginallyGenericParam)
                        AddTypeSpecDependencies(field.Type, depSet, true, false);
                foreach (CppField field in expandedClass.InheritedFields)
                    if (field.OriginallyGenericParam)
                        AddTypeSpecDependencies(field.Type, depSet, true, false);
            }
            else if (ts is CLRTypeSpecSimple)
            {
                string fullName = SimpleTypeFullName(((CLRTypeSpecSimple)ts).BasicType);
                CppClass cppClass = m_fullNameClasses[fullName];
                depSet.AddDependency(cppClass.FullName, defAlways || (defIfValue && cppClass.IsValueType));
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                CLRTypeSpecSZArray szArray = (CLRTypeSpecSZArray)ts;
                AddTypeSpecDependencies(szArray.SubType, depSet, false, false);
            }
            else if (ts is CLRTypeSpecVarOrMVar)
            {
            }
            else
                throw new ArgumentException();
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

        private string SpecToClassName(CLRTypeSpec ts)
        {
            if (ts is CLRTypeSpecClass)
            {
                CLRTypeSpecClass cls = (CLRTypeSpecClass)ts;
                return m_typeDefClasses[cls.TypeDef].GenerateCppClassName();
            }
            else if (ts is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)ts;
                string name = SpecToClassName(gi.GenericType);
                name += "< ";
                for (int i = 0; i < gi.ArgTypes.Length; i++)
                {
                    if (i != 0)
                        name += ",";
                    name += SpecToClassName(gi.ArgTypes[i]);
                }
                name += " >";
                return name;
            }
            else if (ts is CLRTypeSpecSimple)
            {
                CLRTypeSpecSimple simple = (CLRTypeSpecSimple)ts;
                return CppClass.GenerateCppClassNameFromFullName(SimpleTypeFullName(simple.BasicType));

                throw new ArgumentException();
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                CLRTypeSpecSZArray szArray = (CLRTypeSpecSZArray)ts;
                return "::CLRCore::SZArray< " + SpecToClassName(szArray.SubType) + " >";
            }
            else if (ts is CLRTypeSpecVarOrMVar)
            {
                CLRTypeSpecVarOrMVar varOrMvar = (CLRTypeSpecVarOrMVar)ts;
                if (varOrMvar.ElementType == CLRSigType.ElementType.VAR)
                    return "T" + varOrMvar.Value.ToString();
                else if (varOrMvar.ElementType == CLRSigType.ElementType.MVAR)
                    return "M" + varOrMvar.Value.ToString();
                else
                    throw new ArgumentException();
            }
            else
                throw new ArgumentException();
        }

        private enum MethodParameterMapping
        {
            ClassImpl,
            DelegateDef,
        }

        private void WriteMethodParameters(StreamWriter writer, CLRTypeSpec disambig, CLRMethodSignatureInstance sig, MethodParameterMapping mappingType)
        {
            writer.Write("(const ::CLRExec::Frame &frame");

            if (disambig != null)
            {
                writer.Write(", const ");
                writer.Write(SpecToClassName(disambig));
                writer.Write("* paramIfcDA");
            }

            if (mappingType == MethodParameterMapping.DelegateDef)
                writer.Write(", ::CLRUtil::DGTarget dgtarget");
            
            for (int i = 0; i < sig.ParamTypes.Length; i++)
            {
                writer.Write(", ");
                CLRMethodSignatureInstanceParam param = sig.ParamTypes[i];
                if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.ByRef)
                {
                    writer.Write("typename ::CLRUtil::RefParameter< ");
                    writer.Write(SpecToValueType(param.Type));
                    writer.Write(" >::Type param" + i.ToString());
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.TypedByRef)
                {
                    throw new NotImplementedException();
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                {
                    writer.Write(SpecToValueType(param.Type));
                    writer.Write(" param" + i.ToString());
                }
                else
                    throw new NotSupportedException("Unsupported method parameter type");
            }
            writer.Write(")");
        }

        private void WriteDelegateThunk(CppClass cls, string funcName, uint numMethodGenericParams, CLRMethodSignatureInstance sig, bool isStatic, StreamWriter writer)
        {
            writer.Write("\t\ttemplate<");
            bool haveRP = !TypeSpecIsVoid(sig.RetType);
            writer.Write("class PR");

            for (int i = 0; i < sig.ParamTypes.Length; i++)
                writer.Write(", class P" + i.ToString());

            for (uint i = 0; i < numMethodGenericParams; i++)
                writer.Write(", class M" + i.ToString());

                writer.WriteLine(">");
            writer.Write("\t\tinline static ");
            if (haveRP)
                writer.Write("typename ::CLRUtil::DGBoundReturn<PR>::Type");
            else
                writer.Write("void");

            string paramMangle;
            {
                CppMangleBuilder builder = new CppMangleBuilder();
                builder.Add(sig);
                paramMangle = builder.Finish();
            }

            writer.Write(" dgbind_" + funcName + "_p" + paramMangle + "(");
            writer.Write("const ::CLRExec::Frame &frame, ::CLRUtil::DGTarget dgtarget");
            for (int i=0;i<sig.ParamTypes.Length;i++)
            {
                writer.Write(", ");
                CLRMethodSignatureInstanceParam param = sig.ParamTypes[i];

                if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.ByRef)
                {
                    writer.Write("typename ::CLRUtil::RefParameter<");
                    writer.Write(SpecToValueType(param.Type));
                    writer.Write(" >::Type dgparam" + i.ToString());
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.TypedByRef)
                {
                    throw new NotImplementedException();
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                {
                    writer.Write("typename ::CLRUtil::Val<P" + i.ToString() + ">::Type param" + i.ToString());
                }
                else
                    throw new NotSupportedException("Unsupported method parameter type");
            }


            writer.WriteLine(")");
            writer.WriteLine("\t\t{");

            if (haveRP)
            {
                writer.Write("\t\t\t");
                writer.Write(SpecToValueType(sig.RetType));
                writer.WriteLine(" dgreturnValue;");
            }

            for (int i=0;i<sig.ParamTypes.Length;i++)
            {
                CLRMethodSignatureInstanceParam param = sig.ParamTypes[i];

                if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.ByRef)
                {
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.TypedByRef)
                {
                    throw new NotImplementedException();
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                {
                    writer.Write("\t\t\t");
                    writer.Write(SpecToValueType(param.Type));
                    writer.WriteLine(" dgparam" + i.ToString() + ";");
                    writer.WriteLine("\t\t\t::CLRUtil::ConvertDelegateParam<P" + i.ToString() + ", " + SpecToClassName(param.Type) + ">(dgparam" + i.ToString() + ", param" + i.ToString() + ");");
                }
                else
                    throw new NotSupportedException("Unsupported method parameter type");
            }

            writer.Write("\t\t\t");

            if (haveRP)
                writer.Write("dgreturnValue = ");

            if (!isStatic)
            {
                writer.Write("(::CLRUtil::ConvertDelegateTarget<");
                writer.Write(cls.GenerateCppClassName());
                if (cls.NumGenericParameters > 0)
                {
                    writer.Write("<");
                    for (int i = 0; i < cls.NumGenericParameters; i++)
                    {
                        if (i != 0)
                            writer.Write(", ");
                        writer.Write("T" + i.ToString());
                    }
                    writer.Write(" >");
                }

                writer.Write(" >(dgtarget))->");
            }
            
            writer.Write(funcName);

            if (numMethodGenericParams > 0)
            {
                writer.Write("<");
                for (uint i = 0; i < numMethodGenericParams; i++)
                {
                    if (i != 0)
                        writer.Write(", ");
                    writer.Write("M" + i.ToString());
                }
                writer.Write(">");
            }
            
            writer.Write("(frame");
            for (int i = 0; i < sig.ParamTypes.Length; i++)
                writer.Write(", dgparam" + i.ToString());
            writer.WriteLine(");");

            if (haveRP)
                writer.WriteLine("\t\t\treturn ::CLRUtil::ConvertDelegateReturn<PR>(dgreturnValue);");

            writer.WriteLine("\t\t}");
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

        private CppVtableSlot ResolveMethodImplReference(CLRTableRow methodDecl)
        {
            if (methodDecl is CLRMethodDefRow)
            {
                CLRMethodDefRow mdef = (CLRMethodDefRow)methodDecl;

                CppMethod cppMethod = new CppMethod(m_assemblies, mdef.Owner, mdef);
                if (cppMethod.CreatesSlot == null)
                    throw new NotSupportedException("Can't bind an implementation to an override");

                return cppMethod.CreatesSlot;
            }
            else if (methodDecl is CLRMemberRefRow)
            {
                CLRMemberRefRow mref = (CLRMemberRefRow)methodDecl;
                if (mref.MethodSig == null)
                    throw new ParseFailedException("Strange method override encountered");

                CppClass declaredInClass = this.CreateClassFromType(m_assemblies.InternTypeDefOrRefOrSpec(mref.Class));
                CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_assemblies, mref.MethodSig);

                foreach (CppVtableSlot slot in declaredInClass.VisibleVtableSlots)
                {
                    if (slot.InternalName == mref.Name && sig.Equals(slot.DeclaredSignature))
                        return slot;
                }
                throw new ParseFailedException("Couldn't match method reference");
            }
            else
                throw new NotSupportedException();
        }

        private void WriteInterfaceBinding(CppClass cls, CppVtableSlot decl, CppVtableSlot body, StreamWriter writer, InterfaceConstraintMappingType mappingType, bool proto)
        {
            if (mappingType == InterfaceConstraintMappingType.ClassImpl)
            {
                if (proto)
                    writer.Write("\t\tvirtual ");
                else
                    writer.Write("\t\tinline ");
            }
            else if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
            {
                if (!proto)
                    writer.Write("\t\tCLARITY_FORCEINLINE ");
            }
            else
                throw new ArgumentException();

            writer.Write(SpecToValueType(decl.Signature.RetType));
            writer.Write(" ");

            if (!proto)
            {
                writer.Write("(");
                writer.Write(cls.GenerateCppClassName());
                writer.Write("::");
            }

            bool wantDisambig = (mappingType == InterfaceConstraintMappingType.ClassImpl && decl.IsGenericInterface);

            if (wantDisambig)
                writer.Write("i");
            writer.Write(decl.GenerateName());

            if (!proto)
                writer.Write(")");

            WriteMethodParameters(writer, wantDisambig ? decl.DisambigSpec : null, decl.Signature, MethodParameterMapping.ClassImpl);
            if (mappingType == InterfaceConstraintMappingType.ClassImpl)
            {
                if (proto)
                    writer.WriteLine(" CLARITY_OVERRIDE;");
                else
                    writer.WriteLine();
            }
            else if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
            {
                if (proto)
                    writer.WriteLine(";");
                else
                    writer.WriteLine();
            }
            else
                throw new ArgumentException();

            if (!proto)
            {
                writer.WriteLine("\t\t{");
                writer.Write("\t\t\t");
                if (!TypeSpecIsVoid(decl.Signature.RetType))
                    writer.Write("return ");

                if (mappingType == InterfaceConstraintMappingType.ClassImpl)
                    writer.Write("this->");
                else if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
                    writer.Write("this->bGlue.");
                else
                    throw new ArgumentException();

                writer.Write(body.GenerateName());
                writer.Write("(frame");
                for (int i = 0; i < decl.Signature.ParamTypes.Length; i++)
                {
                    writer.Write(", param" + i.ToString());
                }
                writer.WriteLine(");");
                writer.WriteLine("\t\t}");
            }
        }

        private enum InterfaceConstraintMappingType
        {
            ClassImpl,
            GenericConstraint,
        }

        private void WriteInterfaceImplementations(CppClass cls, StreamWriter writer, InterfaceConstraintMappingType mappingType, bool proto)
        {
            // Emit passive conversions
            if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Class)
            {
                foreach (CLRTypeSpec conv in cls.PassiveInterfaceConversions)
                {
                    writer.Write("\t\tinline virtual void iPassiveConvertInterface(");
                    writer.Write(SpecToClassName(conv));
                    writer.WriteLine("*& ref) CLARITY_OVERRIDE CLARITY_FINAL { ref = this; }");
                }

                List<CppVtableSlot> requiredVTableSlots = new List<CppVtableSlot>();

                List<CLRTypeSpec> reqBindings = new List<CLRTypeSpec>();

                reqBindings.AddRange(cls.NewlyImplementedInterfaces);
                reqBindings.AddRange(cls.ReimplementedInterfaces);

                List<BoundInterfaceMethodImpl> boundImpls = new List<BoundInterfaceMethodImpl>();

                foreach (CLRMethodImplRow methodImpl in cls.TypeDef.MethodImplementations)
                {
                    CppVtableSlot decl = ResolveMethodImplReference(methodImpl.MethodDeclaration);
                    CppVtableSlot body = ResolveMethodImplReference(methodImpl.MethodBody);
                    CLRTypeSpec spec = m_assemblies.InternTypeDefOrRefOrSpec(methodImpl.Class);

                    boundImpls.Add(new BoundInterfaceMethodImpl(spec, decl, body));
                }

                foreach (CLRTypeSpec conv in reqBindings)
                {
                    CppClass ifcClass = this.CreateClassFromType(conv);

                    bool isReimpl = false;
                    foreach (CLRTypeSpec reimpl in cls.ReimplementedInterfaces)
                    {
                        if (reimpl.Equals(conv))
                        {
                            isReimpl = true;
                            break;
                        }
                    }

                    if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
                    {
                        writer.Write("template<");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);

                        writer.WriteLine(" >");

                        writer.Write("struct ::CLRUtil::ConstrainedInterfaceBindingGlue<");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.Write(",");
                        writer.Write(SpecToClassName(conv));
                        writer.WriteLine(" >");
                        writer.WriteLine("{");
                        writer.Write("\t\t::CLRUtil::ConstrainedVtableGlue<");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine(" > bGlue;");
                        writer.Write("\t\tCLARITY_FORCEINLINE explicit ConstrainedInterfaceBindingGlue(");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine("* bPtr)");
                        writer.WriteLine("\t\t\t: bGlue(bPtr)");
                        writer.WriteLine("\t\t{");
                        writer.WriteLine("\t\t}");
                    }

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
                                bimi.InterfaceSlot.InternalName == slot.InternalName && bimi.InterfaceSlot.Signature.Equals(slot.Signature))
                            {
                                isExplicitlyBound = true;
                                WriteInterfaceBinding(cls, bimi.InterfaceSlot, bimi.ClassSlot, writer, mappingType, proto);
                                boundImpls.RemoveAt(i);
                                break;
                            }
                        }

                        if (!isExplicitlyBound)
                        {
                            // Look for a matching slot
                            bool haveMatch = false;
                            foreach (CppVtableSlot vtSlot in cls.VisibleVtableSlots)
                            {
                                if (slot.InternalName == vtSlot.InternalName && slot.Signature.Equals(vtSlot.Signature))
                                {
                                    if (haveMatch)
                                        throw new ParseFailedException("Multiple visible vtable slots could implement the same interface method");
                                    haveMatch = true;

                                    WriteInterfaceBinding(cls, slot, vtSlot, writer, mappingType, proto);
                                }
                            }

                            // If there's no match, but this is a reimplementation, then use the old implementation
                            // Allows TestInheritedReimpl to pass.
                            if (!haveMatch && !isReimpl)
                                throw new ParseFailedException("Unmatched interface method");
                        }
                    }

                    if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
                        writer.WriteLine("};");
                }

                if (boundImpls.Count > 0)
                    throw new ParseFailedException("Unmatched bound interface override");

                /*
                    CppClass ifcClass = builder.CreateClassFromType(conv);

                    foreach (CppMethod method in ifcClass.Methods)
                    {
                        CppVtableSlot slot = method.CreatesSlot;
                        if (slot == null)
                            throw new ArgumentException();
                        requiredVTableSlots.Add(slot);
                    }

                    writer.WriteLine("\t\t// Implement " + SpecToClassName(conv));
                }
                 * */
                //Dictionary<CppVtableSlot.SlotKey, CppVtableSlot> implMappings = new Dictionary<CppVtableSlot, CppVtableSlot>();

                /*
                foreach (CLRTypeSpec conv in cls.ImplementedInterfaces)
                {
                    CppClass ifcClass = builder.CreateClassFromType(conv);
                    foreach (CppMethod method in ifcClass.Methods)
                    {
                        CppVtableSlot slot = method.CreatesSlot;
                        if (slot == null)
                            throw new ArgumentException();
                        requiredVTableSlots.Add(slot);
                    }

                    writer.WriteLine("\t\t// Implement " + SpecToClassName(conv));
                    foreach (CLRMethodImplRow methodImpl in cls.TypeDef.MethodImplementations)
                    {
                        CppVtableSlot slotToOverride = null;

                        CLRTableRow methodDecl = methodImpl.MethodDeclaration;
                        if (methodDecl is CLRMethodDefRow)
                        {
                            CLRMethodDefRow mdef = (CLRMethodDefRow)methodDecl;
                            //CppClass declaredInClass = builder.CreateClassFromType(m_assemblies.InternTypeDefOrRefOrSpec(mref.Class));

                            CppClass declaredInClass = builder.CreateClassFromType(m_assemblies.InternTypeDefOrRefOrSpec(mdef.Owner));
                            CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_assemblies, mdef.Signature);

                            foreach (CppVtableSlot slot in declaredInClass.VtableSlots)
                            {
                                if (slot.InternalName == mdef.Name && sig.Equals(slot.DeclaredSignature))
                                {
                                    slotToOverride = slot;
                                    break;
                                }
                            }
                        }
                        else if (methodDecl is CLRMemberRefRow)
                        {
                            CLRMemberRefRow mref = (CLRMemberRefRow)methodDecl;
                            if (mref.MethodSig == null)
                                throw new ParseFailedException("Strange method override encountered");
                            CppClass declaredInClass = builder.CreateClassFromType(m_assemblies.InternTypeDefOrRefOrSpec(mref.Class));
                            CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_assemblies, mref.MethodSig);

                            foreach (CppVtableSlot slot in declaredInClass.VtableSlots)
                            {
                                if (slot.InternalName == mref.Name && sig.Equals(slot.DeclaredSignature))
                                {
                                    slotToOverride = slot;
                                    break;
                                }
                            }
                        }
                        else
                            throw new NotSupportedException();

                        if (slotToOverride == null)
                            throw new ParseFailedException("Failed to find matching explicit override");


                    }
                }
                 * */
            }
            else if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
            {
                List<CLRTypeSpec> allPassiveConversions = new List<CLRTypeSpec>();
                allPassiveConversions.AddRange(cls.NewlyImplementedInterfaces);
                allPassiveConversions.AddRange(cls.InheritedImplementedInterfaces);
                foreach (CLRTypeSpec conv in allPassiveConversions)
                {
                    writer.Write("\t\tvirtual void iPassiveConvertInterface(");
                    writer.Write(SpecToClassName(conv));
                    writer.WriteLine("*& ref) CLARITY_PURE;");
                }
            }
        }

        private enum VtableThunkMappingType
        {
            ClassImpl,
            StructGlue,
        }

        private void WriteVtableThunk(CppClass cls, CppMethod method, string methodName, CppVtableSlot slot, StreamWriter writer, VtableThunkMappingType mappingType, CppDependencySet depSet, bool proto)
        {
            if (!proto)
            {
                if (slot.IsGenericInterface == false && method.Abstract)
                    return;     // No code to export
            }

            CLRMethodSignatureInstance slotSig = slot.Signature;

            if (!proto && cls.NumGenericParameters > 0)
            {
                writer.Write("template<");
                WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                writer.WriteLine(">");
            }
            string vfuncName = slot.GenerateName();
            if (mappingType == VtableThunkMappingType.ClassImpl)
            {
                if (proto)
                {
                    writer.Write("\t\t");
                    if (!slot.IsGenericInterface)
                        writer.Write("virtual ");
                }
                else
                    writer.Write("\t\tinline ");
            }
            else if (mappingType == VtableThunkMappingType.StructGlue)
            {
                writer.Write("\t\t");
                if (!proto)
                    writer.Write("CLARITY_FORCEINLINE ");
            }
            else
                throw new NotSupportedException();
            writer.Write(SpecToValueType(slotSig.RetType));

            writer.Write(" ");
            if (!proto)
            {
                writer.Write("(");
                writer.Write(cls.GenerateCppClassName());
                WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                writer.Write("::");
            }
            writer.Write(vfuncName);
            if (!proto)
            {
                writer.Write(")");
                depSet.AddMethodSigDependencies(slotSig, true);
            }
            WriteMethodParameters(writer, null, slotSig, MethodParameterMapping.ClassImpl);

            if (mappingType == VtableThunkMappingType.ClassImpl)
            {
                if (proto)
                {
                    if (method.Abstract && !slot.IsGenericInterface)
                        writer.Write(" CLARITY_PURE");
                    if (method.Final)
                        writer.Write(" CLARITY_FINAL");
                    if (method.ReplacesStandardSlot != null)
                        writer.Write(" CLARITY_OVERRIDE");
                    writer.Write(";");
                }
            }
            else if (mappingType == VtableThunkMappingType.StructGlue)
            {
                writer.Write(";");
            }
            else
                throw new ArgumentException();
            writer.WriteLine();

            bool returnsAnything = true;
            if (TypeSpecIsVoid(slot.Signature.RetType))
                returnsAnything = false;

            if (!proto)
            {
                if (!method.Abstract)
                {
                    writer.WriteLine("\t\t{");
                    writer.Write("\t\t\t");
                    if (returnsAnything)
                        writer.Write("return ");

                    if (mappingType == VtableThunkMappingType.ClassImpl)
                        writer.Write("this->mcall_" + methodName + "(frame");
                    else if (mappingType == VtableThunkMappingType.StructGlue)
                        writer.Write("this->bGluePtr->mcall_" + methodName + "(frame");
                    else
                        throw new ArgumentException();

                    for (int i = 0; i < slot.Signature.ParamTypes.Length; i++)
                        writer.Write(", param" + i.ToString());
                    writer.WriteLine(");");
                    writer.WriteLine("\t\t}");
                }
                else if (slot.IsGenericInterface)
                {
                    writer.WriteLine("\t\t{");
                    writer.Write("\t\t\t");
                    if (returnsAnything)
                        writer.Write("return ");

                    writer.Write("this->i" + vfuncName + "(frame, this");
                    for (int i = 0; i < slot.Signature.ParamTypes.Length; i++)
                        writer.Write(", param" + i.ToString());
                    writer.WriteLine(");");
                    writer.WriteLine("\t\t}");
                }
            }

            if (proto && slot.IsGenericInterface)
            {
                writer.Write("\t\tvirtual ");
                writer.Write(SpecToValueType(slotSig.RetType));
                writer.Write(" i" + vfuncName);
                WriteMethodParameters(writer, slot.DisambigSpec, slotSig, MethodParameterMapping.ClassImpl);
                writer.WriteLine(" CLARITY_PURE;");
            }

        }

        private void WriteVtableThunks(CppClass cls, StreamWriter writer, VtableThunkMappingType mappingType, CppDependencySet depSet, bool proto)
        {
            foreach (CppMethod method in cls.Methods)
            {
                string methodName = LegalizeName(method.Name, true);
                if (method.GenericTypeParamMangle != null)
                    methodName += method.GenericTypeParamMangle;

                bool isGenericInterface = (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface && cls.NumGenericParameters > 0);

                if (method.CreatesSlot != null)
                {
                    WriteVtableThunk(cls, method, methodName, method.CreatesSlot, writer, mappingType, depSet, proto);
                    if (mappingType == VtableThunkMappingType.ClassImpl && proto)
                        WriteDelegateThunk(cls, method.CreatesSlot.GenerateName(), method.NumGenericParameters, method.CreatesSlot.Signature, method.Static, writer);
                }
                else
                {
                    if (method.ReplacesStandardSlot != null)
                        WriteVtableThunk(cls, method, methodName, method.ReplacesStandardSlot, writer, mappingType, depSet, proto);
                    foreach (CppVtableSlot slot in method.ReplacesExplicitSlots)
                        WriteVtableThunk(cls, method, methodName, slot, writer, mappingType, depSet, proto);
                }
            }
        }

        private static void WriteTemplateParamCluster(bool conditionalBrackets, int numParameters, string prefix, StreamWriter writer)
        {
            if (conditionalBrackets && numParameters > 0)
                writer.Write("<");
            for (int i = 0; i < numParameters; i++)
            {
                if (i != 0)
                    writer.Write(", ");
                writer.Write(prefix);
                writer.Write(i.ToString());
            }
            if (conditionalBrackets && numParameters > 0)
                writer.Write(">");
            
        }

        private static bool TypeSpecIsVoid(CLRTypeSpec ts)
        {
            if ((ts is CLRTypeSpecSimple) && ((CLRTypeSpecSimple)ts).BasicType == CLRSigType.ElementType.VOID)
                return true;
            return false;
        }

        private void ExportClassCode(CppClass cls, bool exportInline, Stream outStream)
        {
            CppDependencySet depSet = new CppDependencySet();

            string inlineCodePath = cls.GenerateInlineCodePath();

            byte[] bodyContents;
            using (MemoryStream bodyMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(bodyMS, System.Text.Encoding.ASCII))
                {
                    if (cls.IsValueType)
                    {
                        writer.WriteLine("// box thunks");

                        foreach (CppMethod method in cls.Methods)
                        {
                            if (method.Static)
                                continue;

                            if (method.Abstract)
                                throw new ArgumentException();

                            string methodName = method.GenerateBaseName();

                            depSet.AddMethodSigDependencies(method.MethodSignature, true);

                            writer.Write("\tCLARITY_FORCEINLINE ");
                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" ");
                            writer.Write("(::CLRUtil::Boxed<");
                            writer.Write(cls.GenerateCppClassName());
                            writer.Write(" >::mcall_");
                            writer.Write(methodName);
                            writer.Write(")");
                            WriteMethodParameters(writer, null, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            writer.WriteLine();
                            writer.WriteLine("\t{");

                            bool returnsAnything = true;
                            if (TypeSpecIsVoid(method.MethodSignature.RetType))
                                returnsAnything = false;

                            writer.Write("\t\t");
                            if (returnsAnything)
                                writer.Write("return ");
                            writer.Write("this->bValue.mcall_");
                            writer.Write(methodName);
                            writer.Write("(frame");
                            for (int i = 0; i < method.MethodSignature.ParamTypes.Length; i++)
                                writer.Write(", param" + i.ToString());
                            writer.WriteLine(");");

                            writer.WriteLine("\t}");
                        }
                    }

                    if (!cls.IsValueType)
                    {
                        writer.WriteLine("// vtable thunks");
                        WriteVtableThunks(cls, writer, VtableThunkMappingType.ClassImpl, depSet, false);
                        writer.WriteLine("// interface bindings");
                        WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.ClassImpl, false);
                    }
                }
                bodyContents = bodyMS.ToArray();
            }

            using (MemoryStream tempMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(tempMS, System.Text.Encoding.ASCII))
                {
                    depSet.WriteAll(writer);
                    writer.WriteLine();
                    writer.Flush();

                    tempMS.Write(bodyContents, 0, bodyContents.Length);

                    tempMS.WriteTo(outStream);
                }
            }
        }

        private void ExportClassDefinitions(CppClass cls)
        {
            string defPath = cls.GenerateDefinitionPath();
            string protoPath = cls.GeneratePrototypePath();
            string classCppName = cls.GenerateCppClassName();
            string inlineCodePath = cls.GenerateInlineCodePath();
            CreateDirectoriesForFile(m_exportPath + defPath);

            CppDependencySet depSet = new CppDependencySet();

            bool staticNeedStaticInit = cls.HaveStaticFields;
            bool instanceNeedStaticInit = (cls.HaveStaticFields && cls.IsValueType);

            byte[] bodyContents;
            using (MemoryStream bodyMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(bodyMS, System.Text.Encoding.ASCII))
                {
                    string[] path = classCppName.Split(new string[] { "::" }, StringSplitOptions.None);
                    for (int i = 1; i < path.Length - 1; i++)
                    {
                        writer.WriteLine("namespace " + path[i]);
                        writer.WriteLine("{");
                    }

                    string clsName = path[path.Length - 1];

                    if (cls.NumGenericParameters > 0)
                    {
                        writer.Write("\ttemplate<");
                        for (int i = 0; i < cls.NumGenericParameters; i++)
                        {
                            if (i != 0)
                                writer.Write(",");
                            writer.Write("class T" + i.ToString());
                        }
                        writer.Write(">");
                        writer.WriteLine();
                    }

                    writer.WriteLine("\tstruct " + clsName);
                    if (!cls.IsValueType)
                    {
                        if (cls.ParentTypeSpec != null)
                        {
                            writer.WriteLine("\t\t: public " + SpecToClassName(cls.ParentTypeSpec));
                            AddTypeSpecDependencies(cls.ParentTypeSpec, depSet, true, true);

                            foreach (CLRTypeSpec ifc in cls.NewlyImplementedInterfaces)
                            {
                                AddTypeSpecDependencies(ifc, depSet, true, true);
                                writer.WriteLine("\t\t, public " + SpecToClassName(ifc));
                            }
                        }
                        else
                            writer.WriteLine("\t\t: public ::CLRCore::RefTarget");
                    }
                    writer.WriteLine("\t{");

                    if (cls.IsDelegate)
                    {
                        CLRMethodSignatureInstance delegateSig = cls.DelegateSignature;
                        writer.WriteLine("\t\t// delegate defs");

                        writer.Write("\t\ttypedef ");
                        if (TypeSpecIsVoid(delegateSig.RetType))
                            writer.Write("void");
                        else
                            writer.Write(SpecToValueType(delegateSig.RetType));
                        writer.Write(" (*BFuncPtr)");
                        WriteMethodParameters(writer, null, delegateSig, MethodParameterMapping.DelegateDef);
                        writer.WriteLine(";");
                        writer.WriteLine("\t\t::CLRUtil::SizeT bNumCallbacks;");
                        writer.WriteLine("\t\tconst ::CLRUtil::DelegateTargetCallbackPair<BFuncPtr> *bCallbackPairs;");
                        writer.WriteLine();
                    }

                    Dictionary<CppMethod, string> methodNames = new Dictionary<CppMethod, string>();
                    //if (!cls.IsDelegate)
                    {
                        writer.WriteLine("\t\t// methods");
                        foreach (CppMethod method in cls.Methods)
                        {
                            if (method.Abstract)
                                continue;

                            if (cls.IsDelegate)
                            {
                                if (method.Name == ".ctor" || method.Name == "BeginInvoke" || method.Name == "EndInvoke")
                                    continue;   // Delegate constructors are completely elided in Clarity
                            }

                            depSet.AddMethodSigDependencies(method.MethodSignature, false);

                            string methodBaseName = method.GenerateBaseName();

                            string methodName = "mcall_" + methodBaseName;

                            methodNames[method] = methodName;

                            bool needStaticInit = method.Static ? staticNeedStaticInit : instanceNeedStaticInit;

                            // Write new methods
                            writer.Write("\t\t");
                            if (method.Static)
                                writer.Write("static ");
                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" " + methodName);
                            WriteMethodParameters(writer, null, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            writer.WriteLine(";");

                            writer.Write("\t\t");
                            if (method.Static)
                                writer.Write("static ");
                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" mcode_" + methodBaseName);
                            WriteMethodParameters(writer, null, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            writer.WriteLine(";");

                            if (!cls.IsValueType && method.Name != ".ctor")
                                WriteDelegateThunk(cls, methodName, method.NumGenericParameters, method.MethodSignature, method.Static, writer);
                        }

                        writer.WriteLine();

                        writer.WriteLine("\t\t// fields");
                        foreach (CppField field in cls.Fields)
                        {
                            writer.Write("\t\t");
                            writer.Write(SpecToValueType(field.Type));
                            writer.Write(" f");
                            writer.Write(LegalizeName(field.Name, true));
                            writer.WriteLine(";");
                        }

                        if (!cls.IsValueType)
                        {
                            writer.WriteLine("\t\t// vtable thunks");
                            WriteVtableThunks(cls, writer, VtableThunkMappingType.ClassImpl, depSet, true);
                            writer.WriteLine("\t\t// interface bindings");
                            WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.ClassImpl, true);
                        }
                    }

                    writer.WriteLine("\t};");

                    for (int i = 1; i < path.Length - 1; i++)
                        writer.WriteLine("}");

                    writer.WriteLine("");

                    // For value types, now write the box specialization
                    if (cls.IsValueType)
                    {
                        writer.WriteLine("#include \"" + CppClass.GenerateDefinitionPathForFullName("System.ValueType") + "\"");
                        writer.WriteLine("");
                        writer.Write("template<");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                        writer.WriteLine(">");

                        writer.Write("struct ::CLRUtil::Boxed<");
                        writer.Write(classCppName);
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine(" >");
                        writer.WriteLine("\t: public " + SpecToClassName(cls.ParentTypeSpec));
                        foreach (CLRTypeSpec ifc in cls.NewlyImplementedInterfaces)
                            writer.WriteLine("\t, public " + SpecToClassName(ifc));
                        //AddTypeSpecDependencies(cls.ParentTypeSpec, depSet, true, true);
                        writer.WriteLine("{");
                        writer.Write("\t" + classCppName);
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine(" bValue;");

                        // Add shims to the underlying value
                        foreach (CppMethod method in cls.Methods)
                        {
                            if (method.Static)
                                continue;

                            if (method.Abstract)
                                throw new ArgumentException();

                            string methodName = methodNames[method];

                            // Write new methods
                            writer.Write("\t");
                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" " + methodName);
                            WriteMethodParameters(writer, null, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            writer.WriteLine(";");
                        }

                        // CLARITYTODO: Overrides in value types are always final, but may not be emitted as such
                        WriteVtableThunks(cls, writer, VtableThunkMappingType.ClassImpl, depSet, true);
                        WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.ClassImpl, true);
                        writer.WriteLine("};");

                        // Write baseline vtable bindings
                        writer.Write("template<");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                        writer.WriteLine(" >");

                        writer.Write("struct ::CLRUtil::ConstrainedVtableGlue<");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine(" >");
                        writer.WriteLine("{");
                        writer.WriteLine("private:");
                        writer.WriteLine("\t\t" + cls.GenerateCppClassName() + "* bGluePtr;");
                        writer.WriteLine("public:");
                        writer.WriteLine("\t\tCLARITY_FORCEINLINE explicit ConstrainedVtableGlue(" + cls.GenerateCppClassName() + "* pPtr)");
                        writer.WriteLine("\t\t\t: bGluePtr(pPtr)");
                        writer.WriteLine("\t\t{");
                        writer.WriteLine("\t\t}");
                        WriteVtableThunks(cls, writer, VtableThunkMappingType.StructGlue, depSet, true);
                        writer.WriteLine("};");

                        // Write interface constraint mappings
                        writer.WriteLine("// interface constraint mappings");
                        WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.GenericConstraint, false);
                    }
                }

                bodyContents = bodyMS.ToArray();
            }

            // Add the class proto itself
            depSet.AddProtoDependency(cls.FullName);

            using (FileStream fs = new FileStream(m_exportPath + defPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs, System.Text.Encoding.ASCII))
                {
                    CppMangleBuilder builder = new CppMangleBuilder();
                    builder.Add(protoPath);
                    string headerId = builder.Finish();
                    writer.WriteLine("#pragma once");
                    writer.WriteLine("#ifndef __CLARITY_CPPEXPORT_DEF_" + headerId + "__");
                    writer.WriteLine("#define __CLARITY_CPPEXPORT_DEF_" + headerId + "__");
                    writer.WriteLine();
                    writer.WriteLine("#include \"ClarityCore.h\"");

                    depSet.WriteAll(writer);
                    writer.WriteLine();
                    writer.Flush();

                    fs.Write(bodyContents, 0, bodyContents.Length);

                    writer.WriteLine("// inline code");
                    ExportClassCode(cls, true, fs);

                    writer.WriteLine("");
                    writer.WriteLine("#endif");
                }
            }
        }

        public void ExportTypeDef(CLRTypeDefRow typeDef)
        {
            CppClass cls = m_typeDefClasses[typeDef];
            ExportClassPrototypes(cls);
            ExportClassDefinitions(cls);
        }

        public static string LegalizeName(string str, bool makeUnique)
        {
            bool hasIllegalChars = false;
            string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
            string result = "";
            foreach (char c in str)
            {
                if (validChars.IndexOf(c) < 0)
                {
                    result += "_";
                    hasIllegalChars = true;
                }
                else
                    result += c;
            }

            if (makeUnique)
            {
                if (hasIllegalChars)
                {
                    CppMangleBuilder builder = new CppMangleBuilder();
                    builder.Add("tokenname");
                    builder.Cycle();
                    builder.Add(str);
                    result = "m" + result + "_" + builder.Finish();
                }
                else
                    result = "t" + result;
            }

            return result;
        }

    }
}
