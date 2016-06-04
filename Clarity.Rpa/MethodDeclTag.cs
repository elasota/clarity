using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public sealed class MethodDeclTag : IEquatable<MethodDeclTag>, IInternable
    {
        private MethodSignatureTag m_methodSignature;
        private TypeNameTag m_declaredInClass;
        private string m_name;

        public MethodSignatureTag BaseMethodSignature { get { return m_methodSignature; } }
        public TypeNameTag DeclaredInClass { get { return m_declaredInClass; } }
        public string Name { get { return m_name; } }

        public bool IsInterned { get; set; }

        public MethodDeclTag(string name, MethodSignatureTag methodSignature, TypeNameTag declaredInClass)
        {
            m_name = name;
            m_methodSignature = methodSignature;
            m_declaredInClass = declaredInClass;
        }

        public bool Equals(MethodDeclTag other)
        {
            if (m_name != other.m_name)
                return false;

            if (m_methodSignature == null)
            {
                if (other.m_methodSignature != null)
                    return false;
            }
            else
            {
                if (other.m_methodSignature == null)
                    return false;
                if (!m_methodSignature.Equals(other.m_methodSignature))
                    return false;
            }

            if (m_declaredInClass == null)
            {
                if (other.m_declaredInClass != null)
                    return false;
            }
            else
            {
                if (other.m_declaredInClass == null)
                    return false;
                if (!m_declaredInClass.Equals(other.m_declaredInClass))
                    return false;
            }

            return true;
        }

        public static MethodDeclTag Read(CatalogReader rpa, BinaryReader reader)
        {
            MethodSignatureTag methodSignature = null;
            TypeNameTag declaredInClass = null;

            string name = rpa.GetString(reader.ReadUInt32());

            methodSignature = rpa.GetMethodSignature(reader.ReadUInt32());

            uint declaredInClassIndex = reader.ReadUInt32();
            if (declaredInClassIndex != 0)
                declaredInClass = rpa.GetTypeName(declaredInClassIndex - 1);

            return new MethodDeclTag(name, methodSignature, declaredInClass);
        }

        public void Write(StreamWriter writer)
        {
            if (m_methodSignature == null && m_declaredInClass == null)
            {
                writer.Write("noslottag ");
                return;
            }

            writer.Write("slottag ( ");
            if (m_methodSignature == null)
                writer.Write("null");
            else
                m_methodSignature.Write(writer);

            writer.Write(", ");
            if (m_declaredInClass == null)
                writer.Write("null");
            else
                m_declaredInClass.Write(writer);
            writer.Write(") ");
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexString(m_name));
            writer.Write(fileBuilder.IndexMethodSignatureTag(m_methodSignature));

            if (m_declaredInClass == null)
                writer.Write((uint)0);
            else
                writer.Write(1 + fileBuilder.IndexTypeNameTag(m_declaredInClass));
        }

        public override bool Equals(object other)
        {
            MethodDeclTag tOther = other as MethodDeclTag;

            if (tOther == null)
                return false;

            return this.Equals(tOther);
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (m_methodSignature != null)
                hash += m_methodSignature.GetHashCode();
            if (m_declaredInClass != null)
                hash += m_declaredInClass.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return m_declaredInClass.ToString() + "/" + m_name + "|" + m_methodSignature.ToString();
        }
    }
}
