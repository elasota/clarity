using System;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class MethodSpecMethodKey : MethodKey
    {
        private MethodSpecTag m_methodSpec;

        public MethodSpecMethodKey(MethodSpecTag methodSpec)
        {
            m_methodSpec = methodSpec;
        }

        public override bool Equals(MethodKey other)
        {
            MethodSpecMethodKey tOther = other as MethodSpecMethodKey;
            if (tOther == null)
                return false;
            return m_methodSpec == tOther.m_methodSpec;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            return new RloMethod(compiler, m_methodSpec, instantiationPath);
        }

        public override int GetHashCode()
        {
            return m_methodSpec.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("methodSpecKey(");
            m_methodSpec.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
