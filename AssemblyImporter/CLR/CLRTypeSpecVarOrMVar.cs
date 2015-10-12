using System;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecVarOrMVar : CLRTypeSpec, IEquatable<CLRTypeSpecVarOrMVar>
    {
        public CLRSigType.ElementType ElementType { get; private set; }
        public uint Value { get; private set; }

        public CLRTypeSpecVarOrMVar(CLRSigType.ElementType elementType, uint value)
        {
            ElementType = elementType;
            Value = value;
        }

        public override int GetHashCode()
        {
            return ElementType.GetHashCode() + Value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecVarOrMVar) && this.Equals((CLRTypeSpecVarOrMVar)obj);
        }

        public bool Equals(CLRTypeSpecVarOrMVar other)
        {
            return other.ElementType == this.ElementType &&
                other.Value == this.Value;
        }

        public override CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            if (ElementType == CLRSigType.ElementType.MVAR)
            {
                if (methodParams == null)
                    return this;
                return methodParams[this.Value];
            }

            if (ElementType == CLRSigType.ElementType.VAR)
            {
                if (typeParams == null)
                    return this;
                return typeParams[this.Value];
            }

            throw new ParseFailedException("Strange instantiation");
        }

        public override bool UsesGenericParamOfType(CLRSigType.ElementType elementType)
        {
            return this.ElementType == elementType;
        }

        public override string ToString()
        {
            if (this.ElementType == CLRSigType.ElementType.VAR)
                return "T" + Value.ToString();
            else if (this.ElementType == CLRSigType.ElementType.MVAR)
                return "M" + Value.ToString();
            else
                throw new ArgumentException();
        }
    }
}
