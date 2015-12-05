﻿using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using System.IO;

namespace AssemblyImporter.CppExport
{
    public class CppBuilder
    {
        public CLRAssemblyCollection Assemblies { get { return m_assemblies; } }

        private CLRAssemblyCollection m_assemblies;
        private string m_exportPath;
        private string m_stubDir;
        private Dictionary<CLRTypeSpec, CppClass> m_typeSpecClasses;
        private Dictionary<string, CppClass> m_fullNameClasses;

        public CppBuilder(string exportDir, string stubDir, CLRAssemblyCollection assemblies)
        {
            m_assemblies = assemblies;
            m_exportPath = exportDir;
            m_stubDir = stubDir;
            m_typeSpecClasses = new Dictionary<CLRTypeSpec, CppClass>();
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
            CppStubExporter.ExportStub(this, m_stubDir, cls);
        }

        private void ExportClassBox(CppClass cls)
        {
            if (!cls.IsValueType)
                return;

            string boxPath = cls.GenerateBoxPath();
            CreateDirectoriesForFile(m_exportPath + boxPath);
            using (StreamWriter writer = new StreamWriter(m_exportPath + boxPath, false, System.Text.Encoding.ASCII))
            {
                writer.WriteLine("#pragma once");

                {
                    CppMangleBuilder builder = new CppMangleBuilder();
                    builder.Add(boxPath);
                    string headerId = builder.Finish();
                    writer.WriteLine("#ifndef __CLARITY_CPPEXPORT_BOX_" + headerId + "__");
                    writer.WriteLine("#define __CLARITY_CPPEXPORT_BOX_" + headerId + "__");
                }

                writer.WriteLine();
                writer.Flush();

                ExportBoxDef(cls, writer.BaseStream, cls.GenerateCppClassName());

                writer.WriteLine("#endif");
            }
        }

        private static void ExportSimpleClassPrototype(StreamWriter writer, int numGenericParameters, string typeCppName)
        {

            string[] path = typeCppName.Split(new string[] { "::" }, StringSplitOptions.None);
            for (int i = 1; i < path.Length - 1; i++)
            {
                writer.WriteLine("namespace " + path[i]);
                writer.WriteLine("{");
            }

            string clsName = path[path.Length - 1];

            if (numGenericParameters > 0)
            {
                writer.Write("\ttemplate< ");
                WriteTemplateParamCluster(false, numGenericParameters, "class T", writer);
                writer.WriteLine(" >");
            }

            writer.WriteLine("\tstruct " + clsName + ";");

            for (int i = 1; i < path.Length - 1; i++)
                writer.WriteLine("}");
            writer.WriteLine();
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

                string cppClassName = cls.GenerateCppClassName();
                ExportSimpleClassPrototype(writer, cls.NumGenericParameters, cppClassName);
                if (cls.HaveNewStaticFields)
                    ExportSimpleClassPrototype(writer, cls.NumGenericParameters, cls.GenerateStaticCppClassName());

                writer.WriteLine("namespace CLRTI");
                writer.WriteLine("{");
                writer.Write("\ttemplate< ");
                WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);

                writer.WriteLine(" >");
                writer.Write("\tstruct TypeProtoTraits< ");
                writer.Write(cppClassName);
                WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);

                writer.WriteLine(" >");
                writer.WriteLine("\t{");

                writer.WriteLine("\t\tenum");
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\tIsValueType = " + (cls.IsValueType ? "1" : "0") + ",");
                writer.WriteLine("\t\t\tIsInterface = " + ((cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface) ? "1" : "0") + ",");
                writer.WriteLine("\t\t\tIsDelegate = " + (cls.IsDelegate ? "1" : "0") + ",");
                writer.WriteLine("\t\t\tIsMulticastDelegate = " + (cls.IsMulticastDelegate ? "1" : "0") + ",");
                writer.WriteLine("\t\t\tIsEnum = " + (cls.IsEnum ? "1" : "0") + ",");
                writer.WriteLine("\t\t\tIsArray = 0,");
                writer.WriteLine("\t\t\tIsReferenceArray = 0,");
                writer.WriteLine("\t\t\tIsTypePlaceholder = 0,");
                writer.WriteLine("\t\t};");
                writer.WriteLine("\t};");
                writer.WriteLine("}");
                writer.WriteLine();

                writer.WriteLine("#endif");
            }
        }

        private static string MakeRefName(string name, bool usesGenericParams)
        {
            string result = "::CLRVM::TRefValue< " + name + " >::Type";
            if (usesGenericParams)
                result = "typename " + result;
            return result;
        }

        private static string MakeValueName(string name, bool usesGenericParams)
        {
            string result = "::CLRVM::TValValue< " + name + " >::Type";
            if (usesGenericParams)
                result = "typename " + result;
            return result;
        }

        public string VTypeStorageToValueType(VType vType)
        {
            switch (vType.ValType)
            {
                case VType.ValTypeEnum.ValueValue:
                    {
                        CLRTypeSpec ts = vType.TypeSpec;
                        string prefix = ts.UsesGenericTypeParams ? "typename " : "";
                        return prefix + "::CLRVM::TValValue< " + SpecToAmbiguousStorage(ts) + " >::Type";
                    }
                case VType.ValTypeEnum.NotNullReferenceValue:
                case VType.ValTypeEnum.NullableReferenceValue:
                    {
                        CLRTypeSpec ts = vType.TypeSpec;
                        string prefix = ts.UsesGenericTypeParams ? "typename " : "";
                        return prefix + "::CLRVM::TRefValue< " + SpecToAmbiguousStorage(ts) + " >::Type";
                    }
                case VType.ValTypeEnum.AnchoredManagedPtr:
                    {
                        CLRTypeSpec ts = vType.TypeSpec;
                        string prefix = ts.UsesGenericTypeParams ? "typename " : "";
                        return prefix + "::CLRVM::TAnchoredManagedPtr< " + SpecToAmbiguousStorage(ts) + " >::Type";
                    }
                case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                    {
                        CLRTypeSpec ts = vType.TypeSpec;
                        string prefix = ts.UsesGenericTypeParams ? "typename " : "";
                        return prefix + "::CLRVM::TMaybeAnchoredManagedPtr< " + SpecToAmbiguousStorage(ts) + " >::Type";
                    }
                case VType.ValTypeEnum.LocalManagedPtr:
                    {
                        CLRTypeSpec ts = vType.TypeSpec;
                        string prefix = ts.UsesGenericTypeParams ? "typename " : "";
                        return prefix + "::CLRVM::TLocalManagedPtr< " + SpecToAmbiguousStorage(ts) + " >::Type";
                    }
                default:
                    throw new ArgumentException();
            }
        }

        public string SpecToAmbiguousStorage(CLRTypeSpec ts)
        {
            string prefix = (ts.UsesAnyGenericParams) ? "typename " : "";
            if (ts is CLRTypeSpecClass)
            {
                CppClass cls = GetCachedClass(ts);
                return prefix + cls.GenerateCppClassName();
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
                return prefix + name;
            }
            else if (ts is CLRTypeSpecVoid)
            {
                throw new ArgumentException();
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                return prefix + SpecToClassName(ts);
            }
            else if (ts is CLRTypeSpecComplexArray)
            {
                return prefix + SpecToClassName(ts);
            }
            else if (ts is CLRTypeSpecVarOrMVar)
            {
                // C++ does not allow typename followed by a template parameter
                return SpecToClassName(ts);
            }
            else
                throw new ArgumentException();
        }

        public string SpecToValueType(CLRTypeSpec ts)
        {
            if (ts is CLRTypeSpecClass)
            {
                CppClass cppClass = GetCachedClass(ts);
                string clsName = cppClass.GenerateCppClassName();
                return MakeValueName(clsName, ts.UsesAnyGenericParams);
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
                CppClass cppClass = GetCachedClass(new CLRTypeSpecClass(gi.GenericType.TypeDef));
                return MakeValueName(name, ts.UsesAnyGenericParams);
            }
            else if (ts is CLRTypeSpecVoid)
            {
                return "void";
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                return MakeValueName(SpecToClassName(ts), ts.UsesAnyGenericParams);
            }
            else if (ts is CLRTypeSpecComplexArray)
            {
                return MakeValueName(SpecToClassName(ts), ts.UsesAnyGenericParams);
            }
            else if (ts is CLRTypeSpecVarOrMVar)
            {
                return "typename ::CLRVM::TValValue< " + SpecToClassName(ts) + " >::Type";
            }
            else
                throw new ArgumentException();
        }

        private void AddTypeSpecDependencies(CLRTypeSpec ts, CppDependencySet depSet, bool defIfValue, bool defAlways)
        {
            if (ts is CLRTypeSpecClass)
            {
                CppClass cls = GetCachedClass(ts);
                bool wantDef = defAlways || (defIfValue && cls.IsValueType);
                depSet.AddDependency(cls.FullName, wantDef ? CppDependencySet.LevelEnum.Def : CppDependencySet.LevelEnum.Proto);
            }
            else if (ts is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)ts;
                CppClass genericClass = GetCachedClass(new CLRTypeSpecClass(gi.GenericType.TypeDef));

                if (defAlways == false && defIfValue == true && !genericClass.IsValueType)
                    return;

                AddTypeSpecDependencies(gi.GenericType, depSet, defIfValue, defAlways);

                CppClass expandedClass = GetCachedClass(ts);

                foreach (CppField field in expandedClass.Fields)
                    if (field.OriginallyGenericParam)
                        AddTypeSpecDependencies(field.Type, depSet, true, false);
                foreach (CppField field in expandedClass.InheritedFields)
                    if (field.OriginallyGenericParam)
                        AddTypeSpecDependencies(field.Type, depSet, true, false);
            }
            else if (ts is CLRTypeSpecVoid)
            {
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

        public string SpecToClassName(CLRTypeSpec ts)
        {
            if (ts is CLRTypeSpecClass)
                return GetCachedClass(ts).GenerateCppClassName();
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
            else if (ts is CLRTypeSpecVoid)
            {
                return "void";
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                CLRTypeSpecSZArray szArray = (CLRTypeSpecSZArray)ts;
                return "::CLRCore::SZArray< " + SpecToClassName(szArray.SubType) + " >";
            }
            else if (ts is CLRTypeSpecComplexArray)
            {
                CLRTypeSpecComplexArray cplxArray = (CLRTypeSpecComplexArray)ts;

                string p = SpecToClassName(cplxArray.SubType);
                p += ", ";
                for (uint r = 0; r < cplxArray.Rank; r++)
                {
                    if (r == cplxArray.Rank - 1)
                        p += "::CLRCore::LastLB<";
                    else
                        p += "::CLRCore::LB<";
                    p += cplxArray.LowBounds[r];
                    if (r != cplxArray.Rank - 1)
                        p += ", ";
                }
                for (uint r = 0; r < cplxArray.Rank; r++)
                    p += " >";
                return "::CLRCore::ComplexArray< " + p + " >";
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

        public enum MethodParameterMapping
        {
            ClassImpl,
            DelegateDef,
        }

        public void WriteMethodParameters(StreamWriter writer, CLRTypeSpec disambig, CLRTypeSpec inlineThisType, CLRMethodSignatureInstance sig, MethodParameterMapping mappingType)
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

            if (inlineThisType != null)
            {
                CppClass cls = GetCachedClass(inlineThisType);
                writer.Write(", ");
                if (cls.IsValueType)
                    writer.Write("::CLRUtil::TValueThisParameter< ");
                else
                    writer.Write("::CLRVM::TValValue< ");
                writer.Write(SpecToAmbiguousStorage(inlineThisType));
                writer.Write(" >::Type bThis0");
            }

            for (int i = 0; i < sig.ParamTypes.Length; i++)
            {
                writer.Write(", ");
                CLRMethodSignatureInstanceParam param = sig.ParamTypes[i];
                if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.ByRef)
                {
                    writer.Write("::CLRUtil::TRefParameter< ");
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

        private void WriteDelegateThunk(CppClass cls, string funcName, int numMethodGenericParams, CLRMethodSignatureInstance sig, bool isStatic, StreamWriter writer, CppDependencySet depSet, bool proto, bool shouldBeInline)
        {
            bool haveRP = !TypeSpecIsVoid(sig.RetType);

            bool isProtoTemplated = haveRP || (sig.ParamTypes.Length > 0);
            bool isDefTemplated = isProtoTemplated || (cls.NumGenericParameters > 0);

            List<string> templateArgs = new List<string>();
            {
                if (!proto)
                {
                    for (int i = 0; i < cls.NumGenericParameters; i++)
                        templateArgs.Add("T" + i.ToString());
                }

                for (int i = 0; i < numMethodGenericParams; i++)
                    templateArgs.Add("M" + i.ToString());

                if (haveRP)
                    templateArgs.Add("PR");

                for (int i = 0; i < sig.ParamTypes.Length; i++)
                    templateArgs.Add("P" + i.ToString());
            }

            if (!proto)
            {
                bool thunkShouldBeInline = (templateArgs.Count > 0);

                if (shouldBeInline != thunkShouldBeInline)
                    return;
            }

            if (templateArgs.Count > 0)
            {
                writer.Write("\t\ttemplate<");
                for (int i = 0; i < templateArgs.Count; i++)
                {
                    if (i != 0)
                        writer.Write(", ");
                    writer.Write("class ");
                    writer.Write(templateArgs[i]);
                }
                writer.WriteLine(">");
            }

            writer.Write("\t\t");
            if (proto)
                writer.Write("static ");
            else if (shouldBeInline)
                writer.Write("inline ");

            if (haveRP)
                writer.Write("typename ::CLRUtil::TDGBoundReturn<PR>::Type");
            else
                writer.Write("void");

            string paramMangle;
            {
                CppMangleBuilder builder = new CppMangleBuilder();
                builder.Add(sig);
                paramMangle = builder.Finish();
            }

            writer.Write(" ");

            if (!proto)
            {
                writer.Write("(");
                writer.Write(cls.GenerateCppClassName());
                WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                writer.Write("::");
            }

            writer.Write("dgbind_" + funcName + "_p" + paramMangle);
            WriteTemplateParamCluster(true, numMethodGenericParams, "M", writer);
            if (!proto)
                writer.Write(")");
            writer.Write("(");
            writer.Write("const ::CLRExec::Frame &frame, ::CLRUtil::TDGTarget dgtarget");
            for (int i = 0; i < sig.ParamTypes.Length; i++)
            {
                writer.Write(", ");
                CLRMethodSignatureInstanceParam param = sig.ParamTypes[i];

                if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.ByRef)
                {
                    writer.Write("typename ::CLRUtil::TRefParameter< ");
                    writer.Write(SpecToValueType(param.Type));
                    writer.Write(" >::Type dgparam" + i.ToString());
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.TypedByRef)
                {
                    throw new NotImplementedException();
                }
                else if (param.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                {
                    writer.Write("typename ::CLRVM::TValValue<P" + i.ToString() + ">::Type param" + i.ToString());
                }
                else
                    throw new NotSupportedException("Unsupported method parameter type");
            }

            writer.Write(")");

            if (proto)
                writer.WriteLine(";");
            else
            {
                writer.WriteLine();
                writer.WriteLine("\t\t{");

                if (haveRP)
                {
                    writer.Write("\t\t\t");
                    writer.Write(SpecToValueType(sig.RetType));
                    writer.WriteLine(" dgreturnValue;");
                    depSet.AddTypeSpecDependencies(sig.RetType, true);
                }

                for (int i = 0; i < sig.ParamTypes.Length; i++)
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
                        writer.WriteLine("\t\t\t::CLRUtil::DelegateVarianceConverter<P" + i.ToString() + ", " + SpecToClassName(param.Type) + ">::Convert(dgparam" + i.ToString() + ", param" + i.ToString() + ");");
                        depSet.AddTypeSpecDependencies(param.Type, true);
                    }
                    else
                        throw new NotSupportedException("Unsupported method parameter type");
                }

                writer.Write("\t\t\t");

                if (haveRP)
                    writer.Write("dgreturnValue = ");

                if (!isStatic)
                {
                    writer.Write("(::CLRUtil::DelegateTargetConverter< ");
                    writer.Write(cls.GenerateCppClassName());
                    depSet.AddTypeSpecDependencies(new CLRTypeSpecClass(cls.TypeDef), true);
                    if (cls.NumGenericParameters > 0)
                    {
                        writer.Write("< ");
                        for (int i = 0; i < cls.NumGenericParameters; i++)
                        {
                            if (i != 0)
                                writer.Write(", ");
                            writer.Write("T" + i.ToString());
                        }
                        writer.Write(" >");
                    }

                    writer.Write(" >::FromTarget(dgtarget))->");
                }

                writer.Write(funcName);

                if (numMethodGenericParams > 0)
                {
                    writer.Write("< ");
                    for (uint i = 0; i < numMethodGenericParams; i++)
                    {
                        if (i != 0)
                            writer.Write(", ");
                        writer.Write("M" + i.ToString());
                    }
                    writer.Write(" >");
                }

                writer.Write("(frame");
                for (int i = 0; i < sig.ParamTypes.Length; i++)
                {
                    writer.Write(", dgparam" + i.ToString());
                    depSet.AddTypeSpecDependencies(sig.ParamTypes[i].Type, true);
                }
                writer.WriteLine(");");

                if (haveRP)
                {
                    writer.Write("\t\t\treturn ::CLRUtil::DelegateVarianceConverter< ");
                    writer.Write(SpecToAmbiguousStorage(sig.RetType));
                    writer.WriteLine(", PR>::Convert(dgreturnValue);");
                }

                writer.WriteLine("\t\t}");
            }
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

        private void WriteInterfaceBinding(CppClass cls, CppVtableSlot decl, CppVtableSlot body, StreamWriter writer, InterfaceConstraintMappingType mappingType, bool proto, bool shouldBeInline)
        {
            if (!proto)
            {
                bool thunkShouldBeInline = (cls.NumGenericParameters > 0);

                if (shouldBeInline != thunkShouldBeInline)
                    return;
            }

            if (mappingType == InterfaceConstraintMappingType.ClassImpl)
            {
                writer.Write("\t\t");
                if (proto)
                    writer.Write("virtual ");
                else if (shouldBeInline)
                    writer.Write("inline ");
            }
            else if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
            {
                writer.Write("\t\t");
                if (proto)
                    writer.Write("virtual ");
                else if (shouldBeInline)
                    writer.Write("CLARITY_FORCEINLINE ");
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

            WriteMethodParameters(writer, wantDisambig ? decl.DisambigSpec : null, null, decl.Signature, MethodParameterMapping.ClassImpl);
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

        private void WriteInterfaceImplementations(CppClass cls, StreamWriter writer, InterfaceConstraintMappingType mappingType, bool proto, bool shouldBeInline)
        {
            if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Class)
            {
                // Emit passive conversions
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

                    if (decl.Equals(body))
                    {
                        // Roslyn sometimes emits redundant MethodImpls that override the vtable slot
                        // that the method already occupies via ReuseSlot.  We want to ignore these.
                        continue;
                    }

                    boundImpls.Add(new BoundInterfaceMethodImpl(spec, decl, body));
                }

                foreach (CLRTypeSpec conv in reqBindings)
                {
                    CppClass ifcClass = this.GetCachedClass(conv);

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
                        writer.WriteLine("namespace CLRUtil");
                        writer.WriteLine("{");
                        writer.Write("\ttemplate<");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);

                        writer.WriteLine(" >");

                        writer.Write("\tstruct ::CLRUtil::ConstrainedInterfaceBindingGlue<");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.Write(",");
                        writer.Write(SpecToClassName(conv));
                        writer.WriteLine(" >");
                        writer.WriteLine("\t{");
                        writer.Write("\t\t\t::CLRUtil::ConstrainedVtableGlue< ");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine(" > bGlue;");
                        writer.Write("\t\t\tCLARITY_FORCEINLINE explicit ConstrainedInterfaceBindingGlue(");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine("* bPtr)");
                        writer.WriteLine("\t\t\t\t: bGlue(bPtr)");
                        writer.WriteLine("\t\t\t{");
                        writer.WriteLine("\t\t\t}");
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
                                WriteInterfaceBinding(cls, bimi.InterfaceSlot, bimi.ClassSlot, writer, mappingType, proto, shouldBeInline);
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

                                    WriteInterfaceBinding(cls, slot, vtSlot, writer, mappingType, proto, shouldBeInline);
                                }
                            }

                            // If there's no match, but this is a reimplementation, then use the old implementation
                            // Allows TestInheritedReimpl to pass.
                            if (!haveMatch && !isReimpl)
                                throw new ParseFailedException("Unmatched interface method");
                        }
                    }

                    if (mappingType == InterfaceConstraintMappingType.GenericConstraint)
                    {
                        writer.WriteLine("\t};");
                        writer.WriteLine("}");
                    }
                }

                if (boundImpls.Count > 0)
                    throw new NotSupportedException("Don't support non-interface override thunks yet");
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

        private void WriteVtableThunk(CppClass cls, CppMethod method, string methodName, CppVtableSlot slot, StreamWriter writer, VtableThunkMappingType mappingType, CppDependencySet depSet, bool proto, bool shouldBeInline)
        {
            if (!proto)
            {
                if (slot.IsGenericInterface == false && method.Abstract)
                    return;     // No code to export
            }

            if (!proto)
            {
                bool thunkShouldBeInline = (cls.NumGenericParameters > 0);
                if (thunkShouldBeInline != shouldBeInline)
                    return;
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
                {
                    writer.Write("\t\t");
                    if (shouldBeInline)
                        writer.Write("inline ");
                }
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
                depSet.AddTypeSpecDependencies(new CLRTypeSpecClass(cls.TypeDef), true);

                writer.Write("(");
                writer.Write(cls.GenerateCppClassName());
                WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                writer.Write("::");
            }
            writer.Write(vfuncName);
            if (proto)
                depSet.AddMethodSigDependencies(slotSig, CppDependencySet.LevelEnum.Proto);
            else
            {
                writer.Write(")");
                depSet.AddMethodSigDependencies(slotSig, CppDependencySet.LevelEnum.Def);
            }
            WriteMethodParameters(writer, null, null, slotSig, MethodParameterMapping.ClassImpl);

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

            depSet.AddTypeSpecDependencies(slot.Signature.RetType, !proto);

            if (!proto)
            {
                if (returnsAnything)
                    depSet.AddTypeSpecDependencies(slot.Signature.RetType, true);
                foreach (CLRMethodSignatureInstanceParam param in slot.Signature.ParamTypes)
                    depSet.AddTypeSpecDependencies(param.Type, true);

                if (!method.Abstract)
                {
                    writer.WriteLine("\t\t{");
                    writer.Write("\t\t\t");
                    if (returnsAnything)
                    {
                        writer.Write("return ");
                    }

                    if (mappingType == VtableThunkMappingType.ClassImpl)
                        writer.Write("this->mcall_i" + methodName + "(frame");
                    else if (mappingType == VtableThunkMappingType.StructGlue)
                        writer.Write("this->bGluePtr->mcall_i" + methodName + "(frame");
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
                WriteMethodParameters(writer, slot.DisambigSpec, null, slotSig, MethodParameterMapping.ClassImpl);
                writer.WriteLine(" CLARITY_PURE;");
            }
        }

        private void WriteVtableThunks(CppClass cls, StreamWriter writer, VtableThunkMappingType mappingType, CppDependencySet depSet, bool proto, bool shouldBeInline)
        {
            foreach (CppMethod method in cls.Methods)
            {
                string methodName = LegalizeName(method.Name, true);
                if (method.GenericTypeParamMangle != null)
                    methodName += method.GenericTypeParamMangle;

                bool isGenericInterface = (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface && cls.NumGenericParameters > 0);

                if (method.CreatesSlot != null)
                {
                    WriteVtableThunk(cls, method, methodName, method.CreatesSlot, writer, mappingType, depSet, proto, shouldBeInline);
                    if (mappingType == VtableThunkMappingType.ClassImpl)
                        WriteDelegateThunk(cls, method.CreatesSlot.GenerateName(), method.NumGenericParameters, method.CreatesSlot.Signature, method.Static, writer, depSet, proto, shouldBeInline);
                }
                else
                {
                    if (method.ReplacesStandardSlot != null)
                        WriteVtableThunk(cls, method, methodName, method.ReplacesStandardSlot, writer, mappingType, depSet, proto, shouldBeInline);
                }
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

        private void ExportBoxDef(CppClass cls, Stream outStream, string classCppName)
        {
            using (MemoryStream bodyMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(bodyMS, System.Text.Encoding.ASCII))
                {
                    CppDependencySet depSet = new CppDependencySet();

                    {
                        string parentType = "System.ValueType";
                        if (cls.IsEnum)
                            parentType = "System.Enum";
                        writer.Write("#include \"");
                        writer.Write(CppClass.GenerateDefinitionPathForFullName(parentType));
                        writer.WriteLine("\"");
                    }
                    writer.WriteLine();
                    writer.WriteLine("namespace CLRUtil");
                    writer.WriteLine("{");
                    writer.Write("\ttemplate< ");
                    WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                    writer.WriteLine(" >");

                    writer.Write("\tstruct Boxed< ");
                    writer.Write(classCppName);
                    WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                    writer.WriteLine(" >");
                    writer.WriteLine("\t: public " + SpecToClassName(cls.ParentTypeSpec));
                    foreach (CLRTypeSpec ifc in cls.NewlyImplementedInterfaces)
                        writer.WriteLine("\t, public " + SpecToClassName(ifc));
                    //AddTypeSpecDependencies(cls.ParentTypeSpec, depSet, true, true);
                    writer.WriteLine("\t{");
                    writer.Write("\t\t");

                    if (cls.NumGenericParameters > 0)
                        writer.Write("typename ");

                    writer.Write("::CLRVM::TValValue< ");
                    writer.Write(classCppName);
                    WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                    writer.WriteLine(" >::Type bValue;");
                    writer.WriteLine();

                    // Add shims to the underlying value
                    foreach (CppMethod method in cls.Methods)
                    {
                        if (method.Static)
                            continue;

                        if (method.Abstract)
                            throw new ArgumentException();

                        string methodName = method.GenerateCallName();

                        // Write new methods
                        writer.Write("\t\t");
                        writer.Write(SpecToValueType(method.MethodSignature.RetType));
                        writer.Write(" " + methodName);
                        WriteMethodParameters(writer, null, null, method.MethodSignature, MethodParameterMapping.ClassImpl);
                        writer.WriteLine(";");

                        WriteDelegateThunk(cls, methodName, method.NumGenericParameters, method.MethodSignature, method.Static, writer, depSet, true, true);
                    }

                    // CLARITYTODO: Overrides in value types are always final, but may not be emitted as such
                    WriteVtableThunks(cls, writer, VtableThunkMappingType.ClassImpl, depSet, true, true);
                    WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.ClassImpl, true, true);
                    writer.WriteLine("\t};");
                    writer.WriteLine("}");

                    // Write baseline vtable bindings
                    writer.WriteLine("namespace CLRUtil");
                    writer.WriteLine("{");
                    writer.Write("\ttemplate<");
                    WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                    writer.WriteLine(" >");

                    writer.Write("\tstruct ::CLRUtil::ConstrainedVtableGlue< ");
                    writer.Write(cls.GenerateCppClassName());
                    WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                    writer.WriteLine(" >");
                    writer.WriteLine("\t{");
                    writer.WriteLine("\tprivate:");
                    writer.Write("\t\t\t");
                    writer.Write(cls.GenerateCppClassName());
                    WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                    writer.WriteLine("* bGluePtr;");
                    writer.WriteLine("\tpublic:");
                    writer.Write("\t\t\tCLARITY_FORCEINLINE explicit ConstrainedVtableGlue(");
                    writer.Write(cls.GenerateCppClassName());
                    WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                    writer.WriteLine("* pPtr)");
                    writer.WriteLine("\t\t\t\t: bGluePtr(pPtr)");
                    writer.WriteLine("\t\t\t{");
                    writer.WriteLine("\t\t\t}");
                    WriteVtableThunks(cls, writer, VtableThunkMappingType.StructGlue, depSet, true, true);
                    writer.WriteLine("\t};");
                    writer.WriteLine("}");

                    // Write interface constraint mappings
                    writer.WriteLine("// interface constraint mappings");
                    WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.GenericConstraint, true, true);
                    writer.Flush();
                    bodyMS.WriteTo(outStream);
                }
            }
        }

        private void ExportBoxThunks(CppClass cls, Stream outStream, CppDependencySet depSet)
        {
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

                            depSet.AddMethodSigDependencies(method.MethodSignature, CppDependencySet.LevelEnum.Def);

                            string contentName = SpecToAmbiguousStorage(CreateInstanceTypeSpec(m_assemblies, cls.TypeDef));
                            writer.Write("\tinline ");
                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" ");
                            writer.Write("(::CLRUtil::Boxed< ");
                            writer.Write(contentName);
                            writer.Write(" >::mcall_");
                            writer.Write(methodName);
                            writer.Write(")");
                            WriteMethodParameters(writer, null, null, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            writer.WriteLine();
                            writer.WriteLine("\t{");
                            writer.Write("\t\t::CLRUtil::TAnchoredManagedPtr< ");
                            writer.Write(contentName);
                            writer.Write(" >::Type bUnboxedPtr = ::CLRUtil::Unbox< ");
                            writer.Write(contentName);
                            writer.WriteLine(" >(this);");
                            writer.Write("\t\t::CLRExec::UnboxThunkFrame< ");
                            writer.Write(cls.GenerateCppClassName());
                            writer.WriteLine(" > bUnboxFrame(frame, bUnboxedPtr);");

                            bool returnsAnything = true;
                            if (TypeSpecIsVoid(method.MethodSignature.RetType))
                                returnsAnything = false;

                            writer.Write("\t\t");
                            if (returnsAnything)
                                writer.Write("return ");
                            writer.Write(cls.GenerateCppClassName());
                            if (cls.NumGenericParameters > 0 || method.NumGenericParameters > 0)
                                throw new NotImplementedException();
                            writer.Write("::mcall_");
                            writer.Write(methodName);
                            writer.Write("(bUnboxFrame, bUnboxFrame.GetPassableThis()");
                            for (int i = 0; i < method.MethodSignature.ParamTypes.Length; i++)
                                writer.Write(", param" + i.ToString());
                            writer.WriteLine(");");

                            writer.WriteLine("\t}");
                        }
                    }

                    writer.Flush();
                    bodyMS.WriteTo(outStream);
                }
            }
        }

        private void ExportClassCode(CppClass cls, bool exportInline, Stream outStream)
        {
            CppDependencySet depSet = new CppDependencySet();

            string inlineCodePath = cls.GenerateInlineCodePath();

            bool staticNeedStaticInit;
            bool instanceNeedStaticInit;

            if (cls.TypeDef.IsBeforeFieldInit)
            {
                staticNeedStaticInit = instanceNeedStaticInit = false;
            }
            else
            {
                // For non-BFI classes, static constructor timing is dictated by I.8.9.5
                // Under those, any method calls (including constructors) triggers init
                staticNeedStaticInit = cls.HaveNewStaticFields;
                instanceNeedStaticInit = (staticNeedStaticInit && cls.IsValueType);
            }

            byte[] bodyContents;
            using (MemoryStream bodyMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(bodyMS, System.Text.Encoding.ASCII))
                {
                    if (cls.IsValueType && exportInline)
                        ExportBoxThunks(cls, bodyMS, depSet);

                    if (exportInline)
                    {
                        writer.WriteLine("// call to code init thunks");
                        foreach (CppMethod method in cls.Methods)
                        {
                            if (!method.Abstract)
                            {
                                bool needStaticInit = method.Static ? staticNeedStaticInit : instanceNeedStaticInit;

                                string methodName = method.GenerateBaseName();

                                depSet.AddMethodSigDependencies(method.MethodSignature, CppDependencySet.LevelEnum.Def);

                                writer.Write("\t");
                                writer.Write("CLARITY_FORCEINLINE ");
                                writer.Write(SpecToValueType(method.MethodSignature.RetType));
                                writer.Write(" (");
                                writer.Write(cls.GenerateCppClassName());
                                writer.Write("::mcall_");
                                writer.Write(methodName);
                                writer.Write(")");

                                CLRTypeSpec inlineThisType = null;
                                if (cls.IsValueType && !method.Static)
                                    inlineThisType = CreateInstanceTypeSpec(m_assemblies, cls.TypeDef);

                                WriteMethodParameters(writer, null, inlineThisType, method.MethodSignature, MethodParameterMapping.ClassImpl);
                                writer.WriteLine();
                                writer.WriteLine("\t{");

                                if (needStaticInit)
                                {
                                    CLRTypeSpec staticClassSpec = CreateInstanceTypeSpec(m_assemblies, cls.TypeDef);
                                    CppClass staticClass = cls;
                                    while (!staticClass.HaveNewStaticFields)
                                    {
                                        staticClassSpec = cls.ParentTypeSpec;
                                        staticClass = GetCachedClass(staticClassSpec);
                                    }

                                    // For the static constructor, only allocate, since InitStatics will call the cctor again
                                    if (method.Name == ".cctor")
                                        writer.Write("\t\t::CLRVM::AllocStatics< ");
                                    else
                                        writer.Write("\t\t::CLRVM::InitStatics< ");
                                    writer.Write(SpecToAmbiguousStorage(staticClassSpec));
                                    writer.WriteLine("\t\t >(frame);");
                                }

                                bool returnsAnything = true;
                                if (TypeSpecIsVoid(method.MethodSignature.RetType))
                                    returnsAnything = false;

                                writer.Write("\t\t");
                                if (returnsAnything)
                                    writer.Write("return ");

                                string methodType = SpecToAmbiguousStorage(CreateInstanceTypeSpec(m_assemblies, cls.TypeDef));
                                if (method.Static || cls.IsValueType)
                                {
                                    writer.Write(methodType);
                                    writer.Write("::");
                                }
                                else
                                    writer.Write("this->");
                                writer.Write("mcode_");
                                writer.Write(methodName);
                                writer.Write("(frame");
                                if (!method.Static)
                                {
                                    if (cls.IsValueType)
                                        writer.Write(", bThis0");
                                    else
                                    {
                                        writer.Write(", ::CLRVM::ParamThis< ");
                                        writer.Write(methodType);
                                        writer.Write(" >(this)");
                                    }
                                }
                                for (int i = 0; i < method.MethodSignature.ParamTypes.Length; i++)
                                    writer.Write(", param" + i.ToString());
                                writer.WriteLine(");");

                                writer.WriteLine("\t}");
                            }
                        }
                    }

                    if (!cls.IsValueType)
                    {
                        bool thunksShouldBeInline = (cls.NumGenericParameters > 0);

                        writer.WriteLine("// vtable thunks");
                        WriteVtableThunks(cls, writer, VtableThunkMappingType.ClassImpl, depSet, false, exportInline);
                        writer.WriteLine("// interface bindings");
                        WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.ClassImpl, false, exportInline);
                    }

                    // Export field visitor
                    bool visitorShouldBeInline = (cls.NumGenericParameters > 0);

                    if (visitorShouldBeInline == exportInline)
                    {
                        if (cls.IsValueType || (cls.TypeDef.Semantics != CLRTypeDefRow.TypeSemantics.Interface && ClassHasNewTraceableFields(cls)))
                        {
                            writer.WriteLine("// field visitor");

                            if (cls.NumGenericParameters > 0)
                            {
                                writer.Write("template< ");
                                WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                                writer.WriteLine(" >");
                            }

                            if (exportInline)
                                writer.Write("inline ");

                            writer.Write("void ");
                            writer.Write(cls.GenerateCppClassName());
                            WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                            writer.WriteLine("::VisitReferences(::CLRExec::IRefVisitor &visitor)");
                            writer.WriteLine("{");
                            if (!cls.IsValueType && cls.ParentTypeSpec != null)
                            {
                                writer.Write("\t");
                                writer.Write(SpecToAmbiguousStorage(cls.ParentTypeSpec));
                                WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                                writer.WriteLine("::VisitReferences(visitor);");
                            }

                            foreach (CppField fld in cls.Fields)
                            {
                                if (fld.Field.Static)
                                    continue;

                                writer.Write("\tthis->f");
                                writer.Write(LegalizeName(fld.Name, true));
                                writer.WriteLine(".VisitReferences(visitor);");
                            }
                            writer.WriteLine("}");
                        }

                        if (cls.HaveNewStaticFields)
                        {
                            writer.WriteLine("// static field visitor");

                            if (cls.NumGenericParameters > 0)
                            {
                                writer.Write("template< ");
                                WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                                writer.WriteLine(" >");
                            }

                            if (exportInline)
                                writer.Write("inline ");

                            writer.Write("void ");
                            writer.Write(cls.GenerateStaticCppClassName());
                            WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                            writer.WriteLine("::VisitReferences(::CLRExec::IRefVisitor &visitor)");
                            writer.WriteLine("{");

                            foreach (CppField fld in cls.Fields)
                            {
                                if (!fld.Field.Static || fld.Field.Literal)
                                    continue;

                                writer.Write("\tthis->f");
                                writer.Write(LegalizeName(fld.Name, true));
                                writer.WriteLine(".VisitReferences(visitor);");
                            }
                            writer.WriteLine("}");
                        }
                    }

                    bool classTokenImplShouldBeInline = (cls.NumGenericParameters > 0);

                    // Export class token
                    if (classTokenImplShouldBeInline == exportInline)
                    {
                        depSet.AddTypeSpecDependencies(CreateInstanceTypeSpec(m_assemblies, cls.TypeDef), true);

                        writer.WriteLine("// class token implementation");

                        if (cls.NumGenericParameters > 0)
                        {
                            writer.Write("template< ");
                            WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                            writer.WriteLine(" >");
                        }

                        writer.Write("CLRCore::ClassToken (");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine("::bClassToken);");
                    }

                    bool rttiQueryShouldBeInline = (cls.NumGenericParameters > 0);

                    if (rttiQueryShouldBeInline == exportInline)
                    {
                        writer.WriteLine("// rtti query implementation");
                        string instLocPrefix;
                        if (cls.NumGenericParameters > 0)
                        {
                            writer.Write("template< ");
                            WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                            writer.WriteLine(" >");
                            instLocPrefix = "\ttypeInfo.genericInstInfo.";
                        }
                        else
                            instLocPrefix = "\ttypeInfo.classInfo.";

                        writer.Write("void (");
                        writer.Write(cls.GenerateCppClassName());
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine("::RttiQuery)(::CLRCore::RttiTypeInfo &typeInfo)");
                        writer.WriteLine("{");

                        if (cls.NumGenericParameters > 0)
                        {
                            writer.WriteLine("\ttypeInfo.typeOfType = CLRCore::TOT_GenericInstantiation;");
                            writer.WriteLine("\ttypeInfo.genericInstInfo.baseType = bTGenericBase::RttiQuery;");
                            writer.Write("\ttypeInfo.genericInstInfo.genericParameters = ");
                            for (int i = 0; i < cls.NumGenericParameters; i++)
                            {
                                writer.Write("CLRCore::RttiTypeList< T");
                                writer.Write(i);
                                writer.Write(", ");
                            }
                            writer.Write("RttiTypeListEnd");
                            for (int i = 0; i < cls.NumGenericParameters; i++)
                                writer.Write(" >");
                            writer.WriteLine("::Query;");
                        }
                        else
                        {
                            int hash;
                            bool isPacked;
                            string encodedName;
                            string encodedNamespace;
                            CppRegionEmitter.GenerateCppStringConstant(cls.TypeDef.TypeName, out encodedName, out hash, out isPacked);
                            CppRegionEmitter.GenerateCppStringConstant(cls.TypeDef.TypeNamespace, out encodedNamespace, out hash, out isPacked);

                            writer.WriteLine("\ttypeInfo.typeOfType = CLRCore::TOT_Class;");
                            writer.Write("\ttypeInfo.classInfo.containingType = ");
                            if (cls.TypeDef.ContainerClass == null)
                                writer.WriteLine("CLARITY_NULLPTR;");
                            else
                            {
                                writer.Write(OpenGenericTypeName(cls.TypeDef.ContainerClass));
                                writer.Write("::RttiQuery;");
                            }

                            writer.Write("\ttypeInfo.classInfo.name = \"");
                            writer.Write(encodedName);
                            writer.WriteLine("\";");

                            writer.Write("\ttypeInfo.classInfo.ns = \"");
                            writer.Write(encodedNamespace);
                            writer.WriteLine("\";");
                        }

                        writer.WriteLine("\ttypeInfo.classToken = &bClassToken;");
                        writer.Write(instLocPrefix);
                        writer.Write("extends = ");

                        if (cls.ParentTypeSpec == null)
                            writer.Write("CLARITY_NULLPTR");
                        else
                        {
                            writer.Write(SpecToAmbiguousStorage(cls.ParentTypeSpec));
                            writer.Write("::RttiQuery");
                        }
                        writer.WriteLine(";");
                        writer.Write(instLocPrefix);
                        writer.Write("numNewInterfaces = ");
                        writer.Write(cls.NumNewlyImplementedInterfaces.ToString());
                        writer.WriteLine(";");

                        writer.Write(instLocPrefix);
                        writer.Write("newInterfaces = ");
                        if (cls.NumNewlyImplementedInterfaces > 0)
                            writer.WriteLine("RttiQueryInterfaces;");
                        else
                            writer.WriteLine("CLARITY_NULLPTR;");

                        writer.Write(instLocPrefix);
                        writer.Write("numGenericParameters = ");
                        writer.Write(cls.NumGenericParameters);
                        writer.WriteLine(";");

                        writer.WriteLine("}");

                        if (cls.NumNewlyImplementedInterfaces > 0)
                        {
                            if (cls.NumGenericParameters > 0)
                            {
                                writer.Write("template< ");
                                WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                                writer.WriteLine(" >");
                            }
                            writer.Write("void (");
                            writer.Write(cls.GenerateCppClassName());
                            WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                            writer.WriteLine("::RttiQueryInterfaces)(CLRTypes::U32 index, CLRCore::RttiInterfaceImplInfo &interfaceImplInfo)");
                            writer.WriteLine("{");
                            writer.WriteLine("\tswitch(index)");
                            writer.WriteLine("\t{");

                            int ifcNum = 0;
                            foreach (CLRTypeSpec ifc in cls.NewlyImplementedInterfaces)
                            {
                                writer.Write("\tcase ");
                                writer.Write(ifcNum);
                                writer.WriteLine(":");

                                writer.Write("\t\tinterfaceImplInfo.typeInfo = ");
                                writer.Write(SpecToAmbiguousStorage(ifc));
                                writer.WriteLine("::RttiQuery;");

                                writer.Write("\t\tinterfaceImplInfo.convertRefFunc = ");
                                if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Class)
                                    writer.Write("CLRCore::RttiC2IConverter< ");
                                else if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                                    writer.Write("CLRCore::RttiI2IConverter< ");
                                else
                                    throw new Exception("Unexpected type semantics");

                                writer.Write(SpecToAmbiguousStorage(CreateInstanceTypeSpec(m_assemblies, cls.TypeDef)));
                                writer.Write(", ");
                                writer.Write(SpecToAmbiguousStorage(ifc));
                                writer.WriteLine(" >::Convert;");
                                writer.WriteLine("\t\tbreak;");

                                ifcNum++;
                            }
                            writer.WriteLine("\t};");

                            writer.WriteLine("}");
                        }
                    }

                    // Export converted IL
                    foreach (CppMethod method in cls.Methods)
                    {
                        if (!method.Abstract && method.MethodDef.Method != null)
                        {
                            writer.Flush();
                            CppCilExporter.WriteMethodCode(this, cls, method, writer.BaseStream, depSet, exportInline);
                        }
                    }
                }
                bodyContents = bodyMS.ToArray();
            }

            using (MemoryStream tempMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(tempMS, System.Text.Encoding.ASCII))
                {
                    writer.WriteLine("#include \"ClarityCodeSupport.h\"");
                    depSet.WriteCodeDeps(writer);
                    writer.WriteLine();
                    writer.Flush();

                    tempMS.Write(bodyContents, 0, bodyContents.Length);

                    tempMS.WriteTo(outStream);
                }
            }
        }

        private string OpenGenericTypeName(CLRTypeDefRow typeDef)
        {
            CppClass cls = GetCachedClass(new CLRTypeSpecClass(typeDef));
            return cls.GenerateCppGenericBaseClassName();
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

        private void ExportClassStatics(CppClass cls)
        {
            if (!cls.HaveNewStaticFields)
                return;

            string staticsPath = cls.GenerateStaticDefPath();
            CreateDirectoriesForFile(m_exportPath + staticsPath);

            CppDependencySet depSet = new CppDependencySet();

            byte[] bodyContents;
            using (MemoryStream bodyMS = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(bodyMS, System.Text.Encoding.ASCII))
                {
                    string classCppName = cls.GenerateStaticCppClassName();

                    string[] path = classCppName.Split(new string[] { "::" }, StringSplitOptions.None);
                    for (int i = 1; i < path.Length - 1; i++)
                    {
                        writer.WriteLine("namespace " + path[i]);
                        writer.WriteLine("{");
                    }

                    writer.Write("struct ");
                    writer.Write(path[path.Length - 1]);
                    writer.WriteLine(" CLARITY_FINAL : public ::CLRCore::StaticFieldContainer");

                    writer.WriteLine("{");
                    writer.WriteLine("\t// root location reimplementation");
                    writer.WriteLine("\tvirtual void VisitReferences(::CLRExec::IRefVisitor &visitor) CLARITY_OVERRIDE;");
                    writer.WriteLine();

                    foreach (CppField field in cls.Fields)
                    {
                        if (field.Field.Static && !field.Field.Literal)
                        {
                            depSet.AddTypeSpecDependencies(field.Type, true);
                            writer.Write("\t::CLRVM::Field< ");
                            writer.Write(SpecToAmbiguousStorage(field.Type));
                            writer.Write(" > f");
                            writer.Write(LegalizeName(field.Name, true));
                            writer.WriteLine(";");
                        }
                    }
                    writer.WriteLine("};");

                    for (int i = 1; i < path.Length - 1; i++)
                        writer.WriteLine("}");

                    writer.Flush();

                    bodyContents = bodyMS.ToArray();
                }
            }


            CppMangleBuilder builder = new CppMangleBuilder();
            builder.Add(staticsPath);
            string headerId = builder.Finish();

            // Export def header
            using (FileStream fs = new FileStream(m_exportPath + staticsPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs, System.Text.Encoding.ASCII))
                {
                    writer.WriteLine("#pragma once");
                    writer.WriteLine("#ifndef __CLARITY_CPPEXPORT_STATICS_" + headerId + "__");
                    writer.WriteLine("#define __CLARITY_CPPEXPORT_STATICS_" + headerId + "__");
                    writer.WriteLine();

                    writer.WriteLine("#include \"ClarityCore.h\"");

                    depSet.WriteAll(writer);
                    writer.WriteLine();

                    writer.Flush();

                    fs.Write(bodyContents, 0, bodyContents.Length);

                    writer.WriteLine();
                    writer.WriteLine("#endif");
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
                        writer.WriteLine("\tstruct bGenericBase_" + clsName);
                        writer.WriteLine("\t{");
                        writer.WriteLine("\t\t// generic base rtti");
                        writer.WriteLine("\t\tstatic void RttiQuery(::CLRCore::RttiTypeInfo &typeInfo);");
                        if (cls.NumNewlyImplementedInterfaces > 0)
                            writer.WriteLine("\t\tstatic void RttiQueryInterfaces(CLRTypes::U32 index, CLRCore::RttiInterfaceImplInfo & interfaceImplInfo);");
                        writer.WriteLine("\t};");
                        writer.WriteLine();

                        writer.Write("\ttemplate< ");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                        writer.WriteLine("> ");
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
                        {
                            if (cls.FullName == "System.Object")
                                writer.WriteLine("\t\t: public ::CLRCore::GCObject");
                            else
                            {
                                if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                                    writer.WriteLine("\t\t: public ::CLRCore::InterfaceTarget");
                            }
                        }
                    }
                    writer.WriteLine("\t{");

                    if (cls.NumGenericParameters > 0)
                    {
                        writer.WriteLine("\t// generic base");
                        writer.WriteLine("\ttypedef bGenericBase_" + clsName + " bTGenericBase;");
                        writer.WriteLine();
                    }

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
                        WriteMethodParameters(writer, null, null, delegateSig, MethodParameterMapping.DelegateDef);
                        writer.WriteLine(";");
                        writer.WriteLine("\t\t::CLRUtil::SizeT bNumCallbacks;");
                        writer.WriteLine("\t\tconst ::CLRUtil::DelegateTargetCallbackPair<BFuncPtr> *bCallbackPairs;");
                        writer.WriteLine();
                    }

                    if (cls.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Class && (cls.TypeDef.Extends == null || cls.NumNewlyImplementedInterfaces > 0))
                    {
                        writer.WriteLine("\t\t// root location reimplementation");
                        writer.WriteLine("\t\tinline virtual ::CLRX::NtSystem::tObject *GetRootObject() CLARITY_OVERRIDE { return this; }");
                        writer.WriteLine("\t\tinline virtual ::CLRCore::GCObject *GetRootGCObject() CLARITY_OVERRIDE { return this; }");
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

                            depSet.AddMethodSigDependencies(method.MethodSignature, CppDependencySet.LevelEnum.Proto);

                            string methodBaseName = method.GenerateBaseName();

                            string methodName = "mcall_" + methodBaseName;

                            methodNames[method] = methodName;

                            // Write new methods
                            writer.Write("\t\t");
                            if (method.Static || cls.IsValueType)
                                writer.Write("static ");


                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" " + methodName);
                            {
                                CLRTypeSpec inlineThisType = null;
                                if (cls.IsValueType && !method.Static)
                                    inlineThisType = CreateInstanceTypeSpec(m_assemblies, cls.TypeDef);
                                WriteMethodParameters(writer, null, inlineThisType, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            }
                            writer.WriteLine(";");

                            writer.Write("\t\tstatic ");
                            writer.Write(SpecToValueType(method.MethodSignature.RetType));
                            writer.Write(" mcode_");
                            writer.Write(methodBaseName);
                            {
                                CLRTypeSpec inlineThisType = method.Static ? null : CppBuilder.CreateInstanceTypeSpec(m_assemblies, cls.TypeDef);
                                WriteMethodParameters(writer, null, inlineThisType, method.MethodSignature, MethodParameterMapping.ClassImpl);
                            }
                            writer.WriteLine(";");

                            if (!cls.IsValueType && method.Name != ".ctor")
                                WriteDelegateThunk(cls, methodName, method.NumGenericParameters, method.MethodSignature, method.Static, writer, depSet, true, true);
                        }

                        writer.WriteLine();

                        writer.WriteLine("\t\t// fields");
                        foreach (CppField field in cls.Fields)
                        {
                            CLRFieldRow fieldDef = field.Field;
                            if (fieldDef.Literal)
                            {
                                CLRConstantRow constant = fieldDef.AttachedConstants[0];
                            }
                            else if (fieldDef.Static)
                            {
                            }
                            else
                            {
                                writer.Write("\t\t::CLRVM::Field< ");
                                writer.Write(SpecToAmbiguousStorage(field.Type));
                                writer.Write(" > f");
                                writer.Write(LegalizeName(field.Name, true));
                                writer.WriteLine(";");

                                depSet.AddTypeSpecDependencies(field.Type, FieldNeedsDependencyDefs(field.Type));
                            }
                        }

                        if (!cls.IsValueType)
                        {
                            writer.WriteLine("\t\t// vtable thunks");
                            WriteVtableThunks(cls, writer, VtableThunkMappingType.ClassImpl, depSet, true, false);
                            writer.WriteLine("\t\t// interface bindings");
                            WriteInterfaceImplementations(cls, writer, InterfaceConstraintMappingType.ClassImpl, true, true);
                        }
                    }

                    writer.WriteLine();
                    writer.WriteLine("\t\t// ref visitor");
                    if (cls.IsValueType)
                    {
                        writer.WriteLine("\t\tvoid VisitReferences(::CLRExec::IRefVisitor &visitor);");
                    }
                    else
                    {
                        if (cls.TypeDef.Semantics != CLRTypeDefRow.TypeSemantics.Interface && ClassHasNewTraceableFields(cls))
                            writer.WriteLine("\t\tvirtual void VisitReferences(::CLRExec::IRefVisitor &visitor) CLARITY_OVERRIDE;");
                    }

                    writer.WriteLine();
                    writer.WriteLine("\t\t// class token");
                    writer.WriteLine("\t\tstatic CLRCore::ClassToken bClassToken;");

                    writer.WriteLine();
                    writer.WriteLine("\t\t// rtti query");
                    writer.WriteLine("\t\tstatic void RttiQuery(::CLRCore::RttiTypeInfo &typeInfo);");
                    if (cls.NumNewlyImplementedInterfaces > 0)
                        writer.WriteLine("\t\tstatic void RttiQueryInterfaces(CLRTypes::U32 index, CLRCore::RttiInterfaceImplInfo &interfaceImplInfo);");

                    writer.WriteLine("\t};");

                    for (int i = 1; i < path.Length - 1; i++)
                        writer.WriteLine("}");

                    writer.WriteLine("");

                    // For value types, now write the box specialization prototype
                    if (cls.IsValueType)
                    {
                        writer.WriteLine();
                        writer.WriteLine("namespace CLRUtil");
                        writer.WriteLine("{");
                        writer.Write("\ttemplate<");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                        writer.WriteLine(">");
                        writer.Write("\tstruct Boxed< ");
                        writer.Write(classCppName);
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                        writer.WriteLine(" >;");
                        writer.WriteLine("}");
                        writer.WriteLine();
                    }

                    // Write type traits
                    {
                        writer.WriteLine("namespace CLRTI");
                        writer.WriteLine("{");
                        writer.Write("\ttemplate< ");
                        WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);

                        writer.WriteLine(" >");
                        writer.Write("\tstruct TypeTraits< ");
                        writer.Write(classCppName);
                        WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);

                        writer.WriteLine(" >");
                        writer.WriteLine("\t{");

                        writer.WriteLine("\t\tenum");
                        writer.WriteLine("\t\t{");

                        if (cls.IsValueType)
                        {
                            CLRTypeSpec instanceSpec = CreateInstanceTypeSpec(m_assemblies, cls.TypeDef);
                            CppTraceabilityEnum traceability = GetCachedTraceability(instanceSpec);

                            if (traceability == CppTraceabilityEnum.NotTraced)
                                writer.WriteLine("\t\t\tIsValueTraceable = 0,");
                            else if (traceability == CppTraceabilityEnum.DefinitelyTraced)
                                writer.WriteLine("\t\t\tIsValueTraceable = 1,");
                            else if (traceability == CppTraceabilityEnum.MaybeTraced)
                            {
                                writer.WriteLine("\t\t\tIsValueTraceable = ((");
                                bool first = true;
                                foreach (CppField field in cls.Fields)
                                {
                                    if (field.Field.Static)
                                        continue;

                                    CppTraceabilityEnum fieldTraceability = GetCachedTraceability(field.Type);
                                    if (fieldTraceability == CppTraceabilityEnum.MaybeTraced)
                                    {
                                        writer.Write("\t\t\t\t");
                                        if (first)
                                            first = false;
                                        else
                                            writer.Write("|| ");
                                        writer.Write("(");
                                        writer.Write("::CLRTI::TypeTraits< ");
                                        writer.Write(SpecToAmbiguousStorage(field.Type));
                                        writer.WriteLine(" >::IsValueTraceable != 0)");
                                    }
                                }
                                writer.WriteLine("\t\t\t) ? 1 : 0),");
                            }
                        }
                        else
                            writer.WriteLine("\t\t\tIsValueTraceable = 1,");

                        writer.WriteLine("\t\t};");

                        if (cls.HaveNewStaticFields)
                        {
                            writer.Write("\t\t\ttypedef ");
                            writer.Write(cls.GenerateStaticCppClassName());
                            WriteTemplateParamCluster(true, cls.NumGenericParameters, "T", writer);
                            writer.WriteLine(" StaticType;");
                        }

                        writer.WriteLine("\t};");

                        // Write compatible ref assignments
                        List<CLRTypeSpec> compatibleSpecs = new List<CLRTypeSpec>();
                        CLRTypeSpec thisClassSpec = CreateInstanceTypeSpec(m_assemblies, cls.TypeDef);
                        CLRTypeSpec currentSpec = thisClassSpec;
                        while (currentSpec != null)
                        {
                            compatibleSpecs.Add(currentSpec);
                            currentSpec = GetCachedClass(currentSpec).ParentTypeSpec;
                        }

                        compatibleSpecs.AddRange(cls.NewlyImplementedInterfaces);
                        compatibleSpecs.AddRange(cls.InheritedImplementedInterfaces);

                        string thisClassSpecString = SpecToAmbiguousStorage(thisClassSpec);
                        foreach (CLRTypeSpec compatibleSpec in compatibleSpecs)
                        {
                            depSet.AddTypeSpecDependencies(compatibleSpec, false);
                            writer.Write("\ttemplate< ");
                            WriteTemplateParamCluster(false, cls.NumGenericParameters, "class T", writer);
                            writer.WriteLine(" >");
                            writer.Write("\tstruct TypeRefCompatibility< ");
                            writer.Write(thisClassSpecString);
                            writer.Write(", ");
                            writer.Write(SpecToAmbiguousStorage(compatibleSpec));
                            writer.WriteLine(" >");
                            writer.WriteLine("\t{");
                            writer.WriteLine("\t\tenum");
                            writer.WriteLine("\t\t{");
                            writer.WriteLine("\t\t\tIsRefAssignable = 1,");
                            writer.WriteLine("\t\t};");
                            writer.WriteLine("\t};");
                        }
                        writer.WriteLine("}");
                        writer.WriteLine();

                    }
                }

                bodyContents = bodyMS.ToArray();
            }

            // Add the class proto itself
            depSet.AddProtoDependency(cls.FullName);

            CppMangleBuilder builder = new CppMangleBuilder();
            builder.Add(protoPath);
            string headerId = builder.Finish();

            // Export def header
            using (FileStream fs = new FileStream(m_exportPath + defPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs, System.Text.Encoding.ASCII))
                {
                    writer.WriteLine("#pragma once");
                    writer.WriteLine("#ifndef __CLARITY_CPPEXPORT_DEF_" + headerId + "__");
                    writer.WriteLine("#define __CLARITY_CPPEXPORT_DEF_" + headerId + "__");
                    writer.WriteLine();
                    writer.WriteLine("#include \"ClarityCore.h\"");

                    depSet.WriteAll(writer);
                    writer.WriteLine();
                    writer.Flush();

                    fs.Write(bodyContents, 0, bodyContents.Length);

                    writer.WriteLine("");
                    writer.WriteLine("#endif");
                }
            }

            // Export main header
            using (FileStream fs = new FileStream(m_exportPath + cls.GenerateMainHeaderPath(), FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs, System.Text.Encoding.ASCII))
                {
                    writer.WriteLine("#pragma once");
                    writer.WriteLine("#ifndef __CLARITY_CPPEXPORT_INLINECODE_" + headerId + "__");
                    writer.WriteLine("#define __CLARITY_CPPEXPORT_INLINECODE_" + headerId + "__");
                    writer.WriteLine();

                    writer.Write("#include \"");
                    writer.Write(cls.GenerateDefinitionPath());
                    writer.WriteLine("\"");

                    if (cls.HaveNewStaticFields)
                    {
                        writer.Write("#include \"");
                        writer.Write(cls.GenerateStaticDefPath());
                        writer.WriteLine("\"");
                    }

                    writer.WriteLine();
                    writer.Flush();

                    // Export box definition
                    if (cls.IsValueType)
                        ExportBoxDef(cls, writer.BaseStream, cls.GenerateCppClassName());

                    writer.WriteLine();
                    writer.WriteLine("////////////////////////////////////////////////////////////////////////////////");
                    writer.WriteLine("// inline code");
                    writer.Flush();

                    ExportClassCode(cls, true, fs);
                    fs.Flush();

                    writer.WriteLine();
                    writer.WriteLine("#endif");
                }
            }

            // Export non-inline code
            using (FileStream fs = new FileStream(m_exportPath + cls.GenerateInstanceCodePath(), FileMode.Create))
            {
                ExportClassCode(cls, false, fs);
            }
        }


        private void ExportClassCodeFile(CppClass cls)
        {

        }

        public void ExportTypeDef(CLRTypeDefRow typeDef)
        {
            CppClass cls = GetCachedClass(new CLRTypeSpecClass(typeDef));
            ExportClassPrototypes(cls);
            ExportClassCodeFile(cls);
            ExportClassStatics(cls);
            ExportClassDefinitions(cls);
            ExportClassBox(cls);
            ExportClassStubs(cls);
        }

        public static string LegalizeName(string str, bool makeUnique)
        {
            bool hasIllegalChars = false;
            string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string result = "";
            foreach (char c in str)
            {
                if (validChars.IndexOf(c) < 0)
                {
                    if (c == '_')
                        result += "__";
                    else if (c == '`')
                        result += "_t_";
                    else if (c == '.')
                        result += "_d_";
                    else if (c == '<')
                        result += "_l_";
                    else if (c == '>')
                        result += "_g_";
                    else if (c == '{')
                        result += "_lc_";
                    else if (c == '}')
                        result += "_rc_";
                    else if (c == '-')
                        result += "_h_";
                    else if (c == '=')
                        result += "_e_";
                    else
                    {
                        result += "_";
                        hasIllegalChars = true;
                    }
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
