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
        public MethodSignatureTag MethodSignature { get { return m_methodSignature; } }

        private HighLocal m_instanceLocal;
        private HighLocal[] m_args;
        private HighLocal[] m_locals;
        private TypeSpecTag m_returnType;
        private HighRegion m_entryRegion;
        private MethodInstantiationPath m_instantiationPath;
        private MethodSpecTag m_methodSpec;
        private MethodSignatureTag m_methodSignature;

        public RloMethodBody(HighLocal instanceLocal, HighLocal[] args, HighLocal[] locals, TypeSpecTag returnType, HighRegion entryRegion, MethodSignatureTag methodSignature, MethodInstantiationPath instPath)
        {
            m_instanceLocal = instanceLocal;
            m_args = args;
            m_locals = locals;
            m_returnType = returnType;
            m_entryRegion = entryRegion;
            m_instantiationPath = instPath;
            m_methodSignature = methodSignature;
        }

        public RloMethodBody(Compiler compiler, HighMethod method, MethodSpecTag methodSpec, TypeSpecClassTag thisType, bool isStruct, RloInstantiationParameters instParams, MethodInstantiationPath methodInstantiationPath)
        {
            m_instantiationPath = methodInstantiationPath;
            m_methodSpec = methodSpec;

            HighMethodBody methodBody = method.MethodBody;
            TagRepository tagRepo = compiler.TagRepository;

            // Validate locals
            uint numParamArgs = (uint)method.MethodSignature.ParamTypes.Length;

            if (method.IsStatic)
            {
                if (methodBody.InstanceLocal != null)
                    throw new Exception("Instance local in static method");
            }
            else
            {
                HighLocal thisLocal = methodBody.InstanceLocal;
                if (thisLocal == null)
                    throw new Exception("Missing instance local in instance method");

                HighLocal.ETypeOfType expectedTypeOfType = isStruct ? HighLocal.ETypeOfType.ByRef : HighLocal.ETypeOfType.Value;
                if (thisLocal.TypeOfType != expectedTypeOfType || thisLocal.Type.Instantiate(tagRepo, instParams.TypeParams, instParams.MethodParams) != thisType)
                    throw new Exception("Invalid this type for method");
            }

            if (numParamArgs != (uint)methodBody.Args.Length)
                throw new Exception("Mismatched argument count in method body and signature");

            for (uint i = 0; i < numParamArgs; i++)
            {
                HighLocal bodyArg = methodBody.Args[i];
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

            HighLocal instanceLocal = methodBody.InstanceLocal;

            RloMethodConverter methodConverter = new RloMethodConverter(compiler.TagRepository, instParams, method.MethodSignature.RetType, instanceLocal, methodBody.Args, methodBody.Locals);
            RloRegionConverter regionConverter = new RloRegionConverter(methodConverter, methodBody.MainRegion, true);

            m_locals = methodConverter.Locals2;
            m_args = methodConverter.Args;
            m_instanceLocal = methodConverter.InstanceLocal;
            m_returnType = methodConverter.ReturnType;
            m_entryRegion = new HighRegion(regionConverter.EntryNode);
            m_methodSignature = method.MethodSignature;

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
            if (m_instanceLocal == null)
                dw.WriteLine("static");
            else
            {
                dw.Write("instance ");
                m_instanceLocal.WriteDisassembly(dw);
            }
            dw.WriteLine("args {");
            dw.PushIndent();
            foreach (HighLocal local in m_args)
                local.WriteDisassembly(dw);
            dw.PopIndent();
            dw.WriteLine("}");
            dw.WriteLine("locals {");
            dw.PushIndent();
            foreach (HighLocal local in m_locals)
                local.WriteDisassembly(dw);
            dw.PopIndent();
            dw.WriteLine("}");
            dw.Write("returns ");
            m_returnType.WriteDisassembly(dw);
            dw.WriteLine("");

            CfgWriter cfgWriter = new CfgWriter(dw, m_instanceLocal, m_args, m_locals);

            cfgWriter.GetCfgNodeIndex(m_entryRegion.EntryNode.Value);
            cfgWriter.WriteGraph();
        }
    }
}
