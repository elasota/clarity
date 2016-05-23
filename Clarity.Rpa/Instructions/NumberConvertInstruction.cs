using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class NumberConvertInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;
        private bool m_checkOverflow;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public bool CheckOverflow { get { return m_checkOverflow; } }

        public override Opcodes Opcode { get { return Opcodes.NumberConvert; } }

        public NumberConvertInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, bool checkOverflow)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_checkOverflow = checkOverflow;
        }

        public NumberConvertInstruction()
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
            writer.Write(m_checkOverflow);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_checkOverflow = reader.ReadBoolean();
        }

        public override HighInstruction Clone()
        {
            return new NumberConvertInstruction(CodeLocation, m_dest, m_src, m_checkOverflow);
        }
    }
}
