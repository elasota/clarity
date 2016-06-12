using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class LoadMulticastDelegateElementInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_delegateSrc;
        private HighSsaRegister m_indexSrc;

        public LoadMulticastDelegateElementInstruction()
        {
        }

        public LoadMulticastDelegateElementInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister delegateSrc, HighSsaRegister indexSrc)
            : base(codeLocation)
        {
            m_dest = dest;
            m_delegateSrc = delegateSrc;
            m_indexSrc = indexSrc;
        }

        public override Opcodes Opcode { get { return Opcodes.LoadMulticastDelegateElement; } }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_delegateSrc);
            visitor(ref m_indexSrc);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
        }

        protected override HighInstruction CloneImpl()
        {
            return new LoadMulticastDelegateElementInstruction(this.CodeLocation, m_dest, m_delegateSrc, m_indexSrc);
        }

        public override bool MayThrow { get { return false; } }
    }
}
