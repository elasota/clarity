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
                return VType.ValTypeEnum.ReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation giSpec = (CLRTypeSpecGenericInstantiation)typeSpec;
                CppClass cls = cppBuilder.GetCachedClass(new CLRTypeSpecClass(giSpec.GenericType.TypeDef));
                if (cls.IsValueType)
                    return VType.ValTypeEnum.ValueValue;
                return VType.ValTypeEnum.ReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecSZArray)
            {
                return VType.ValTypeEnum.ReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecComplexArray)
            {
                return VType.ValTypeEnum.ReferenceValue;
            }
            else if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                // Generic parameters are always treated like value types even if they're provably ref types
                return VType.ValTypeEnum.ValueValue;
            }
            else
                throw new ArgumentException();
        }

        public static void WriteMethodCode(Clarity.Rpa.HighFileBuilder fileBuilder, BinaryWriter writer, CppBuilder builder, CppClass cls, CppMethod method)
        {
            List<VReg> args = new List<VReg>();
            if (!method.Static)
            {
                CppClass thisClass = builder.GetCachedClass(method.DeclaredInClassSpec);
                CLRTypeSpec thisTypeSpec = method.DeclaredInClassSpec;
                VType vt = new VType(thisClass.IsValueType ? VType.ValTypeEnum.ManagedPtr : VType.ValTypeEnum.ReferenceValue, thisTypeSpec);
                args.Add(new VReg(builder, "bThis", vt, args.Count, VReg.UsageEnum.Argument));
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
                args.Add(new VReg(builder, "bParam", vt, args.Count, VReg.UsageEnum.Argument));
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
                            vreg = new VReg(builder, "bLocal", new VType(VType.ValTypeEnum.ManagedPtr, localTypeSpec), locals.Count, VReg.UsageEnum.Local);
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

            foreach (VReg vReg in locals)
                vReg.Liven();
            foreach (VReg vReg in args)
                vReg.Liven();

            List<VReg> temporaries = new List<VReg>();

            ExceptionHandlingRegion mainRegion = new ExceptionHandlingRegion(null, builder, method, 0, (uint)method.MethodDef.Method.Instructions.Length - 1, null);
            {
                CfgBuilder cfgBuilder = new CfgBuilder(mainRegion, builder, cls, method, args.ToArray(), locals.ToArray(), temporaries);
                mainRegion.RootCfgNode = cfgBuilder.RootNode;
            }

            CppMidCompiler midCompiler = new CppMidCompiler(builder, cls, method, mainRegion, "bTLFrame", args.ToArray(), locals.ToArray(), temporaries.ToArray());

            midCompiler.EmitAll(fileBuilder, writer);

            //MidCompile(builder, cls, method, mainRegion, args.ToArray(), locals.ToArray(), writer.BaseStream);

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
        }
    }
}
