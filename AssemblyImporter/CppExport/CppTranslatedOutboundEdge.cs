using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyImporter.CppExport
{
    public class CppTranslatedOutboundEdge
    {
        private Clarity.Rpa.HighCfgNodeHandle m_prevNode;
        private Clarity.Rpa.HighCfgNodeHandle m_nextNode;
        private IList<Clarity.Rpa.HighSsaRegister> m_regs;

        public Clarity.Rpa.HighCfgNodeHandle PrevNode { get { return m_prevNode; } }
        public Clarity.Rpa.HighCfgNodeHandle NextNode { get { return m_nextNode; } }

        public IList<Clarity.Rpa.HighSsaRegister> Regs { get { return m_regs; } }

        public CppTranslatedOutboundEdge(Clarity.Rpa.HighCfgNodeHandle prev, Clarity.Rpa.HighCfgNodeHandle next, IList<Clarity.Rpa.HighSsaRegister> regs)
        {
            m_prevNode = prev;
            m_nextNode = next;
            m_regs = regs;
        }
    }
}
