
using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class BranchCompareRefsInstruction : HighInstruction, IBranchingInstruction
    {
        private HighSsaRegister m_regA;
        private HighSsaRegister m_regB;
        private HighCfgEdge m_equalNode;
        private HighCfgEdge m_notEqualNode;

        public override Opcodes Opcode { get { return Opcodes.BranchCompareRefs; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public BranchCompareRefsInstruction(CodeLocationTag codeLocation, HighSsaRegister regA, HighSsaRegister regB, HighCfgNodeHandle equalNode, HighCfgNodeHandle notEqualNode)
            : base(codeLocation)
        {
            m_regA = regA;
            m_regB = regB;
            m_equalNode = new HighCfgEdge(this, equalNode);
            m_notEqualNode = new HighCfgEdge(this, notEqualNode);
        }

        public BranchCompareRefsInstruction()
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

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            visitor(ref m_equalNode);
            visitor(ref m_notEqualNode);
        }

        public override HighInstruction Clone()
        {
            return new BranchCompareRefsInstruction(CodeLocation, m_regA, m_regB, m_equalNode.Dest, m_notEqualNode.Dest);
        }
    }
}
