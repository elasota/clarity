
using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class CompareRefsInstruction : HighInstruction
    {
        private HighSsaRegister m_regA;
        private HighSsaRegister m_regB;
        private HighSsaRegister m_dest;
        private int m_equalValue;
        private int m_notEqualValue;

        public HighSsaRegister SrcA { get { return m_regA; } }
        public HighSsaRegister SrcB { get { return m_regB; } }
        public HighSsaRegister Dest { get { return m_dest; } }
        public int EqualValue { get { return m_equalValue; } }
        public int NotEqualValue { get { return m_notEqualValue; } }

        public override Opcodes Opcode { get { return Opcodes.CompareRefs; } }

        public CompareRefsInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister regA, HighSsaRegister regB, int equalValue, int notEqualValue)
            : base(codeLocation)
        {
            m_dest = dest;
            m_regA = regA;
            m_regB = regB;
            m_equalValue = equalValue;
            m_notEqualValue = notEqualValue;
        }

        public CompareRefsInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_regA);
            visitor(ref m_regB);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(m_equalValue);
            writer.Write(m_notEqualValue);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_equalValue = reader.ReadInt32();
            m_notEqualValue = reader.ReadInt32();
        }

        public override HighInstruction Clone()
        {
            return new CompareRefsInstruction(CodeLocation, m_dest, m_regA, m_regB, m_equalValue, m_notEqualValue);
        }
    }
}
