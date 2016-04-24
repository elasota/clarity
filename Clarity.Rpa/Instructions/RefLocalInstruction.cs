using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class RefLocalInstruction : HighInstruction, ILocalUsingInstruction
    {
        private HighSsaRegister m_dest;
        private HighLocal m_local;

        public RefLocalInstruction()
        {
        }

        public RefLocalInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighLocal local)
            : base(codeLocation)
        {
            m_dest = dest;
            m_local = local;
        }

        public override Opcodes Opcode { get { return Opcodes.RefLocal; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
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
            return new RefLocalInstruction(CodeLocation, m_dest, m_local);
        }
    }
}
