using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public struct VType : IEquatable<VType>
    {
        // Generic parameters are always considered ValueValues
        public enum ValTypeEnum
        {
            Invalid,
            ValueValue,
            NotNullReferenceValue,
            NullableReferenceValue,
            Null,
            ManagedPtr,
            ConstantValue,
            ConstantReference,
            DelegateVirtualMethod,
            DelegateSimpleMethod,
        }

        public ValTypeEnum ValType { get; private set; }
        public CLRTypeSpec TypeSpec { get; private set; }
        public object ConstantValue { get; private set; }

        public VType(ValTypeEnum valType, CLRTypeSpec typeSpec)
            : this()
        {
            ValType = valType;
            TypeSpec = typeSpec;

            if (valType == ValTypeEnum.ConstantValue || valType == ValTypeEnum.ConstantReference)
                throw new ArgumentException();  // Don't use this to construct constant vtypes
        }

        public VType(ValTypeEnum valType, CLRTypeSpec typeSpec, object constValue)
            : this()
        {
            ValType = valType;
            TypeSpec = typeSpec;
            ConstantValue = constValue;
        }

        public bool Equals(VType other)
        {
            if (!ValType.Equals(other.ValType))
                return false;
            if (!TypeSpec.Equals(other.TypeSpec))
                return false;
            if (ConstantValue == null)
            {
                if (other.ConstantValue != null)
                    return false;
            }
            else
            {
                if (other.ConstantValue == null)
                    return false;
                if (!ConstantValue.Equals(other.ConstantValue))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(VType) && this.Equals((VType)obj);
        }

        public override int GetHashCode()
        {
            return ValType.GetHashCode() + TypeSpec.GetHashCode() + (ConstantValue == null ? 0 : ConstantValue.GetHashCode());
        }
    }

}
