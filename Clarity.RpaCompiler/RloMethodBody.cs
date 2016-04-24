using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloMethodBody
    {
        private HighLocal[] m_locals;
        private HighCfgNodeHandle m_entryNode;

        public HighCfgNodeHandle EntryNode { get { return m_entryNode; } }
        public HighLocal[] Locals { get { return m_locals; } }

        public RloMethodBody(Compiler compiler, HighMethod method, TypeSpecClassTag thisType, bool isStruct, RloInstantiationParameters instParams)
        {
            HighMethodBody methodBody = method.MethodBody;
            TagRepository tagRepo = compiler.TagRepository;

            // Validate locals
            uint numParamArgs = (uint)method.MethodSignature.ParamTypes.Length;
            uint firstSigArg;

            if (method.IsStatic)
                firstSigArg = 0;
            else
            {
                if (methodBody.Args.Length == 0)
                    throw new Exception("Empty args list in instance method");
                HighLocal thisLocal = methodBody.Args[0];

                HighLocal.ETypeOfType expectedTypeOfType = isStruct ? HighLocal.ETypeOfType.ByRef : HighLocal.ETypeOfType.Value;
                if (thisLocal.TypeOfType != expectedTypeOfType || thisLocal.Type.Instantiate(tagRepo, instParams.TypeParams, instParams.MethodParams) != thisType)
                    throw new Exception("Invalid this type for method");
                firstSigArg = 1;
            }

            if (numParamArgs != (uint)methodBody.Args.Length - firstSigArg)
                throw new Exception("Mismatched argument count in method body and signature");

            for (uint i = 0; i < numParamArgs; i++)
            {
                HighLocal bodyArg = methodBody.Args[firstSigArg + i];
                MethodSignatureParam methodSigParam = method.MethodSignature.ParamTypes[i];
                MethodSignatureParamTypeOfType tot = methodSigParam.TypeOfType;

                HighLocal.ETypeOfType expectedTypeOfType;
                switch (tot.Value)
                {
                    case MethodSignatureParamTypeOfType.Values.ByRef:
                        expectedTypeOfType = HighLocal.ETypeOfType.ByRef;
                        break;
                    case MethodSignatureParamTypeOfType.Values.TypedByRef:
                        expectedTypeOfType = HighLocal.ETypeOfType.TypedByRef;
                        break;
                    case MethodSignatureParamTypeOfType.Values.Value:
                        expectedTypeOfType = HighLocal.ETypeOfType.Value;
                        break;
                    default:
                        throw new ArgumentException();
                }

                if (bodyArg.TypeOfType != expectedTypeOfType)
                    throw new Exception("Method body arg doesn't match signature");
            }

            List<HighLocal> mergedLocals = new List<HighLocal>();
            mergedLocals.AddRange(methodBody.Args);
            mergedLocals.AddRange(methodBody.Locals);

            HighLocal[] locals = mergedLocals.ToArray();

            RloMethodConverter methodConverter = new RloMethodConverter(compiler.TagRepository, instParams, locals);
            RloRegionConverter regionConverter = new RloRegionConverter(methodConverter, methodBody.MainRegion, true);

            m_locals = locals;
            m_entryNode = regionConverter.EntryNode;

            RloFindPredecessorsAndSuccessorsPass psPass = new RloFindPredecessorsAndSuccessorsPass(compiler, this);
            psPass.Run();

            RloCanonicalizeSsaTypesPass cstPass = new RloCanonicalizeSsaTypesPass(compiler, this);
            cstPass.Run();

            RloInitPass initPass = new RloInitPass(compiler, this, psPass);
            initPass.Run();
        }
    }
}
