using System;
using Clarity.Rpa;

namespace AssemblyImporter.CppExport
{
    public class CfgOutboundEdge
    {
        public CfgNode SuccessorNode { get; private set; }
        public VType[] OutputValueTypes { get; private set; }
        public SsaRegister[] SurvivingRegs { get; private set; }    // In push order
        public CodeLocationTag CodeLocation { get; private set; }

        public CfgOutboundEdge(CodeLocationTag codeLocation, CfgNode successorNode, CfgOutboundEdgePrototype outboundEdgeProto)
        {
            CodeLocation = codeLocation;
            SurvivingRegs = outboundEdgeProto.OutboundRegs;
            OutputValueTypes = outboundEdgeProto.OutboundTypes;
            SuccessorNode = successorNode;
        }
    }
}
