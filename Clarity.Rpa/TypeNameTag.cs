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

        public string AssemblyName { get { return m_assemblyName; } }
        public string TypeNamespace { get { return m_typeNamespace; } }
        public string TypeName { get { return m_typeName; } }
        public TypeNameTag ContainerType { get { return m_containerType; } }
        public bool IsInterned { get; set; }

        public TypeNameTag(string assemblyName, string typeNamespace, string typeName, TypeNameTag containerType)
        {
            m_assemblyName = assemblyName;
            m_typeName = typeName;
            m_typeNamespace = typeNamespace;
            m_containerType = containerType;
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

            return new TypeNameTag(assemblyName, typeNamespace, typeName, containerType);
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
            return result;
        }
    }
}
