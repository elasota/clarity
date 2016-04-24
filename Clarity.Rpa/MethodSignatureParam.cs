using System;
using System.IO;

namespace Clarity.Rpa
{
    public class MethodSignatureParam : IEquatable<MethodSignatureParam>
    {
        private MethodSignatureParamTypeOfType m_typeOfType;
        private TypeSpecTag m_type;

        public MethodSignatureParamTypeOfType TypeOfType { get { return m_typeOfType; } }
        public TypeSpecTag Type { get { return m_type; } }

        public MethodSignatureParam(TypeSpecTag type, MethodSignatureParamTypeOfType typeOfType)
        {
            m_typeOfType = typeOfType;
            m_type = type;
        }

        public void Write(StreamWriter writer)
        {
            writer.Write("mparam ( ");
            m_typeOfType.Write(writer);
            writer.Write(", ");
            m_type.Write(writer);
            writer.Write(") ");
        }

        public bool Equals(MethodSignatureParam other)
        {
            return m_typeOfType.Equals(other.m_typeOfType)
                && m_type.Equals(other.m_type);
        }

        public override bool Equals(object other)
        {
            MethodSignatureParam tOther = other as MethodSignatureParam;

            if (tOther == null)
                return false;

            return this.Equals(tOther);
        }

        public override int GetHashCode()
        {
            int hash = 0;
            hash += m_typeOfType.GetHashCode();
            hash += m_type.GetHashCode();
            return hash;
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            m_typeOfType.Write(writer);
            writer.Write(fileBuilder.IndexTypeSpecTag(m_type));
        }

        public static MethodSignatureParam Read(CatalogReader rpa, BinaryReader reader)
        {
            MethodSignatureParamTypeOfType tot = MethodSignatureParamTypeOfType.Read(reader);
            TypeSpecTag typeSpec = rpa.GetTypeSpec(reader.ReadUInt32());

            MethodSignatureParam tag = new MethodSignatureParam(typeSpec, tot);

            return tag;
        }

        public MethodSignatureParam Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            TypeSpecTag newType = m_type.Instantiate(repo, argTypes);

            if (newType == m_type)
                return this;

            return new MethodSignatureParam(newType, m_typeOfType);
        }
    }
}
