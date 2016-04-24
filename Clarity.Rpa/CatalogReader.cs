using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.Rpa
{
    public class CatalogReader
    {
        private string[] m_strings;
        private List<TypeNameTag> m_typeNames;
        private List<TypeSpecTag> m_typeSpecs;
        private MethodDeclTag[] m_methodDeclTags;
        private MethodSignatureTag[] m_methodSignatures;
        private MethodSpecTag[] m_methodSpecs;
        private string m_assemblyName;

        public string AssemblyName { get { return m_assemblyName; } }

        public CatalogReader(TagRepository rpa, BinaryReader reader)
        {
            uint numStrings = reader.ReadUInt32();
            m_strings = new string[numStrings];

            for (uint i = 0; i < numStrings; i++)
                m_strings[i] = reader.ReadString();

            uint numTypeNames = reader.ReadUInt32();
            m_typeNames = new List<TypeNameTag>();

            for (uint i = 0; i < numTypeNames; i++)
                m_typeNames.Add(rpa.InternTypeName(TypeNameTag.Read(this, reader)));

            uint numTypeSpecs = reader.ReadUInt32();
            m_typeSpecs = new List<TypeSpecTag>();

            for (uint i = 0; i < numTypeSpecs; i++)
                m_typeSpecs.Add(rpa.InternTypeSpec(TypeSpecTag.ReadVariant(this, reader)));

            uint numMethodSignatures = reader.ReadUInt32();
            m_methodSignatures = new MethodSignatureTag[numMethodSignatures];
            for (uint i = 0; i < numMethodSignatures; i++)
                m_methodSignatures[i] = rpa.InternMethodSignature(MethodSignatureTag.Read(this, reader));

            uint numVtableSlotTags = reader.ReadUInt32();
            m_methodDeclTags = new MethodDeclTag[numVtableSlotTags];

            for (uint i = 0; i < numVtableSlotTags; i++)
                m_methodDeclTags[i] = rpa.InternMethodDeclTag(MethodDeclTag.Read(this, reader));

            uint numMethodSpecs = reader.ReadUInt32();
            m_methodSpecs = new MethodSpecTag[numMethodSpecs];
            for (uint i = 0; i < numMethodSpecs; i++)
                m_methodSpecs[i] = rpa.InternMethodSpec(MethodSpecTag.Read(this, reader));

            m_assemblyName = GetString(reader.ReadUInt32());

            if (!rpa.RegisterAssembly(m_assemblyName))
                throw new Exception("Assembly registered multiple times");
        }

        public MethodSpecTag GetMethodSpec(uint v)
        {
            return m_methodSpecs[v];
        }

        public TypeSpecTag GetTypeSpec(uint v)
        {
            return m_typeSpecs[(int)v];
        }

        public string GetString(uint v)
        {
            return m_strings[v];
        }

        public MethodSignatureTag GetMethodSignature(uint v)
        {
            return m_methodSignatures[v];
        }

        public TypeNameTag GetTypeName(uint v)
        {
            return m_typeNames[(int)v];
        }

        public MethodDeclTag GetMethodDecl(uint v)
        {
            return m_methodDeclTags[v];
        }
    }
}
