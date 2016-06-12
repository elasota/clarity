using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class GetMulticastDelegateInvocationCountInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_delegateSrc;

        public GetMulticastDelegateInvocationCountInstruction()
        {
        }

        public GetMulticastDelegateInvocationCountInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister delegateSrc)
            : base(codeLocation)
        {
            m_dest = dest;
            m_delegateSrc = delegateSrc;
        }

        public override Opcodes Opcode { get { return Opcodes.GetMulticastDelegateInvocationCount; } }

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
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
        }

        protected override HighInstruction CloneImpl()
        {
            return new GetMulticastDelegateInvocationCountInstruction(this.CodeLocation, m_dest, m_delegateSrc);
        }

        public override bool MayThrow { get { return false; } }
    }
}
