using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public sealed class CLRMethodSignatureInstanceParam : IEquatable<CLRMethodSignatureInstanceParam>
    {
        public CLRSigParamOrRetType.TypeOfTypeEnum TypeOfType { get; private set; }
        public CLRTypeSpec Type { get; private set; }

        public CLRMethodSignatureInstanceParam(CLRSigParamOrRetType.TypeOfTypeEnum typeOfType, CLRTypeSpec typeSpec)
        {
            TypeOfType = typeOfType;
            Type = typeSpec;
        }

        public CLRMethodSignatureInstanceParam(CLRMethodSignatureInstanceParam baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            TypeOfType = baseInstance.TypeOfType;
            Type = baseInstance.Type.Instantiate(typeParams, methodParams);
        }

        public bool Equals(CLRMethodSignatureInstanceParam other)
        {
            if (this.TypeOfType != other.TypeOfType)
                return false;
            if (!Type.Equals(other.Type))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRMethodSignatureInstanceParam) && this.Equals((CLRMethodSignatureInstanceParam)obj);
        }

        public override int GetHashCode()
        {
            return TypeOfType.GetHashCode() + Type.GetHashCode();
        }

        public override string ToString()
        {
            return TypeOfType.ToString() + " " + Type;
        }
    }
}
