using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public class HighFileBuilder
    {
        private Dictionary<Clarity.Rpa.TypeSpecTag, uint> m_typeSpecTagsDict;
        private Dictionary<Clarity.Rpa.TypeNameTag, uint> m_typeNameTagsDict;
        private Dictionary<Clarity.Rpa.MethodDeclTag, uint> m_methodDeclTagsDict;
        private Dictionary<string, uint> m_stringsDict;
        private Dictionary<MethodSignatureTag, uint> m_methodSignaturesDict;
        private Dictionary<MethodSpecTag, uint> m_methodSpecDict;

        private MemoryStream m_typeCatalogStream;
        private BinaryWriter m_typeCatalogWriter;

        private MemoryStream m_typeNameCatalogStream;
        private BinaryWriter m_typeNameCatalogWriter;

        private MemoryStream m_stringsCatalogStream;
        private BinaryWriter m_stringsCatalogWriter;

        private MemoryStream m_methodDeclCatalogStream;
        private BinaryWriter m_methodDeclCatalogWriter;

        private MemoryStream m_methodSignatureCatalogStream;
        private BinaryWriter m_methodSignatureCatalogWriter;

        private MemoryStream m_methodSpecCatalogStream;
        private BinaryWriter m_methodSpecCatalogWriter;

        public HighFileBuilder()
        {
            m_typeSpecTagsDict = new Dictionary<TypeSpecTag, uint>();
            m_typeNameTagsDict = new Dictionary<TypeNameTag, uint>();
            m_methodDeclTagsDict = new Dictionary<MethodDeclTag, uint>();
            m_stringsDict = new Dictionary<string, uint>();
            m_methodSignaturesDict = new Dictionary<MethodSignatureTag, uint>();
            m_methodSpecDict = new Dictionary<MethodSpecTag, uint>();

            m_typeCatalogStream = new MemoryStream();
            m_typeCatalogWriter = new BinaryWriter(m_typeCatalogStream);

            m_typeNameCatalogStream = new MemoryStream();
            m_typeNameCatalogWriter = new BinaryWriter(m_typeNameCatalogStream);

            m_stringsCatalogStream = new MemoryStream();
            m_stringsCatalogWriter = new BinaryWriter(m_stringsCatalogStream, System.Text.Encoding.UTF8);

            m_methodDeclCatalogStream = new MemoryStream();
            m_methodDeclCatalogWriter = new BinaryWriter(m_methodDeclCatalogStream);

            m_methodSignatureCatalogStream = new MemoryStream();
            m_methodSignatureCatalogWriter = new BinaryWriter(m_methodSignatureCatalogStream);

            m_methodSpecCatalogStream = new MemoryStream();
            m_methodSpecCatalogWriter = new BinaryWriter(m_methodSpecCatalogStream);
        }

        public uint IndexTypeSpecTag(Clarity.Rpa.TypeSpecTag typeSpecTag)
        {
            uint index;
            if (m_typeSpecTagsDict.TryGetValue(typeSpecTag, out index))
                return index;

            typeSpecTag.Write(this, m_typeCatalogWriter);

            index = (uint)m_typeSpecTagsDict.Count;
            m_typeSpecTagsDict.Add(typeSpecTag, index);

            return index;
        }

        public uint IndexTypeNameTag(TypeNameTag typeNameTag)
        {
            uint index;
            if (m_typeNameTagsDict.TryGetValue(typeNameTag, out index))
                return index;

            typeNameTag.Write(this, m_typeNameCatalogWriter);

            index = (uint)m_typeNameTagsDict.Count;
            m_typeNameTagsDict.Add(typeNameTag, index);

            return index;
        }

        public uint IndexString(string str)
        {
            uint index;
            if (m_stringsDict.TryGetValue(str, out index))
                return index;

            m_stringsCatalogWriter.Write(str);

            index = (uint)m_stringsDict.Count;
            m_stringsDict.Add(str, index);

            return index;
        }

        public uint IndexMethodDeclTag(MethodDeclTag slotTag)
        {
            uint index;
            if (m_methodDeclTagsDict.TryGetValue(slotTag, out index))
                return index;

            slotTag.Write(this, m_methodDeclCatalogWriter);

            index = (uint)m_methodDeclTagsDict.Count;
            m_methodDeclTagsDict.Add(slotTag, index);

            return index;
        }

        public uint IndexMethodSignatureTag(MethodSignatureTag sigTag)
        {
            uint index;
            if (m_methodSignaturesDict.TryGetValue(sigTag, out index))
                return index;

            sigTag.Write(this, m_methodSignatureCatalogWriter);

            index = (uint)m_methodSignaturesDict.Count;
            m_methodSignaturesDict.Add(sigTag, index);

            return index;
        }

        public uint IndexMethodSpecTag(MethodSpecTag methodSpec)
        {
            uint index;
            if (m_methodSpecDict.TryGetValue(methodSpec, out index))
                return index;

            methodSpec.Write(this, m_methodSpecCatalogWriter);

            index = (uint)m_methodSpecDict.Count;
            m_methodSpecDict.Add(methodSpec, index);

            return index;
        }

        public void FlushAndWriteCatalogs(BinaryWriter writer)
        {
            writer.Write((uint)m_stringsDict.Count);
            writer.Flush();
            m_stringsCatalogWriter.Flush();
            m_stringsCatalogStream.WriteTo(writer.BaseStream);

            writer.Write((uint)m_typeNameTagsDict.Count);
            writer.Flush();
            m_typeNameCatalogWriter.Flush();
            m_typeNameCatalogStream.WriteTo(writer.BaseStream);

            writer.Write((uint)m_typeSpecTagsDict.Count);
            writer.Flush();
            m_typeCatalogWriter.Flush();
            m_typeCatalogStream.WriteTo(writer.BaseStream);

            writer.Write((uint)m_methodSignaturesDict.Count);
            writer.Flush();
            m_methodSignatureCatalogWriter.Flush();
            m_methodSignatureCatalogStream.WriteTo(writer.BaseStream);

            writer.Write((uint)m_methodDeclTagsDict.Count);
            writer.Flush();
            m_methodDeclCatalogWriter.Flush();
            m_methodDeclCatalogStream.WriteTo(writer.BaseStream);

            writer.Write((uint)m_methodSpecDict.Count);
            writer.Flush();
            m_methodSpecCatalogWriter.Flush();
            m_methodSpecCatalogStream.WriteTo(writer.BaseStream);
        }
    }
}
