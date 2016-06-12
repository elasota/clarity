using System;
using System.IO;

namespace Clarity.Rpa
{
    public class TypeSpecGenericParamTag : TypeSpecTag
    {
        private TypeSpecGenericParamTypeTag m_genericParamType;
        private uint m_index;

        public TypeSpecGenericParamTypeTag GenericParamType { get { return m_genericParamType; } }
        public uint Index { get { return m_index; } }

        public override SubTypeCode SubType { get { return SubTypeCode.GenericParameter; } }

        public TypeSpecGenericParamTag(TypeSpecGenericParamTypeTag genericParamType, uint index)
        {
            m_genericParamType = genericParamType;
            m_index = index;
        }

        public override bool Equals(object other)
        {
            TypeSpecGenericParamTag tOther = other as TypeSpecGenericParamTag;

            if (tOther == null)
                return false;

            if (this.IsInterned && tOther.IsInterned)
                return this == tOther;

            return m_genericParamType.Equals(tOther.m_genericParamType) &&
                m_index == tOther.m_index;
        }

        public override int GetHashCode()
        {
            int hash = SubTypeCode.GenericParameter.GetHashCode();
            hash += m_genericParamType.GetHashCode();
            return hash;
        }

        public override void Write(StreamWriter writer)
        {
            m_genericParamType.Write(writer);
            writer.Write(m_index);
        }

        public override void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter)
        {
            catalogWriter.Write((byte)SubTypeCode.GenericParameter);
            m_genericParamType.Write(catalogWriter);
            catalogWriter.Write(m_index);
        }

        public static TypeSpecGenericParamTag Read(CatalogReader rpa, BinaryReader reader)
        {
            TypeSpecGenericParamTypeTag paramType = TypeSpecGenericParamTypeTag.Read(reader);
            uint index = reader.ReadUInt32();

            return new TypeSpecGenericParamTag(paramType, index);
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            if (m_genericParamType.Value == TypeSpecGenericParamTypeTag.Values.Var)
                return argTypes[m_index];
            return this;
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            if (m_genericParamType.Value == TypeSpecGenericParamTypeTag.Values.Var)
                return typeParams[m_index];
            else if (m_genericParamType.Value == TypeSpecGenericParamTypeTag.Values.MVar)
                return methodParams[m_index];
            throw new Exception();
        }

        public override string ToString()
        {
            return m_genericParamType.ToString() + m_index.ToString();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gen(");
            switch (m_genericParamType.Value)
            {
                case TypeSpecGenericParamTypeTag.Values.MVar:
                    dw.Write("M");
                    break;
                case TypeSpecGenericParamTypeTag.Values.Var:
                    dw.Write("T");
                    break;
                default:
                    throw new Exception();
            }
            dw.Write(m_index.ToString());
            dw.Write(")");
        }
    }
}
