using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class LoadValueFieldInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;
        private string m_fieldName;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public string FieldName { get { return m_fieldName; } }

        public override Opcodes Opcode { get { return Opcodes.LoadValueField; } }

        public LoadValueFieldInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, string fieldName)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_fieldName = fieldName;
        }

        public LoadValueFieldInstruction()
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
            writer.Write(fileBuilder.IndexString(m_fieldName));
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_fieldName = catalog.GetString(reader.ReadUInt32());
        }

        public override HighInstruction Clone()
        {
            return new LoadValueFieldInstruction(CodeLocation, m_dest, m_src, m_fieldName);
        }
    }
}
