using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class BranchInstruction : HighInstruction, IBranchingInstruction
    {
        private HighCfgEdge m_target;

        public BranchInstruction()
        {
        }

        public BranchInstruction(CodeLocationTag codeLocation, HighCfgNodeHandle target)
            : base(codeLocation)
        {
            m_target = new HighCfgEdge(this, target);
        }

        public override Opcodes Opcode { get { return Opcodes.Branch; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
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

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            visitor(ref m_target);
        }

        protected override HighInstruction CloneImpl()
        {
            return new BranchInstruction(CodeLocation, m_target.Dest);
        }

        public override bool MayThrow { get { return false; } }
    }
}
