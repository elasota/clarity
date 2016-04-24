using System;

namespace Clarity.RpaCompiler
{
    public class RloTypedRefType : RloType
    {
        public override ETypeOfType TypeOfType { get { return ETypeOfType.TypedRef; } }

        public override bool Equals(RloType rloType)
        {
            RloTypedRefType tOther = rloType as RloTypedRefType;
            if (tOther == null)
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return 3000;
        }
    }
}
