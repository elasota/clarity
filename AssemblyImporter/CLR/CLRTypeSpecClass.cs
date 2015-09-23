using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecClass : CLRTypeSpec, IEquatable<CLRTypeSpecClass>
    {
        public CLRTypeDefRow TypeDef { get; private set; }

        public CLRTypeSpecClass(CLRTypeDefRow typeDef)
        {
            TypeDef = typeDef;
        }

        public override int GetHashCode()
        {
            return TypeDef.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecClass) && this.Equals((CLRTypeSpecClass)obj);
        }

        public bool Equals(CLRTypeSpecClass other)
        {
            return other.TypeDef == this.TypeDef;
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
