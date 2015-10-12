using System;
using System.Collections.Generic;

namespace AssemblyImporter.CppExport
{
    public struct CfgOutboundEdgePrototype
    {
        public VType[] OutboundTypes { get { return m_outboundTypes; } }
        public SsaRegister[] OutboundRegs { get { return m_outboundRegs; } }

        private VType[] m_outboundTypes;
        private SsaRegister[] m_outboundRegs;

        public CfgOutboundEdgePrototype(VType[] outboundTypes, SsaRegister[] outboundRegs)
        {
            m_outboundRegs = outboundRegs;
            m_outboundTypes = outboundTypes;
        }
    }
}
