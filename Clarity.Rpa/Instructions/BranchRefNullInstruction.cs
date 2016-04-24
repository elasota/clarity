using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class BranchRefNullInstruction : HighInstruction, IBranchingInstruction
    {
        private HighSsaRegister m_value;
        private HighCfgEdge m_isNullNode;
        private HighCfgEdge m_isNotNullNode;

        public override Opcodes Opcode { get { return Opcodes.BranchRefNull; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public BranchRefNullInstruction(CodeLocationTag codeLocation, HighSsaRegister value, HighCfgNodeHandle isNullNode, HighCfgNodeHandle isNotNullNode)
            : base(codeLocation)
        {
            m_value = value;
            m_isNullNode = new HighCfgEdge(this, isNullNode);
            m_isNotNullNode = new HighCfgEdge(this, isNotNullNode);
        }

        public BranchRefNullInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_value);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            visitor(ref m_isNullNode);
            visitor(ref m_isNotNullNode);
        }

        public override HighInstruction Clone()
        {
            return new BranchRefNullInstruction(CodeLocation, m_value, m_isNullNode.Dest, m_isNotNullNode.Dest);
        }
    }
}
