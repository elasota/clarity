using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class RloRoutedBranchInstruction : HighInstruction, IBranchingInstruction
    {
        private int m_routeID;
        private HighCfgEdge m_destination;

        public override Opcodes Opcode { get { return Opcodes.RloRoutedBranch; } }

        public RloRoutedBranchInstruction()
        {
        }

        public RloRoutedBranchInstruction(CodeLocationTag codeLocation, int routeID, HighCfgNode destination)
            : base(codeLocation)
        {
            m_routeID = routeID;
            m_destination = new HighCfgEdge(this, new HighCfgNodeHandle(destination));
        }

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            visitor(ref m_destination);
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(m_routeID);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_routeID.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_routeID = reader.ReadInt32();
        }

        protected override HighInstruction CloneImpl()
        {
            return new RloRoutedBranchInstruction(this.CodeLocation, m_routeID, m_destination.Dest.Value);
        }

        public override bool MayThrow { get { return false; } }
        public override bool TerminatesControlFlow { get { return true; } }
    }
}
