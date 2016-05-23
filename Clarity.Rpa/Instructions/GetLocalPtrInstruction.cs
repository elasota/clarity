using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class GetLocalPtrInstruction : HighInstruction, ILocalUsingInstruction
    {
        private HighLocal m_local;
        private HighSsaRegister m_dest;

        public HighLocal Local { get { return m_local; } }
        public HighSsaRegister Dest { get { return m_dest; } }

        public override Opcodes Opcode { get { return Opcodes.GetLocalPtr; } }

        public GetLocalPtrInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighLocal local)
            : base(codeLocation)
        {
            m_dest = dest;
            m_local = local;
        }

        public GetLocalPtrInstruction()
        {
        }

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
            return new GetLocalPtrInstruction(CodeLocation, m_dest, m_local);
        }
    }
}
