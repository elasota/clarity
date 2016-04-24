using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighCfgNodeBuilder
    {
        private Dictionary<HighSsaRegister, uint> m_regIDs;
        private uint m_numRegs;

        public HighCfgNodeBuilder()
        {
            m_regIDs = new Dictionary<HighSsaRegister, uint>();
            m_numRegs = 0;
        }

        public uint GetSsaID(HighSsaRegister reg)
        {
            return m_regIDs[reg];
        }

        public void AddSsa(HighSsaRegister ssaReg)
        {
            m_regIDs.Add(ssaReg, m_numRegs++);
        }
    }
}
