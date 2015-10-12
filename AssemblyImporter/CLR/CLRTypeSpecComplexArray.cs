using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecComplexArray : CLRTypeSpec, IEquatable<CLRTypeSpecComplexArray>
    {
        public CLRTypeSpec SubType { get; private set; }
        public uint Rank { get; private set; }
        public int[] LowBounds { get; private set; }

        public CLRTypeSpecComplexArray(CLRTypeSpec subType, uint rank, int[] lowBounds)
        {
            SubType = subType;
            Rank = rank;
            LowBounds = lowBounds;
        }

        public override int GetHashCode()
        {
            return SubType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecComplexArray) && this.Equals((CLRTypeSpecComplexArray)obj);
        }

        public bool Equals(CLRTypeSpecComplexArray other)
        {
            if (Rank != other.Rank)
                return false;
            for (uint d = 0; d < Rank; d++)
                if (LowBounds[d] != other.LowBounds[d])
                    return false;
            if (!other.SubType.Equals(this.SubType))
                return false;
            return true;
        }

        public override CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CLRTypeSpecComplexArray(SubType.Instantiate(typeParams, methodParams), Rank, LowBounds);
        }

        public override bool UsesGenericParamOfType(CLRSigType.ElementType elementType)
        {
            return SubType.UsesGenericParamOfType(elementType);
        }

        public override string ToString()
        {
            string fullName = SubType.ToString() + "[";
            for (int i = 0; i < Rank; i++)
            {
                if (i != 0)
                    fullName += ",";
                fullName += LowBounds[i] + "...";
            }
            return fullName + "]";
        }
    }
}
