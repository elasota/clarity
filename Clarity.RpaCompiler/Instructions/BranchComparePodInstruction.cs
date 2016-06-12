using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public sealed class BranchComparePodInstruction : HighInstruction, IBranchingInstruction
    {
        private HighSsaRegister m_regA;
        private HighSsaRegister m_regB;
        private HighCfgEdge m_equalNode;
        private HighCfgEdge m_notEqualNode;

        public HighSsaRegister SrcA { get { return m_regA; } }
        public HighSsaRegister SrcB { get { return m_regB; } }
        public HighCfgEdge EqualEdge { get { return m_equalNode; } }
        public HighCfgEdge NotEqualEdge { get { return m_notEqualNode; } }

        public override Opcodes Opcode { get { return Opcodes.BranchComparePod; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public BranchComparePodInstruction(CodeLocationTag codeLocation, HighSsaRegister regA, HighSsaRegister regB, HighCfgNodeHandle equalNode, HighCfgNodeHandle notEqualNode)
            : base(codeLocation)
        {
            m_regA = regA;
            m_regB = regB;
            m_equalNode = new HighCfgEdge(this, equalNode);
            m_notEqualNode = new HighCfgEdge(this, notEqualNode);
        }

        public BranchComparePodInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_regA);
            visitor(ref m_regB);
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
            visitor(ref m_equalNode);
            visitor(ref m_notEqualNode);
        }

        protected override HighInstruction CloneImpl()
        {
            return new BranchComparePodInstruction(CodeLocation, m_regA, m_regB, m_equalNode.Dest, m_notEqualNode.Dest);
        }

        public override bool MayThrow { get { return false; } }
    }
}
