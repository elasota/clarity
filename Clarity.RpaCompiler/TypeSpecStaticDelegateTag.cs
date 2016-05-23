using System;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class TypeSpecDelegateTag : TypeSpecTag
    {
        private TypeSpecClassTag m_delegateType;
        private MethodSpecTag m_methodSpec;

        public override SubTypeCode SubType { get { return SubTypeCode.StaticDelegate; } }

        public TypeSpecDelegateTag(TypeSpecClassTag delegateType, MethodSpecTag methodSpec)
        {
            m_delegateType = delegateType;
            m_methodSpec = methodSpec;
        }

        public override bool Equals(object other)
        {
            TypeSpecDelegateTag tOther = other as TypeSpecDelegateTag;
            if (other == null)
                return false;
            return tOther.m_delegateType == m_delegateType
                && tOther.m_methodSpec == m_methodSpec;
        }

        public override int GetHashCode()
        {
            int hashCode = m_delegateType.GetHashCode();
            hashCode += m_methodSpec.GetHashCode();
            return hashCode;
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            throw new Exception("Can't instantiate an internal type");
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            throw new Exception("Can't instantiate an internal type");
        }

        public override string ToString()
        {
            string result = "StaticDelegate:";
            result += m_delegateType.ToString();
            result += "-->";
            result += m_methodSpec.ToString();
            return result;
        }

        public override void Write(StreamWriter writer)
        {
            throw new NotSupportedException();
        }

        public override void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter)
        {
            throw new NotSupportedException();
        }
    }
}
