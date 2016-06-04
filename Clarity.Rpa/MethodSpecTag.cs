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

            if ((uint)genericParameters.Length != m_methodDeclTag.BaseMethodSignature.NumGenericParameters)
                throw new ArgumentException("Incorrect number of generic paramters");
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            writer.Write((byte)m_methodSlotType);
            writer.Write(fileBuilder.IndexMethodDeclTag(m_methodDeclTag));

            foreach (TypeSpecTag paramType in m_genericParameters)
                writer.Write(fileBuilder.IndexTypeSpecTag(paramType));

            writer.Write(fileBuilder.IndexTypeSpecTag(m_declaringClass));
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

            MethodDeclTag declTag = rpa.GetMethodDecl(reader.ReadUInt32());

            uint numGenericParameters = declTag.BaseMethodSignature.NumGenericParameters;

            TypeSpecTag[] paramTypes = null;
            paramTypes = new TypeSpecTag[numGenericParameters];
            for (uint i = 0; i < numGenericParameters; i++)
                paramTypes[i] = rpa.GetTypeSpec(reader.ReadUInt32());

            TypeSpecTag declaringClass = rpa.GetTypeSpec(reader.ReadUInt32());
            if (declaringClass.GetType() != typeof(TypeSpecClassTag))
                throw new Exception("Method spec declaring type is not a class");

            return new MethodSpecTag(slotType, paramTypes, (TypeSpecClassTag)declaringClass, declTag);
        }

        public MethodSpecTag Instantiate(TagRepository tagRepo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            int numGenericParameters = m_genericParameters.Length;

            TypeSpecTag[] genericParameters;

            bool anyChanged = false;
            if (numGenericParameters == 0)
                genericParameters = m_genericParameters;
            else
            {
                genericParameters = new TypeSpecTag[numGenericParameters];
                for (int i = 0; i < numGenericParameters; i++)
                {
                    TypeSpecTag param = m_genericParameters[i];
                    TypeSpecTag newParam = param.Instantiate(tagRepo, typeParams, methodParams);
                    genericParameters[i] = newParam;

                    if (param != newParam)
                        anyChanged = true;
                }
            }

            TypeSpecClassTag declaringClass = (TypeSpecClassTag)m_declaringClass.Instantiate(tagRepo, typeParams, methodParams);
            if (declaringClass != m_declaringClass)
                anyChanged = true;

            if (!anyChanged)
                return this;

            return tagRepo.InternMethodSpec(new MethodSpecTag(m_methodSlotType, genericParameters, declaringClass, m_methodDeclTag));
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

        public override string ToString()
        {
            string result = this.MethodDecl.ToString();
            int numGenericParams = this.GenericParameters.Length;
            if (numGenericParams != 0)
            {
                result += "<";
                for (int i = 0; i < numGenericParams; i++)
                {
                    if (i != 0)
                        result += ",";
                    result += this.GenericParameters[i].ToString();
                }
                result += ">";
            }
            return result;
        }
    }
}
