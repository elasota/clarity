using System;
using System.IO;

namespace Clarity.Rpa
{
    public class TypeSpecArrayTag : TypeSpecTag
    {
        private uint m_rank;
        private TypeSpecTag m_typeSpecTag;

        public TypeSpecArrayTag(uint rank, TypeSpecTag typeSpecTag)
        {
            m_rank = rank;
            m_typeSpecTag = typeSpecTag;
        }

        public override bool Equals(object other)
        {
            TypeSpecArrayTag tOther = other as TypeSpecArrayTag;

            if (tOther == null)
                return false;

            if (this.IsInterned && tOther.IsInterned)
                return this == tOther;

            return m_rank == tOther.m_rank &&
                m_typeSpecTag.Equals(tOther.m_typeSpecTag);
        }

        public override int GetHashCode()
        {
            int hash = SubTypeCode.Array.GetHashCode();
            hash += m_rank.GetHashCode();
            hash += m_typeSpecTag.GetHashCode();
            return hash;
        }

        public override void Write(StreamWriter writer)
        {
            m_typeSpecTag.Write(writer);
            writer.Write("[");
            for (uint i = 1; i < m_rank; i++)
                writer.Write(",");
            writer.Write("]");
        }

        public static TypeSpecArrayTag Read(CatalogReader rpa, BinaryReader reader)
        {
            uint rank = reader.ReadUInt32();
            TypeSpecTag subscriptType = rpa.GetTypeSpec(reader.ReadUInt32());

            return new TypeSpecArrayTag(rank, subscriptType);
        }

        public override void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter)
        {
            uint subscriptIndex = highFileBuilder.IndexTypeSpecTag(m_typeSpecTag);

            catalogWriter.Write((byte)SubTypeCode.Array);
            catalogWriter.Write(m_rank);
            catalogWriter.Write(subscriptIndex);
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            TypeSpecTag newSubscript = m_typeSpecTag.Instantiate(repo, argTypes);
            TypeSpecArrayTag newArrayType = new TypeSpecArrayTag(m_rank, newSubscript);

            return repo.InternTypeSpec(newArrayType);
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            TypeSpecTag newSubscript = m_typeSpecTag.Instantiate(repo, typeParams, methodParams);
            TypeSpecArrayTag newArrayType = new TypeSpecArrayTag(m_rank, newSubscript);

            return repo.InternTypeSpec(newArrayType);
        }
    }
}
