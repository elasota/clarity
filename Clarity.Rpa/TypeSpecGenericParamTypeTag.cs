using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.Rpa
{
    public struct TypeSpecGenericParamTypeTag : IEquatable<TypeSpecGenericParamTypeTag>
    {
        public enum Values
        {
            Var,
            MVar,
        }

        private Values m_value;

        public Values Value { get { return m_value; } }

        public TypeSpecGenericParamTypeTag(Values v)
        {
            m_value = v;
        }

        public void Write(StreamWriter writer)
        {
            switch (m_value)
            {
                case Values.Var:
                    writer.Write("!");
                    break;
                case Values.MVar:
                    writer.Write("!!");
                    break;
                default:
                    throw new NotSupportedException("Unsupported generic param type");
            }
        }

        public void Write(BinaryWriter catalogWriter)
        {
            catalogWriter.Write((byte)m_value);
        }

        public bool Equals(TypeSpecGenericParamTypeTag other)
        {
            return m_value == other.m_value;
        }

        public override int GetHashCode()
        {
            return m_value.GetHashCode();
        }

        public static TypeSpecGenericParamTypeTag Read(BinaryReader reader)
        {
            Values v = (Values)reader.ReadByte();
            if (v != Values.MVar && v != Values.Var)
                throw new Exception("Invalid var value");

            TypeSpecGenericParamTypeTag typeTag;
            typeTag.m_value = v;
            return typeTag;
        }
    }
}
