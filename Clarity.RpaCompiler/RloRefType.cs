using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.RpaCompiler
{
    public class RloRefType : RloType
    {
        private RloValueType m_subType;

        public override ETypeOfType TypeOfType { get { return ETypeOfType.Ref; } }

        public RloRefType(RloValueType vt)
        {
            m_subType = vt;
        }

        public override bool Equals(RloType rloType)
        {
            RloRefType tOther = rloType as RloRefType;
            if (tOther == null)
                return false;

            return m_subType == tOther.m_subType; 
        }

        public override int GetHashCode()
        {
            return 1000 + m_subType.GetHashCode();
        }
    }
}
