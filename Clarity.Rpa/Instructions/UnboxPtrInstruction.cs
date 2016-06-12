using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class UnboxPtrInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }

        public UnboxPtrInstruction()
        {
        }

        public UnboxPtrInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
        }

        public override Opcodes Opcode { get { return Opcodes.UnboxPtr; } }

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
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        protected override HighInstruction CloneImpl()
        {
            return new UnboxPtrInstruction(CodeLocation, m_dest, m_src);
        }

        public override bool MayThrow { get { return true; } }
    }
}
