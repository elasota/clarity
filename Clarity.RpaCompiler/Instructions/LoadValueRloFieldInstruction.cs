using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class LoadValueRloFieldInstruction : Rpa.HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;
        private uint m_fieldIndex;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public uint FieldIndex { get { return m_fieldIndex; } }

        public override Opcodes Opcode { get { return Opcodes.LoadValueRloField; } }

        public LoadValueRloFieldInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, uint fieldIndex)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_fieldIndex = fieldIndex;
        }

        public override HighInstruction Clone()
        {
            return new LoadValueRloFieldInstruction(this.CodeLocation, m_dest, m_src, m_fieldIndex);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_fieldIndex = reader.ReadUInt32();
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
            writer.Write(m_fieldIndex);
        }
    }
}
