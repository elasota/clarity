using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloMethodBody
    {
        public HighRegion EntryRegion { get { return m_entryRegion; } }
        public HighLocal[] Locals { get { return m_locals; } }
        public TypeSpecTag ReturnType { get { return m_returnType; } }
        public MethodInstantiationPath InstantiationPath { get { return m_instantiationPath; } }
        public MethodSpecTag MethodSpec { get { return m_methodSpec; } }

        private HighLocal[] m_locals;
        private TypeSpecTag m_returnType;
        private HighRegion m_entryRegion;
        private MethodInstantiationPath m_instantiationPath;
        private MethodSpecTag m_methodSpec;

        public RloMethodBody(HighLocal[] locals, TypeSpecTag returnType, HighRegion entryRegion, MethodInstantiationPath instPath)
        {
            m_locals = locals;
            m_returnType = returnType;
            m_entryRegion = entryRegion;
            m_instantiationPath = instPath;
        }

        public RloMethodBody(Compiler compiler, HighMethod method, MethodSpecTag methodSpec, TypeSpecClassTag thisType, bool isStruct, RloInstantiationParameters instParams, MethodInstantiationPath methodInstantiationPath)
        {
            m_instantiationPath = methodInstantiationPath;
            m_methodSpec = methodSpec;

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

            RloMethodConverter methodConverter = new RloMethodConverter(compiler.TagRepository, instParams, method.MethodSignature.RetType, locals);
            RloRegionConverter regionConverter = new RloRegionConverter(methodConverter, methodBody.MainRegion, true);

            m_locals = methodConverter.Locals;
            m_returnType = methodConverter.ReturnType;
            m_entryRegion = new HighRegion(regionConverter.EntryNode);

            RloFindPredecessorsAndSuccessorsPass psPass = new RloFindPredecessorsAndSuccessorsPass(compiler, this);
            psPass.Run();

            RloCanonicalizeSsaTypesPass cstPass = new RloCanonicalizeSsaTypesPass(compiler, this);
            cstPass.Run();

            RloInitPass initPass = new RloInitPass(compiler, this, psPass);
            initPass.Run();

            RloInitExceptionsPass exceptionInitPass = new RloInitExceptionsPass(compiler, this);
            exceptionInitPass.Run();
        }

        public void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.WriteLine("locals {");
            dw.PushIndent();
            foreach (HighLocal local in m_locals)
                local.WriteDisassembly(dw);
            dw.PopIndent();
            dw.WriteLine("}");
            dw.Write("returns ");
            m_returnType.WriteDisassembly(dw);
            dw.WriteLine("");

            CfgWriter cfgWriter = new CfgWriter(dw, m_locals);

            cfgWriter.GetCfgNodeIndex(m_entryRegion.EntryNode.Value);
            cfgWriter.WriteGraph();
        }
    }
}
