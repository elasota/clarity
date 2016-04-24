using System;
using System.IO;

namespace Clarity.Rpa
{
    public class HighMethod
    {
        private bool m_static;
        private bool m_isInternal;
        private MethodSignatureTag m_methodSignature;
        private HighMethodBody m_methodBody;
        private MethodDeclTag m_methodDeclTag;

        public bool IsStatic { get { return m_static; } }
        public bool IsInternal { get { return m_isInternal; } }
        public MethodSignatureTag MethodSignature { get { return m_methodSignature; } }
        public MethodDeclTag MethodDeclTag { get { return m_methodDeclTag; } }
        public HighMethodBody MethodBody { get { return m_methodBody; } }

        public HighMethod(bool isStatic, MethodSignatureTag methodSignature, HighMethodBody methodBody,
            MethodDeclTag methodDeclTag, bool isInternal)
        {
            m_static = isStatic;
            m_methodSignature = methodSignature;

            m_methodBody = methodBody;
            m_methodDeclTag = methodDeclTag;
            m_isInternal = isInternal;
        }

        public HighMethod(HighMethod baseMethod, TagRepository repo, TypeSpecTag[] argTypes)
        {
            m_static = baseMethod.m_static;
            m_methodSignature = baseMethod.m_methodSignature.Instantiate(repo, argTypes);

            m_methodBody = baseMethod.m_methodBody;
            m_methodDeclTag = baseMethod.m_methodDeclTag;
            m_isInternal = baseMethod.m_isInternal;
        }

        // Export exists in CppBuilder.ExportClassDefinitions
        public static HighMethod Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader, TypeNameTag declaringClass)
        {
            bool isStatic = reader.ReadBoolean();
            MethodSignatureTag methodSignature = catalog.GetMethodSignature(reader.ReadUInt32());
            string name = catalog.GetString(reader.ReadUInt32());

            MethodDeclTag methodDeclTag = new MethodDeclTag(name, methodSignature, declaringClass);
            methodDeclTag = rpa.InternMethodDeclTag(methodDeclTag);

            bool isInternal = reader.ReadBoolean();
            HighMethodBody methodBody;

            if (isInternal)
                methodBody = null;
            else
                methodBody = HighMethodBody.Read(rpa, catalog, methodDeclTag, reader);

            return new HighMethod(isStatic, methodSignature, methodBody, methodDeclTag, isInternal);
        }

        public HighMethod Instantiate(TagRepository compiler, TypeSpecTag[] argTypes)
        {
            return new HighMethod(this, compiler, argTypes);
        }
    }
}
