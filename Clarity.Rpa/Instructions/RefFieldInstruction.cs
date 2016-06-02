using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class RefFieldInstruction : HighInstruction, IExtractableTypesInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;
        private TypeSpecTag m_type;
        private string m_field;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public TypeSpecTag Type { get { return m_type; } }
        public string FieldName { get { return m_field; } }

        public override Opcodes Opcode { get { return Opcodes.RefField; } }

        public RefFieldInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, string field, TypeSpecTag type)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_field = field;
            m_type = type;
        }

        public RefFieldInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_src);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexString(m_field));
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_field = catalog.GetString(reader.ReadUInt32());
        }

        public override HighInstruction Clone()
        {
            return new RefFieldInstruction(CodeLocation, m_dest, m_src, m_field, m_type);
        }

        void IExtractableTypesInstruction.ExtractSsaTypes()
        {
            m_type = m_src.Type;
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }
    }
}
