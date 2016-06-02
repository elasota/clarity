using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighUnresolvedPhiCollection
    {
        private class UnresolvedLink
        {
            private bool m_isConstant;
            private HighSsaRegister m_constantReg;
            private uint m_predecessorIndex;
            private uint m_linkIndex;

            public bool IsConstant { get { return m_isConstant; } }
            public HighSsaRegister ConstantReg { get { return m_constantReg; } }
            public uint PredecessorIndex { get { return m_predecessorIndex; } }
            public uint LinkIndex { get { return m_linkIndex; } }

            private UnresolvedLink(bool isConstant, HighSsaRegister constantReg, uint predecessorIndex, uint linkIndex)
            {
                m_isConstant = isConstant;
                m_constantReg = constantReg;
                m_predecessorIndex = predecessorIndex;
                m_linkIndex = linkIndex;
            }

            public static UnresolvedLink Read(TagRepository rpa, CatalogReader catalog, uint predecessorIndex, BinaryReader reader)
            {
                bool isConstant = reader.ReadBoolean();
                HighSsaRegister constantReg;
                uint linkIndex;
                if (isConstant)
                {
                    constantReg = HighSsaRegister.ReadConstant(rpa, catalog, reader);
                    linkIndex = 0;
                }
                else
                {
                    constantReg = null;
                    linkIndex = reader.ReadUInt32();
                }

                return new UnresolvedLink(isConstant, constantReg, predecessorIndex, linkIndex);
            }
        }

        private UnresolvedLink[] m_links;

        private HighUnresolvedPhiCollection(UnresolvedLink[] links)
        {
            m_links = links;
        }

        public static HighUnresolvedPhiCollection Read(TagRepository rpa, CatalogReader catalog, HighCfgNodeHandle[] cfgNodes, uint[] predecessors, BinaryReader reader)
        {
            int numLinks = predecessors.Length;

            List<UnresolvedLink> links = new List<UnresolvedLink>();
            foreach (uint pred in predecessors)
            {
                UnresolvedLink link = UnresolvedLink.Read(rpa, catalog, pred, reader);
                links.Add(link);
            }

            return new HighUnresolvedPhiCollection(links.ToArray());
        }

        public HighPhiLink[] Resolve(RegionPhiResolver phiResolver)
        {
            List<HighPhiLink> phiLinks = new List<HighPhiLink>();

            foreach (UnresolvedLink link in m_links)
            {
                HighCfgNodeHandle predecessor = phiResolver.LookupPredecessor(link.PredecessorIndex);
                HighSsaRegister reg;
                if (link.IsConstant)
                    reg = link.ConstantReg;
                else
                    reg = phiResolver.LookupReg(link.PredecessorIndex, link.LinkIndex);

                phiLinks.Add(new HighPhiLink(predecessor, reg));
            }

            return phiLinks.ToArray();
        }
    }
}
