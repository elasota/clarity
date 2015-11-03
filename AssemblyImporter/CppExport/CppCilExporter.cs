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
                CppClass cls = cppBuilder.GetCachedClass(typeSpec);
                if (cls.IsValueType)
                    return VType.ValTypeEnum.ValueValue;
                return VType.ValTypeEnum.NullableReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation giSpec = (CLRTypeSpecGenericInstantiation)typeSpec;
                CppClass cls = cppBuilder.GetCachedClass(new CLRTypeSpecClass(giSpec.GenericType.TypeDef));
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
                    return "::CLRVM::TValValue< " + builder.SpecToClassName(vt.TypeSpec) + " >";
                case VType.ValTypeEnum.NullableReferenceValue:
                case VType.ValTypeEnum.NotNullReferenceValue:
                case VType.ValTypeEnum.Null:
                    return "::CLRVM::TRefValue< " + builder.SpecToClassName(vt.TypeSpec) + " >";
                default:
                    throw new ArgumentException();
            }
        }

        public static void WriteMethodCode(CppBuilder builder, CppClass cls, CppMethod method, System.IO.Stream stream, CppDependencySet depSet, bool exportInline)
        {
            int nClassParameters = cls.NumGenericParameters;
            int nMethodParameters = method.NumGenericParameters;

            bool isMethodInline = (nClassParameters != 0 || nMethodParameters != 0);

            if (isMethodInline != exportInline)
                return;

            MemoryStream mainMethodStream = new MemoryStream();
            StreamWriter writer = new StreamWriter(mainMethodStream);

            if (nClassParameters != 0 || nMethodParameters != 0)
            {
                writer.Write("template< ");
                CppBuilder.WriteTemplateParamCluster(false, nClassParameters, "T", writer);
                if (nMethodParameters != 0)
                {
                    if (nClassParameters != 0)
                        writer.Write(", ");
                    CppBuilder.WriteTemplateParamCluster(false, nMethodParameters, "M", writer);
                }
                writer.WriteLine(" >");
            }

            writer.Write(builder.SpecToValueType(method.MethodSignature.RetType));
            writer.Write(" (");
            writer.Write(cls.GenerateCppClassName());
            CppBuilder.WriteTemplateParamCluster(true, nClassParameters, "T", writer);
            writer.Write("::");
            writer.Write(method.GenerateCodeName());
            CppBuilder.WriteTemplateParamCluster(true, nMethodParameters, "M", writer);
            writer.Write(")");

            CLRTypeSpec inlineThisType = method.Static ? null : CppBuilder.CreateInstanceTypeSpec(builder.Assemblies, cls.TypeDef);

            builder.WriteMethodParameters(writer, null, inlineThisType, method.MethodSignature, CppBuilder.MethodParameterMapping.ClassImpl);
            depSet.AddMethodSigDependencies(method.MethodSignature, CppDependencySet.LevelEnum.Def);
            writer.WriteLine();
            writer.WriteLine("{");
            writer.Flush();

            List<VReg> args = new List<VReg>();
            if (!method.Static)
            {
                CppClass thisClass = builder.GetCachedClass(method.DeclaredInClassSpec);
                CLRTypeSpec thisTypeSpec = method.DeclaredInClassSpec;
                VType vt = new VType(thisClass.IsValueType ? VType.ValTypeEnum.MaybeAnchoredManagedPtr : VType.ValTypeEnum.NotNullReferenceValue, thisTypeSpec);
                args.Add(new VReg(builder, "bThis", vt, args.Count, VReg.UsageEnum.Argument));
            }

            foreach (CLRMethodSignatureInstanceParam param in method.MethodSignature.ParamTypes)
            {
                CLRTypeSpec spec = param.Type;
                VType vt;
                switch (param.TypeOfType)
                {
                    case CLRSigParamOrRetType.TypeOfTypeEnum.ByRef:
                        vt = new VType(VType.ValTypeEnum.AnchoredManagedPtr, spec);
                        break;
                    case CLRSigParamOrRetType.TypeOfTypeEnum.Value:
                        vt = new VType(ValTypeForTypeSpec(builder, spec), spec);
                        break;
                    default:
                        throw new ArgumentException();
                }
                args.Add(new VReg(builder, "param", vt, args.Count, VReg.UsageEnum.Argument));
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
                            vreg = new VReg(builder, "bLocal", new VType(VType.ValTypeEnum.AnchoredManagedPtr, localTypeSpec), locals.Count, VReg.UsageEnum.Local);
                            break;
                        case CLRSigLocalVar.LocalVarKind.Default:
                            vreg = new VReg(builder, "bLocal", new VType(CppCilExporter.ValTypeForTypeSpec(builder, localTypeSpec), localTypeSpec), locals.Count, VReg.UsageEnum.Local);
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    locals.Add(vreg);
                }
            }

            Console.WriteLine("Method " + cls.FullName + "::" + method.Name);

            foreach (VReg vReg in locals)
            {
                vReg.Liven();
                depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, CppDependencySet.LevelEnum.Def);
            }
            foreach (VReg vReg in args)
            {
                vReg.Liven();
                depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, CppDependencySet.LevelEnum.Def);
            }

            depSet.AddTypeSpecDependencies(new CLRTypeSpecClass(cls.TypeDef), CppDependencySet.LevelEnum.Def);

            ExceptionHandlingRegion mainRegion = new ExceptionHandlingRegion(null, builder, method, 0, (uint)method.MethodDef.Method.Instructions.Length - 1, null);
            {
                CfgBuilder cfgBuilder = new CfgBuilder(mainRegion, builder, cls, method, args.ToArray(), locals.ToArray());
                mainRegion.RootCfgNode = cfgBuilder.RootNode;
            }

            writer.Flush();

            CppMidCompiler midCompiler = new CppMidCompiler(builder, cls, method, mainRegion, "bTLFrame", args.ToArray(), locals.ToArray());

            MemoryStream localClusterStream = new MemoryStream();
            midCompiler.EmitAll(depSet, localClusterStream, writer.BaseStream);

            writer.BaseStream.Flush();
            //MidCompile(builder, cls, method, mainRegion, args.ToArray(), locals.ToArray(), writer.BaseStream);

            writer.WriteLine("}");


            foreach (VReg vReg in locals)
            {
                if (!vReg.IsAlive || vReg.IsZombie)
                    throw new Exception("Internal error: local vreg was killed");
            }
            foreach (VReg vReg in args)
            {
                if (!vReg.IsAlive || vReg.IsZombie)
                    throw new Exception("Internal error: arg vreg was killed");
            }

            writer.Flush();

            localClusterStream.WriteTo(stream);
            mainMethodStream.WriteTo(stream);
        }
    }
}
