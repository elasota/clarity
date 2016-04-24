using System;
using System.IO;

namespace Clarity.Rpa
{
    public abstract class TypeSpecTag : IInternable
    {
        protected enum SubTypeCode
        {
            Array,
            Class,
            GenericParameter,
            Void,
        }

        public abstract void Write(StreamWriter writer);
        public abstract void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter);
        public abstract override int GetHashCode();
        public abstract override bool Equals(object other);
        public bool IsInterned { get; set; }

        public static TypeSpecTag ReadVariant(CatalogReader rpa, BinaryReader reader)
        {
            TypeSpecTag tag = null;
            SubTypeCode typeCode = (SubTypeCode)reader.ReadByte();

            switch (typeCode)
            {
                case SubTypeCode.Array:
                    tag = TypeSpecArrayTag.Read(rpa, reader);
                    break;
                case SubTypeCode.Class:
                    tag = TypeSpecClassTag.Read(rpa, reader);
                    break;
                case SubTypeCode.GenericParameter:
                    tag = TypeSpecGenericParamTag.Read(rpa, reader);
                    break;
                case SubTypeCode.Void:
                    tag = TypeSpecVoidTag.Read(rpa, reader);
                    break;
                default:
                    throw new Exception("Malformed type tag");
            }

            return tag;
        }

        public abstract TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams);
        public abstract TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes);
    }
}
