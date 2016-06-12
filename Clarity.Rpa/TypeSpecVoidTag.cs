using System;
using System.IO;

namespace Clarity.Rpa
{
    public sealed class TypeSpecVoidTag : TypeSpecTag
    {
        public override SubTypeCode SubType { get { return SubTypeCode.Void; } }

        public override bool Equals(object other)
        {
            return other.GetType() == typeof(TypeSpecVoidTag);
        }

        public override int GetHashCode()
        {
            return SubTypeCode.Void.GetHashCode();
        }

        public override void Write(StreamWriter writer)
        {
            writer.Write("void");
        }

        public override void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter)
        {
            catalogWriter.Write((byte)SubTypeCode.Void);
        }

        public static TypeSpecTag Read(CatalogReader rpa, BinaryReader reader)
        {
            return new TypeSpecVoidTag();
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            return this;
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            return this;
        }

        public override string ToString()
        {
            return "void";
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("void");
        }
    }
}
