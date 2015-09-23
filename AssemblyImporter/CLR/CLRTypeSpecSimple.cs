using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecSimple : CLRTypeSpec, IEquatable<CLRTypeSpecSimple>
    {
        public CLRSigType.ElementType BasicType { get; private set; }

        public CLRTypeSpecSimple(CLRSigType.ElementType basicType)
        {
            BasicType = basicType;
        }

        public override int GetHashCode()
        {
            return BasicType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecSimple) && this.Equals((CLRTypeSpecSimple)obj);
        }

        public bool Equals(CLRTypeSpecSimple other)
        {
            return other.BasicType == this.BasicType;
        }

        public override CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return this;
        }

        public override bool UsesGenericParamOfType(CLRSigType.ElementType elementType)
        {
            return false;
        }
    }
}
