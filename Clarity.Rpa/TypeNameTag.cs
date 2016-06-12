using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public sealed class TypeNameTag : IEquatable<TypeNameTag>, IInternable
    {
        private string m_typeNamespace;
        private string m_typeName;
        private string m_assemblyName;
        private TypeNameTag m_containerType;
        private uint m_numGenericParameters;

        public string AssemblyName { get { return m_assemblyName; } }
        public string TypeNamespace { get { return m_typeNamespace; } }
        public string TypeName { get { return m_typeName; } }
        public uint NumGenericParameters { get { return m_numGenericParameters; } }
        public TypeNameTag ContainerType { get { return m_containerType; } }
        public bool IsInterned { get; set; }

        public TypeNameTag(string assemblyName, string typeNamespace, string typeName)
        {
            m_assemblyName = assemblyName;
            m_typeName = typeName;
            m_typeNamespace = typeNamespace;
            m_containerType = null;
            m_numGenericParameters = 0;
        }

        public TypeNameTag(string assemblyName, string typeNamespace, string typeName, uint numGenericParameters, TypeNameTag containerType)
        {
            m_assemblyName = assemblyName;
            m_typeName = typeName;
            m_typeNamespace = typeNamespace;
            m_containerType = containerType;
            m_numGenericParameters = numGenericParameters;
        }

        public bool FastIs(string assemblyName, string typeNamespace, string typeName, uint numGenericParameters, TypeNameTag containerType)
        {
            return m_assemblyName == assemblyName &&
                m_typeNamespace == typeNamespace &&
                m_typeName == typeName &&
                m_numGenericParameters == numGenericParameters &&
                m_containerType == containerType;
        }

        public bool Equals(TypeNameTag other)
        {
            if (this.IsInterned && other.IsInterned)
                return this == other;

            if (m_assemblyName != other.m_assemblyName)
                return false;
            if (m_typeName != other.m_typeName)
                return false;
            if (m_typeNamespace != other.m_typeNamespace)
                return false;

            if (m_containerType == null)
            {
                if (other.m_containerType != null)
                    return false;
            }
            else
            {
                if (other.m_containerType == null)
                    return false;
                if (!m_containerType.Equals(other.m_containerType))
                    return false;
            }

            if (m_numGenericParameters != other.m_numGenericParameters)
                return false;

            return true;
        }

        public static TypeNameTag Read(CatalogReader rpaCatalogReader, BinaryReader reader)
        {
            TypeNameTag containerType = null;

            uint containerTypeIndex = reader.ReadUInt32();
            if (containerTypeIndex != 0)
                containerType = rpaCatalogReader.GetTypeName(containerTypeIndex - 1);
            string assemblyName = rpaCatalogReader.GetString(reader.ReadUInt32());
            string typeNamespace = rpaCatalogReader.GetString(reader.ReadUInt32());
            string typeName = rpaCatalogReader.GetString(reader.ReadUInt32());
            uint numGenericParameters = reader.ReadUInt32();

            return new TypeNameTag(assemblyName, typeNamespace, typeName, numGenericParameters, containerType);
        }

        public override int GetHashCode()
        {
            int hash = m_typeName.GetHashCode();
            hash += m_typeNamespace.GetHashCode();

            if (m_containerType != null)
                hash += m_containerType.GetHashCode();
            return hash;
        }

        public void Write(HighFileBuilder builder, BinaryWriter writer)
        {
            if (m_containerType == null)
                writer.Write((uint)0);
            else
            {
                uint index = builder.IndexTypeNameTag(m_containerType);
                writer.Write(1 + index);
            }
            writer.Write(builder.IndexString(m_assemblyName));
            writer.Write(builder.IndexString(m_typeNamespace));
            writer.Write(builder.IndexString(m_typeName));
            writer.Write(m_numGenericParameters);
        }

        public void Write(StreamWriter writer)
        {
            if (m_containerType != null)
            {
                writer.Write("{");
                m_containerType.Write(writer);
                writer.Write("}/");
            }
            writer.Write("[");
            writer.Write(m_assemblyName);
            writer.Write("]");
            writer.Write(NameEscaper.EscapeName(m_typeNamespace));
            writer.Write(".");
            writer.Write(NameEscaper.EscapeName(m_typeName));
        }

        public void WriteDisassembly(DisassemblyWriter dw)
        {
            if (m_containerType != null)
                m_containerType.WriteDisassembly(dw);
            else
            {
                dw.Write(".");
                dw.WriteToken(m_assemblyName);
            }
            dw.Write(".");
            dw.WriteToken(m_typeNamespace);
            dw.Write(".");
            dw.WriteToken(m_typeName);

            if (m_numGenericParameters > 0)
            {
                dw.Write("^");
                dw.Write(m_numGenericParameters.ToString());
            }
        }

        public override bool Equals(object other)
        {
            TypeNameTag tOther = other as TypeNameTag;

            if (tOther == null)
                return false;

            return this.Equals(tOther);
        }

        public override string ToString()
        {
            string result = "[" + m_assemblyName + "]";
            if (m_containerType != null)
                result += "(" + m_containerType.ToString() + ")/";
            result += m_typeNamespace + ":" + m_typeName;
            if (m_numGenericParameters > 0)
                result += "^" + m_numGenericParameters.ToString();
            return result;
        }
    }
}
