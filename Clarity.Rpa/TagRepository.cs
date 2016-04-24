using System;
using System.Collections.Generic;

namespace Clarity.Rpa
{
    public class TagRepository
    {
        private Dictionary<TypeNameTag, TypeNameTag> m_typeNameTags = new Dictionary<TypeNameTag, TypeNameTag>();
        private Dictionary<TypeSpecTag, TypeSpecTag> m_typeSpecTags = new Dictionary<TypeSpecTag, TypeSpecTag>();
        private Dictionary<MethodSignatureTag, MethodSignatureTag> m_methodSignatureTags = new Dictionary<MethodSignatureTag, MethodSignatureTag>();
        private Dictionary<MethodDeclTag, MethodDeclTag> m_methodDeclTags = new Dictionary<MethodDeclTag, MethodDeclTag>();
        private Dictionary<MethodSpecTag, MethodSpecTag> m_methodSpecTags = new Dictionary<MethodSpecTag, MethodSpecTag>();
        private HashSet<string> m_assemblies = new HashSet<string>();

        private static T InternDict<T>(Dictionary<T, T> dict, T key)
            where T : IInternable
        {
            T value;
            if (dict.TryGetValue(key, out value))
                return value;
            key.IsInterned = true;
            dict.Add(key, key);
            return key;
        }

        public TypeNameTag InternTypeName(TypeNameTag typeNameTag)
        {
            return InternDict<TypeNameTag>(m_typeNameTags, typeNameTag);
        }

        public TypeSpecTag InternTypeSpec(TypeSpecTag typeSpecTag)
        {
            return InternDict<TypeSpecTag>(m_typeSpecTags, typeSpecTag);
        }

        public MethodSignatureTag InternMethodSignature(MethodSignatureTag methodSignatureTag)
        {
            return InternDict<MethodSignatureTag>(m_methodSignatureTags, methodSignatureTag);
        }

        public MethodDeclTag InternMethodDeclTag(MethodDeclTag methodDeclTag)
        {
            return InternDict<MethodDeclTag>(m_methodDeclTags, methodDeclTag);
        }

        public MethodSpecTag InternMethodSpec(MethodSpecTag methodSpecTag)
        {
            return InternDict<MethodSpecTag>(m_methodSpecTags, methodSpecTag);
        }

        public bool RegisterAssembly(string assemblyName)
        {
            return m_assemblies.Add(assemblyName);
        }
    }
}
