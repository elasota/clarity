using System;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class TypeSpecBoxTag : TypeSpecTag
    {
        private TypeSpecClassTag m_containedType;

        public TypeSpecClassTag ContainedType { get { return m_containedType; } }

        public TypeSpecBoxTag(TypeSpecClassTag containedType)
        {
            m_containedType = containedType;
        }

        public override SubTypeCode SubType { get { return SubTypeCode.Box; } }

        public override bool Equals(object other)
        {
            TypeSpecBoxTag tOther = other as TypeSpecBoxTag;
            if (tOther == null)
                return false;
            return m_containedType == tOther.m_containedType;
        }

        public override int GetHashCode()
        {
            return m_containedType.GetHashCode();
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            TypeSpecClassTag containedType = (TypeSpecClassTag)m_containedType.Instantiate(repo, argTypes);
            return new TypeSpecBoxTag(containedType);
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            TypeSpecClassTag containedType = (TypeSpecClassTag)m_containedType.Instantiate(repo, typeParams, methodParams);
            return new TypeSpecBoxTag(containedType);
        }

        public override string ToString()
        {
            return "Box(" + m_containedType + ")";
        }

        public override void Write(StreamWriter writer)
        {
            throw new NotImplementedException();
        }

        public override void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter)
        {
            throw new NotImplementedException();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("box(");
            m_containedType.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
