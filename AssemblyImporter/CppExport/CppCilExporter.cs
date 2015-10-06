using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;


namespace AssemblyImporter.CppExport
{
    public class CppCilExporter
    {
        public static VType.ValTypeEnum ValTypeForTypeSpec(CppBuilder cppBuilder, CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecClass)
            {
                CLRTypeSpecClass classSpec = (CLRTypeSpecClass)typeSpec;
                CppClass cls = cppBuilder.GetCachedClass(classSpec.TypeDef);
                if (cls.IsValueType)
                    return VType.ValTypeEnum.ValueValue;
                return VType.ValTypeEnum.NullableReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation giSpec = (CLRTypeSpecGenericInstantiation)typeSpec;
                CppClass cls = cppBuilder.GetCachedClass(giSpec.GenericType.TypeDef);
                if (cls.IsValueType)
                    return VType.ValTypeEnum.ValueValue;
                return VType.ValTypeEnum.NullableReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecSZArray)
            {
                return VType.ValTypeEnum.NullableReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecComplexArray)
            {
                return VType.ValTypeEnum.NullableReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                // Generic parameters are always treated like value types even if they're provably ref types
                return VType.ValTypeEnum.ValueValue;
            }
            else
                throw new ArgumentException();
        }

        private static string EvalVarStorageType(CppBuilder builder, VType vt)
        {
            switch (vt.ValType)
            {
                case VType.ValTypeEnum.ValueValue:
                    return "::CLRVM::ValValue< " + builder.SpecToClassName(vt.TypeSpec) + " >";
                case VType.ValTypeEnum.NullableReferenceValue:
                case VType.ValTypeEnum.NotNullReferenceValue:
                case VType.ValTypeEnum.Null:
                    return "::CLRVM::RefValue< " + builder.SpecToClassName(vt.TypeSpec) + " >";
                default:
                    throw new ArgumentException();
            }
        }

        public static void WriteMethodCode(CppBuilder builder, CppClass cls, CppMethod method, System.IO.StreamWriter writer, CppDependencySet depSet, bool exportInline)
        {
            bool isMethodInline = false;
            //RegisterSet regSet = new RegisterSet("bVReg");

            if (isMethodInline != exportInline)
                return;

            writer.Write(builder.SpecToValueType(method.MethodSignature.RetType));
            writer.Write(" (");
            writer.Write(cls.GenerateCppClassName());
            writer.Write("::mcode_");
            writer.Write(method.GenerateBaseName());
            writer.Write(")");
            builder.WriteMethodParameters(writer, null, method.MethodSignature, CppBuilder.MethodParameterMapping.ClassImpl);
            depSet.AddMethodSigDependencies(method.MethodSignature, true);
            writer.WriteLine();
            writer.WriteLine("{");
            writer.Flush();

            MemoryStream localVarsStream = new MemoryStream();
            MemoryStream instructionsStream = new MemoryStream();

            StreamWriter localVarsWriter = new StreamWriter(localVarsStream);
            StreamWriter instructionsWriter = new StreamWriter(instructionsStream);

            List<VReg> args = new List<VReg>();
            if (!method.Static)
            {
                CppClass thisClass = builder.GetCachedClass(method.DeclaredInClassSpec);
                CLRTypeSpec thisTypeSpec = method.DeclaredInClassSpec;
                VType vt = new VType(thisClass.IsValueType ? VType.ValTypeEnum.ManagedPtr : VType.ValTypeEnum.NotNullReferenceValue, thisTypeSpec);
                args.Add(new VReg("bThis", vt, args.Count));
            }

            foreach (CLRMethodSignatureInstanceParam param in method.MethodSignature.ParamTypes)
            {
                CLRTypeSpec spec = param.Type;
                VType vt;
                switch (param.TypeOfType)
                {
                    case CLRSigParamOrRetType.TypeOfTypeEnum.ByRef:
                        vt = new VType(VType.ValTypeEnum.ManagedPtr, spec);
                        break;
                    case CLRSigParamOrRetType.TypeOfTypeEnum.Value:
                        vt = new VType(ValTypeForTypeSpec(builder, spec), spec);
                        break;
                    default:
                        throw new ArgumentException();
                }
                args.Add(new VReg("param", vt, args.Count));
            }

            List<VReg> locals = new List<VReg>();

            CLRSigLocalVarSig localVarSig = method.MethodDef.Method.LocalVarSig;
            if (localVarSig != null)
            {
                foreach (CLRSigLocalVar localVar in localVarSig.LocalVars)
                {
                    if (localVar.Constraints != null && localVar.Constraints.Length > 0)
                        throw new NotSupportedException("Local var constraints are not supported");
                    if (localVar.CustomMods != null && localVar.CustomMods.Length > 0)
                        throw new NotSupportedException("Local var custom mods are not supported");

                    CLRTypeSpec localTypeSpec = builder.Assemblies.InternVagueType(localVar.Type);

                    VReg vreg = null;
                    switch (localVar.VarKind)
                    {
                        case CLRSigLocalVar.LocalVarKind.ByRef:
                            vreg = new VReg("local", new VType(VType.ValTypeEnum.ManagedPtr, localTypeSpec), locals.Count);
                            break;
                        case CLRSigLocalVar.LocalVarKind.Default:
                            vreg = new VReg("local", new VType(CppCilExporter.ValTypeForTypeSpec(builder, localTypeSpec), localTypeSpec), locals.Count);
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    locals.Add(vreg);
                }
            }

            Console.WriteLine("Method " + method.Name);

            CfgBuilder cfgBuilder = new CfgBuilder(builder, cls, method, args.ToArray(), locals.ToArray());
            //CfgNode entryPointNode = cfgBuilder.BuildFromInstr(0);

            //MidCompile(builder, cls, method, args.ToArray(), locals.ToArray());

            localVarsWriter.Flush();
            instructionsWriter.Flush();

            byte[] localVarsBytes = localVarsStream.ToArray();
            byte[] instructionsBytes = instructionsStream.ToArray();
            writer.BaseStream.Write(localVarsBytes, 0, localVarsBytes.Length);
            writer.BaseStream.Write(instructionsBytes, 0, instructionsBytes.Length);

            writer.WriteLine("}");
        }
    }
}
