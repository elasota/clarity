using System.Collections.Generic;

namespace Clarity.Rpa
{
    public class RegionPhiResolver
    {
        private HighSsaRegister[][] m_cfgNodeRegs;
        private HighCfgNodeHandle[] m_cfgNodes;

        public RegionPhiResolver(HighCfgNodeHandle[] cfgNodes)
        {
            m_cfgNodes = cfgNodes;

            List<HighSsaRegister[]> nodeRegs = new List<HighSsaRegister[]>();

            foreach (HighCfgNodeHandle cfgNode in cfgNodes)
                nodeRegs.Add(HighRegionBuilder.CollectRegsFromNode(cfgNode.Value));

            m_cfgNodeRegs = nodeRegs.ToArray();
        }

        public HighSsaRegister LookupReg(uint cfgNodeIndex, uint regIndex)
        {
            return m_cfgNodeRegs[cfgNodeIndex][regIndex];
        }

        public HighCfgNodeHandle LookupPredecessor(uint cfgNodeIndex)
        {
            return m_cfgNodes[cfgNodeIndex];
        }
    }
}
