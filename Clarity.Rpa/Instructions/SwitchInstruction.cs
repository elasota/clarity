using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class SwitchInstruction : HighInstruction, IBranchingInstruction
    {
        private HighSsaRegister m_value;
        private HighCfgEdge[] m_cases;
        private HighCfgEdge m_defaultCase;

        public HighSsaRegister Value { get { return m_value; } }
        public HighCfgEdge[] Cases { get { return m_cases; } }
        public HighCfgEdge DefaultCase { get { return m_defaultCase; } }

        public override Opcodes Opcode { get { return Opcodes.Switch; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public SwitchInstruction(CodeLocationTag codeLocation, HighSsaRegister value, HighCfgNodeHandle[] cases, HighCfgNodeHandle defaultCase)
            : base(codeLocation)
        {
            int numCases = cases.Length;
            HighCfgEdge[] caseEdges = new HighCfgEdge[numCases];
            for (int i = 0; i < numCases; i++)
                caseEdges[i] = new HighCfgEdge(this, cases[i]);

            m_cases = caseEdges;
            m_value = value;
            m_defaultCase = new HighCfgEdge(this, defaultCase);
        }

        public SwitchInstruction()
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
            writer.Write((uint)m_cases.Length);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_cases.Length.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_cases = new HighCfgEdge[reader.ReadUInt32()];
        }

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            HighCfgEdge[] cases = m_cases;
            int len = cases.Length;
            for (int i = 0; i < len; i++)
                visitor(ref cases[i]);
            visitor(ref m_defaultCase);
        }

        protected override HighInstruction CloneImpl()
        {
            int numCases = m_cases.Length;
            HighCfgNodeHandle[] cases = new HighCfgNodeHandle[numCases];
            for (int i = 0; i < numCases; i++)
                cases[i] = m_cases[i].Dest;

            return new SwitchInstruction(CodeLocation, m_value, cases, m_defaultCase.Dest);
        }

        public override bool MayThrow { get { return false; } }
    }
}
