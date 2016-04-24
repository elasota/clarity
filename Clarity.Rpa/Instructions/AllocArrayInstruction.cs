using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class AllocArrayInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister[] m_indexes;

        public AllocArrayInstruction()
        {
        }

        public AllocArrayInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister[] indexes)
            : base(codeLocation)
        {
            m_dest = dest;
            m_indexes = indexes;
        }

        public override Opcodes Opcode { get { return Opcodes.AllocArray; } }

        public override HighInstruction Clone()
        {
            return new AllocArrayInstruction(CodeLocation, m_dest, ArrayCloner.Clone<HighSsaRegister>(m_indexes));
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_indexes = new HighSsaRegister[reader.ReadUInt32()];
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            HighSsaRegister[] indexes = m_indexes;
            int len = m_indexes.Length;
            for (int i = 0; i < len; i++)
                visitor(ref indexes[i]);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((uint)m_indexes.Length);
        }
    }
}
