using System;
using System.Collections.Generic;
using Clarity.Rpa;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class RpaTagFactory
    {
        public static MethodSignatureTag CreateMethodSignature(CLRMethodSignatureInstance methodSignature)
        {
            List<MethodSignatureParam> paramTags = new List<MethodSignatureParam>();

            foreach (CLRMethodSignatureInstanceParam p in methodSignature.ParamTypes)
                paramTags.Add(CreateMethodSignatureParam(p));

            return new MethodSignatureTag(methodSignature.HasThis,
                methodSignature.ExplicitThis,
                methodSignature.NumGenericParameters,
                CreateTypeTag(methodSignature.RetType),
                paramTags.ToArray());
        }

        public static MethodSignatureParam CreateMethodSignatureParam(CLRMethodSignatureInstanceParam param)
        {
            MethodSignatureParamTypeOfType.Values v;
            switch (param.TypeOfType)
            {
                case CLRSigParamOrRetType.TypeOfTypeEnum.ByRef:
                    v = MethodSignatureParamTypeOfType.Values.ByRef;
                    break;
                case CLRSigParamOrRetType.TypeOfTypeEnum.TypedByRef:
                    v = MethodSignatureParamTypeOfType.Values.TypedByRef;
                    break;
                case CLRSigParamOrRetType.TypeOfTypeEnum.Value:
                    v = MethodSignatureParamTypeOfType.Values.Value;
                    break;
                default:
                    throw new Exception("Unsupported method parameter type of type");
            }
            return new MethodSignatureParam(CreateTypeTag(param.Type), new MethodSignatureParamTypeOfType(v));
        }

        public static TypeNameTag CreateTypeNameTag(CLRTypeDefRow typeDef)
        {
            TypeNameTag containerTag = null;
            if (typeDef.ContainerClass != null)
                containerTag = CreateTypeNameTag(typeDef.ContainerClass);

            CLRAssemblyRow assemblyRow = (CLRAssemblyRow)typeDef.Table.MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.Assembly).GetRow(0);

            return new TypeNameTag(assemblyRow.Name, typeDef.TypeNamespace, typeDef.TypeName, containerTag);
        }

        public static TypeSpecTag CreateTypeTag(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecClass)
            {
                CLRTypeSpecClass classSpec = (CLRTypeSpecClass)typeSpec;
                return new TypeSpecClassTag(CreateTypeNameTag(classSpec.TypeDef), new TypeSpecTag[0]);
            }
            else if (typeSpec is CLRTypeSpecComplexArray)
            {
                CLRTypeSpecComplexArray complexArraySpec = (CLRTypeSpecComplexArray)typeSpec;
                return new TypeSpecArrayTag(complexArraySpec.Rank, CreateTypeTag(complexArraySpec.SubType));
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation genericInstTag = (CLRTypeSpecGenericInstantiation)typeSpec;
                List<TypeSpecTag> argTypes = new List<TypeSpecTag>();
                foreach (CLRTypeSpec argType in genericInstTag.ArgTypes)
                    argTypes.Add(CreateTypeTag(argType));
                return new TypeSpecClassTag(CreateTypeNameTag(genericInstTag.GenericType.TypeDef), argTypes.ToArray());
            }
            else if (typeSpec is CLRTypeSpecSZArray)
            {
                CLRTypeSpecSZArray szArraySpec = (CLRTypeSpecSZArray)typeSpec;
                return new TypeSpecArrayTag(1, CreateTypeTag(szArraySpec.SubType));
            }
            else if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                CLRTypeSpecVarOrMVar varOrMVarSpec = (CLRTypeSpecVarOrMVar)typeSpec;
                TypeSpecGenericParamTypeTag v;
                switch (varOrMVarSpec.ElementType)
                {
                    case CLRSigType.ElementType.VAR:
                        v = new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.Var);
                        break;
                    case CLRSigType.ElementType.MVAR:
                        v = new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.MVar);
                        break;
                    default:
                        throw new Exception("Unrecognized VarOrMVar type");
                }
                return new TypeSpecGenericParamTag(v, varOrMVarSpec.Value);
            }
            else if (typeSpec is CLRTypeSpecVoid)
            {
                return new TypeSpecVoidTag();
            }
            else
                throw new Exception("Unrecognized type spec");
        }

        public static string CreateName(string name)
        {
            return name;
        }

        public static MethodSpecTag CreateMethodSpec(Clarity.Rpa.MethodSlotType slotType, CppMethodSpec methodSpec)
        {
            if ((slotType == MethodSlotType.Static) != methodSpec.CppMethod.Static)
                throw new ArgumentException("Static flag mismatch");

            TypeSpecTag[] genericTypes = null;

            List<TypeSpecTag> genericTypesList = new List<TypeSpecTag>();

            if (methodSpec.GenericParameters != null)
                foreach (CLRTypeSpec type in methodSpec.GenericParameters)
                    genericTypesList.Add(CreateTypeTag(type));

            genericTypes = genericTypesList.ToArray();

            return new MethodSpecTag(slotType,
                genericTypes,
                (TypeSpecClassTag)CreateTypeTag(methodSpec.CppMethod.DeclaredInClassSpec),
                methodSpec.CppMethod.VtableSlotTag
                );
        }
    }
}
