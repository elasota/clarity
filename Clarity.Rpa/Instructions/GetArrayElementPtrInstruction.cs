using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class GetArrayElementPtrInstruction : HighInstruction
    {
        private HighSsaRegister m_addrDestReg;
        private HighSsaRegister m_arrayReg;
        private HighSsaRegister[] m_indexes;

        public HighSsaRegister Dest { get { return m_addrDestReg; } }
        public HighSsaRegister ArraySrc { get { return m_arrayReg; } }
        public HighSsaRegister[] Indexes { get { return m_indexes; } }

        public GetArrayElementPtrInstruction()
        {
        }

        public GetArrayElementPtrInstruction(CodeLocationTag codeLocation, HighSsaRegister addrDestReg, HighSsaRegister arrayReg, HighSsaRegister[] indexes)
            : base(codeLocation)
        {
            m_addrDestReg = addrDestReg;
            m_arrayReg = arrayReg;
            m_indexes = indexes;
        }

        public override Opcodes Opcode { get { return Opcodes.GetArrayElementPtr; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_addrDestReg);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            HighSsaRegister[] indexes = m_indexes;
            int len = indexes.Length;
            for (int i = 0; i < len; i++)
                visitor(ref indexes[i]);
            visitor(ref m_arrayReg);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((uint)m_indexes.Length);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_indexes = new HighSsaRegister[reader.ReadUInt32()];
        }

        public override HighInstruction Clone()
        {
            HighSsaRegister[] indexes = ArrayCloner.Clone<HighSsaRegister>(m_indexes);
            return new GetArrayElementPtrInstruction(CodeLocation, m_addrDestReg, m_arrayReg, indexes);
        }
    }
}