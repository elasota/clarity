using System;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecSZArray : CLRTypeSpec, IEquatable<CLRTypeSpecSZArray>
    {
        public CLRTypeSpec SubType { get; private set; }

        public CLRTypeSpecSZArray(CLRTypeSpec subType)
        {
            SubType = subType;
        }

        public override int GetHashCode()
        {
            return SubType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecSZArray) && this.Equals((CLRTypeSpecSZArray)obj);
        }

        public bool Equals(CLRTypeSpecSZArray other)
        {
            return other.SubType.Equals(this.SubType);
        }

        public override CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CLRTypeSpecSZArray(SubType.Instantiate(typeParams, methodParams));
        }

        public override bool UsesGenericParamOfType(CLRSigType.ElementType elementType)
        {
            return SubType.UsesGenericParamOfType(elementType);
        }

        public override string ToString()
        {
            return SubType.ToString() + "[]";
        }
    }
}
