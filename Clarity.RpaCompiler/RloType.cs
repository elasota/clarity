using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.RpaCompiler
{
    public abstract class RloType : IEquatable<RloType>
    {
        public enum ETypeOfType
        {
            Value,
            Ref,
            TypedRef
        }

        public abstract override int GetHashCode();
        public abstract bool Equals(RloType rloType);
        public abstract ETypeOfType TypeOfType { get; }

        public override bool Equals(object obj)
        {
            RloType typedObj = obj as RloType;
            if (obj == null)
                return false;

            return this.Equals(typedObj);
        }
    }
}
