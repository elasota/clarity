using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloMethod
    {
        private bool m_isInternal;
        private RloMethodBody m_methodBody;

        public RloMethod(RloMethodBody methodBody)
        {
            m_methodBody = methodBody;
        }

        public RloMethod(Compiler compiler, MethodSpecTag methodSpec, MethodInstantiationPath instantiationPath)
        {
            CliClass cls = compiler.GetClosedClass(methodSpec.DeclaringClass);
            HighMethod method;
            {
                uint methodSlot;
                if (!cls.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodSlot))
                    throw new Exception("Couldn't find method");
                method = cls.Methods[methodSlot];
            }

            if (method.IsInternal)
            {
                m_isInternal = true;
                return;
            }

            TypeSpecTag[] typeParams = methodSpec.DeclaringClass.ArgTypes;
            TypeSpecTag[] methodParams = methodSpec.GenericParameters;

            if ((uint)methodParams.Length != method.MethodSignature.NumGenericParameters)
                throw new Exception("Method generic parameter count doesn't match argument count");

            RloInstantiationParameters instParams = new RloInstantiationParameters(typeParams, methodParams);
            m_methodBody = new RloMethodBody(compiler, method, methodSpec, methodSpec.DeclaringClass, cls.IsStruct, instParams, instantiationPath);
        }

        public void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.PushIndent();
            if (m_isInternal)
                dw.WriteLine("internal");
            else
            {
                dw.WriteLine("rlo");
                m_methodBody.WriteDisassembly(dw);
            }
            dw.PopIndent();
        }
    }
}
