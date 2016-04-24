using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public struct MethodSignatureParamTypeOfType : IEquatable<MethodSignatureParamTypeOfType>
    {
        public enum Values
        {
            Value,
            ByRef,
            TypedByRef,
        }

        private Values m_value;

        public Values Value { get { return m_value; } }

        public MethodSignatureParamTypeOfType(Values v)
        {
            m_value = v;
        }

        public void Write(StreamWriter writer)
        {
            switch(m_value)
            {
                case Values.ByRef:
                    writer.Write("byref ");
                    break;
                case Values.TypedByRef:
                    writer.Write("typedbyref ");
                    break;
                case Values.Value:
                    writer.Write("value ");
                    break;
                default:
                    throw new Exception("Unrecognized TOT type");
            }
        }

        public override int GetHashCode()
        {
            return m_value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(MethodSignatureParamTypeOfType))
                return this.Equals((MethodSignatureParamTypeOfType)obj);
            return false;
        }

        public bool Equals(MethodSignatureParamTypeOfType other)
        {
            return m_value == other.m_value;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)m_value);
        }

        public static MethodSignatureParamTypeOfType Read(BinaryReader reader)
        {
            Values v = (Values)reader.ReadByte();

            if (v != Values.Value && v != Values.ByRef && v != Values.TypedByRef)
                throw new Exception("Invalid method parameter type of type");

            return new MethodSignatureParamTypeOfType(v);
        }
    }
}
