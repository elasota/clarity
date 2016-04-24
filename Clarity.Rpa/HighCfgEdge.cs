using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighCfgEdge
    {
        private HighCfgNodeHandle m_dest;
        private HighInstruction m_src;

        public HighCfgNodeHandle Dest { get { return m_dest; } }
        public HighInstruction Source { get { return m_src; } }

        public HighCfgEdge(HighInstruction src, HighCfgNodeHandle dest)
        {
            m_src = src;
            m_dest = dest;
        }
    }
}
