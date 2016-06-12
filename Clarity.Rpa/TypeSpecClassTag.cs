using System;
using System.IO;
using System.Collections.Generic;

namespace Clarity.Rpa
{
    public sealed class TypeSpecClassTag : TypeSpecTag
    {
        private TypeNameTag m_typeNameTag;
        private TypeSpecTag[] m_argTypes;

        public TypeNameTag TypeName { get { return m_typeNameTag; } }
        public TypeSpecTag[] ArgTypes { get { return m_argTypes; } }

        public override SubTypeCode SubType { get { return SubTypeCode.Class; } }

        public TypeSpecClassTag(TypeNameTag typeNameTag, TypeSpecTag[] argTypes)
        {
            m_typeNameTag = typeNameTag;
            m_argTypes = argTypes;
        }

        public override bool Equals(object other)
        {
            TypeSpecClassTag tOther = other as TypeSpecClassTag;

            if (tOther == null)
                return false;

            if (this.IsInterned && tOther.IsInterned)
                return this == tOther;

            if (!m_typeNameTag.Equals(tOther.m_typeNameTag))
                return false;
            if (m_argTypes.Length != tOther.m_argTypes.Length)
                return false;
            int len = m_argTypes.Length;
            for (int i = 0; i < len; i++)
                if (!m_argTypes[i].Equals(tOther.m_argTypes[i]))
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
            int hash = SubTypeCode.Class.GetHashCode();
            hash += m_typeNameTag.GetHashCode();
            hash += m_argTypes.Length.GetHashCode();
            foreach (TypeSpecTag argType in m_argTypes)
                hash += argType.GetHashCode();
            return hash;
        }

        public static TypeSpecClassTag Read(CatalogReader rpa, BinaryReader reader)
        {
            TypeNameTag typeNameTag = rpa.GetTypeName(reader.ReadUInt32());

            uint numArgTypes = typeNameTag.NumGenericParameters;
            TypeSpecTag[] argTypes = new TypeSpecTag[numArgTypes];

            for (uint i = 0; i < numArgTypes; i++)
                argTypes[i] = rpa.GetTypeSpec(reader.ReadUInt32());

            return new TypeSpecClassTag(typeNameTag, argTypes);
        }

        public override void Write(StreamWriter writer)
        {
            m_typeNameTag.Write(writer);
            writer.Write("<");
            for (int i = 0; i < m_argTypes.Length; i++)
            {
                if (i != 0)
                    writer.Write(",");
                m_argTypes[i].Write(writer);
            }
            writer.Write(">");
        }

        public override void Write(HighFileBuilder highFileBuilder, BinaryWriter catalogWriter)
        {
            uint typeName = highFileBuilder.IndexTypeNameTag(m_typeNameTag);
            List<uint> argTypes = new List<uint>();

            foreach (TypeSpecTag typeTag in m_argTypes)
                argTypes.Add(highFileBuilder.IndexTypeSpecTag(typeTag));

            catalogWriter.Write((byte)SubTypeCode.Class);
            catalogWriter.Write(typeName);

            foreach (uint argType in argTypes)
                catalogWriter.Write(argType);
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            if (m_argTypes.Length == 0)
                return this;

            List<TypeSpecTag> newArgTypes = new List<TypeSpecTag>();
            foreach (TypeSpecTag argType in m_argTypes)
                newArgTypes.Add(argType.Instantiate(repo, argTypes));

            TypeSpecClassTag newSpec = new TypeSpecClassTag(m_typeNameTag, newArgTypes.ToArray());
            return repo.InternTypeSpec(newSpec);
        }

        public override TypeSpecTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            if (m_argTypes.Length == 0)
                return this;

            List<TypeSpecTag> newArgTypes = new List<TypeSpecTag>();
            foreach (TypeSpecTag argType in m_argTypes)
                newArgTypes.Add(argType.Instantiate(repo, typeParams, methodParams));

            TypeSpecClassTag newSpec = new TypeSpecClassTag(m_typeNameTag, newArgTypes.ToArray());
            return repo.InternTypeSpec(newSpec);
        }

        public override string ToString()
        {
            string result = this.m_typeNameTag.ToString();

            int numGenericParams = m_argTypes.Length;
            if (numGenericParams > 0)
            {
                result += "<";
                for (int i = 0; i < numGenericParams; i++)
                {
                    if (i != 0)
                        result += ",";
                    result += m_argTypes[i].ToString();
                }
                result += ">";
            }

            return result;
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            m_typeNameTag.WriteDisassembly(dw);
            if (m_argTypes.Length > 0)
            {
                dw.Write("<");
                for (int i = 0; i < m_argTypes.Length; i++)
                {
                    if (i != 0)
                        dw.Write(",");
                    m_argTypes[i].WriteDisassembly(dw);
                }
                dw.Write(">");
            }
        }
    }
}
