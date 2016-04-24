using System;
using System.IO;

namespace Clarity.Rpa
{
    public sealed class MethodSpecTag : IEquatable<MethodSpecTag>, IInternable
    {
        private MethodSlotType m_methodSlotType;
        private TypeSpecTag[] m_genericParameters;
        private TypeSpecClassTag m_declaringClass;
        private MethodDeclTag m_methodDeclTag;

        public MethodSlotType MethodSlotType { get { return m_methodSlotType; } }
        public TypeSpecTag[] GenericParameters { get { return m_genericParameters; } }
        public TypeSpecClassTag DeclaringClass { get { return m_declaringClass; } }
        public MethodDeclTag MethodDecl { get { return m_methodDeclTag; } }
        public bool IsInterned { get; set; }

        public MethodSpecTag(MethodSlotType methodSlotType, TypeSpecTag[] genericParameters, TypeSpecClassTag declaringClass, MethodDeclTag methodDefTag)
        {
            m_methodSlotType = methodSlotType;
            m_genericParameters = genericParameters;
            m_declaringClass = declaringClass;
            m_methodDeclTag = methodDefTag;
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            writer.Write((byte)m_methodSlotType);

            writer.Write((uint)m_genericParameters.Length);
            foreach (TypeSpecTag paramType in m_genericParameters)
                writer.Write(fileBuilder.IndexTypeSpecTag(paramType));

            writer.Write(fileBuilder.IndexTypeSpecTag(m_declaringClass));
            writer.Write(fileBuilder.IndexMethodDeclTag(m_methodDeclTag));
        }

        public override int GetHashCode()
        {
            // FIXME
            return m_methodDeclTag.GetHashCode();
        }

        public static MethodSpecTag Read(CatalogReader rpa, BinaryReader reader)
        {
            MethodSlotType slotType = (MethodSlotType)reader.ReadByte();
            if (slotType != MethodSlotType.Instance && slotType != MethodSlotType.Static && slotType != MethodSlotType.Virtual)
                throw new Exception("Invalid method slot type");

            uint numGenericParameters = reader.ReadUInt32();

            TypeSpecTag[] paramTypes = null;
            paramTypes = new TypeSpecTag[numGenericParameters];
            for (uint i = 0; i < numGenericParameters; i++)
                paramTypes[i] = rpa.GetTypeSpec(reader.ReadUInt32());

            TypeSpecTag declaringClass = rpa.GetTypeSpec(reader.ReadUInt32());
            if (declaringClass.GetType() != typeof(TypeSpecClassTag))
                throw new Exception("Method spec declaring type is not a class");

            MethodDeclTag declTag = rpa.GetMethodDecl(reader.ReadUInt32());

            return new MethodSpecTag(slotType, paramTypes, (TypeSpecClassTag)declaringClass, declTag);
        }

        public override bool Equals(object obj)
        {
            MethodSpecTag tOther = obj as MethodSpecTag;
            if (tOther == null)
                return false;
            return this.Equals(tOther);
        }

        public bool Equals(MethodSpecTag other)
        {
            if (this.IsInterned && other.IsInterned)
                return this == other;

            if (m_methodSlotType != other.m_methodSlotType
                || !m_declaringClass.Equals(other.m_declaringClass)
                || !m_methodDeclTag.Equals(other.m_methodDeclTag)
                )
                return false;

            if (m_genericParameters == null)
            {
                if (other.m_genericParameters != null)
                    return false;
            }
            else
            {
                int gpLen = m_genericParameters.Length;
                if (gpLen != other.m_genericParameters.Length)
                    return false;

                for (int i = 0; i < gpLen; i++)
                    if (!m_genericParameters[i].Equals(other.m_genericParameters[i]))
                        return false;
            }

            return true;
        }
    }
}
