using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class TypeSpecMulticastDelegateTag : TypeSpecTag
    {
        private TypeSpecClassTag m_delegateClass;

        public TypeSpecClassTag DelegateType { get { return m_delegateClass; } }

        public TypeSpecMulticastDelegateTag(TypeSpecClassTag delegateClass)
        {
            m_delegateClass = delegateClass;
        }

        public override SubTypeCode SubType { get { return SubTypeCode.MulticastDelegate; } }

        public override bool Equals(object other)
        {
            TypeSpecMulticastDelegateTag tOther = other as TypeSpecMulticastDelegateTag;
            if (tOther == null)
                return false;

            return m_delegateClass == tOther.m_delegateClass;
        }

        public override int GetHashCode()
        {
            return m_delegateClass.GetHashCode();
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            return new TypeSpecMulticastDelegateTag((TypeSpecClassTag)m_delegateClass.Instantiate(repo, argTypes));
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            return new TypeSpecMulticastDelegateTag((TypeSpecClassTag)m_delegateClass.Instantiate(repo, typeParams, methodParams));
        }

        public override string ToString()
        {
            return "Multicaster(" + m_delegateClass.ToString() + ")";
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
            dw.Write("multicaster(");
            m_delegateClass.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
