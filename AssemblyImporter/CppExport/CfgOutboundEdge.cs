using System;
using System.Collections.Generic;

namespace AssemblyImporter.CppExport
{
    public class CfgOutboundEdge
    {
        public CfgNode SuccessorNode { get; private set; }
        public VType[] OutputValueTypes { get; private set; }
        public SsaRegister[] SurvivingRegs { get; private set; }    // In push order

        public CfgOutboundEdge(CfgNode successorNode, CfgOutboundEdgePrototype outboundEdgeProto)
        {
            SurvivingRegs = outboundEdgeProto.OutboundRegs;
            OutputValueTypes = outboundEdgeProto.OutboundTypes;
            SuccessorNode = successorNode;
        }
    }
}
