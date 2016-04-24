using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class BranchCompareNumbersInstruction : HighInstruction, IBranchingInstruction
    {
        private NumberCompareOperation m_operation;
        private HighCfgEdge m_trueNode;
        private HighCfgEdge m_falseNode;
        private HighSsaRegister m_left;
        private HighSsaRegister m_right;
        private NumberArithType m_numberType;

        public override Opcodes Opcode { get { return Opcodes.BranchCompareNumbers; } }

        public override bool TerminatesControlFlow { get { return true; } }

        public BranchCompareNumbersInstruction(CodeLocationTag codeLocation, NumberCompareOperation operation, NumberArithType numberType, HighSsaRegister left, HighSsaRegister right, HighCfgNodeHandle trueNode, HighCfgNodeHandle falseNode)
            : base(codeLocation)
        {
            m_operation = operation;
            m_trueNode = new HighCfgEdge(this, trueNode);
            m_falseNode = new HighCfgEdge(this, falseNode);
            m_left = left;
            m_right = right;
            m_numberType = numberType;
        }

        public BranchCompareNumbersInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_left);
            visitor(ref m_right);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((byte)m_operation);
            writer.Write((byte)m_numberType);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_operation = (NumberCompareOperation)reader.ReadByte();
            if (m_operation < 0 || m_operation >= NumberCompareOperation.NumHighCompareTypes)
                throw new Exception("Invalid compare operation");

            m_numberType = (NumberArithType)reader.ReadByte();
            if (m_numberType < 0 || m_numberType >= NumberArithType.NumHighArithTypes)
                throw new Exception("Invalid arith type");
        }

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            visitor(ref m_trueNode);
            visitor(ref m_falseNode);
        }

        public override HighInstruction Clone()
        {
            return new BranchCompareNumbersInstruction(CodeLocation, m_operation, m_numberType, m_left, m_right, m_trueNode.Dest, m_falseNode.Dest);
        }
    }
}
