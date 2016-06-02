using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public struct HighPhiLink : ISsaUser
    {
        private HighCfgNodeHandle m_predecessor;
        private HighSsaRegister m_reg;

        public HighCfgNodeHandle Predecessor { get { return m_predecessor; } set { m_predecessor = value; } }
        public HighSsaRegister Reg { get { return m_reg; } }

        public HighPhiLink(HighCfgNodeHandle predecessor, HighSsaRegister reg)
        {
            m_predecessor = predecessor;
            m_reg = reg;
        }

        public void Write(HighFileBuilder fileBuilder, HighRegionBuilder regionBuilder, BinaryWriter writer)
        {
            if (m_reg.IsConstant)
            {
                writer.Write(true);
                m_reg.WriteConstant(fileBuilder, regionBuilder, writer);
            }
            else
            {
                writer.Write(false);
                writer.Write(regionBuilder.FindPredecessorSsaIndex(m_predecessor.Value, m_reg));
            }
        }

        public void VisitSsaUses(HighInstruction.VisitSsaDelegate visitor)
        {
            visitor(ref m_reg);
        }
    }
}
