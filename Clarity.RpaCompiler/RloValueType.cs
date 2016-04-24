using System;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloValueType : RloType
    {
        private TypeSpecTag m_typeSpec;

        public override ETypeOfType TypeOfType { get { return ETypeOfType.Value; } }

        public RloValueType(Compiler compiler, TypeSpecTag type, RloInstantiationParameters instParams)
        {
            m_typeSpec = type.Instantiate(compiler.TagRepository, instParams.TypeParams, instParams.MethodParams);
        }

        public override bool Equals(RloType rloType)
        {
            RloValueType tOther = rloType as RloValueType;
            if (tOther == null)
                return false;

            return m_typeSpec.Equals(tOther.m_typeSpec);
        }

        public override int GetHashCode()
        {
            return 2400 + m_typeSpec.GetHashCode();
        }
    }
}
