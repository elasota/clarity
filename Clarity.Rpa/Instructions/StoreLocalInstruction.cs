using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class StoreLocalInstruction : HighInstruction, ILocalUsingInstruction
    {
        private HighLocal m_local;
        private HighSsaRegister m_src;

        public HighLocal Local { get { return m_local; } }
        public HighSsaRegister Src { get { return m_src; } }

        public StoreLocalInstruction()
        {
        }

        public StoreLocalInstruction(CodeLocationTag codeLocation, HighLocal local, HighSsaRegister src)
            : base(codeLocation)
        {
            m_local = local;
            m_src = src;
        }

        public override Opcodes Opcode { get { return Opcodes.StoreLocal; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_src);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        void ILocalUsingInstruction.VisitLocalRefs(VisitLocalDelegate visitor)
        {
            visitor(ref m_local);
        }

        public override HighInstruction Clone()
        {
            return new StoreLocalInstruction(CodeLocation, m_local, m_src);
        }
    }
}
