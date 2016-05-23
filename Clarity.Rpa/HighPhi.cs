using System;
using System.IO;

namespace Clarity.Rpa
{
    public class HighPhi : ISsaEmitter, ISsaUser
    {
        private HighSsaRegister m_dest;
        private HighPhiLink[] m_links;

        private HighUnresolvedPhiCollection m_unresolvedCollection;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighPhiLink[] Links { get { return m_links; } }

        public HighPhi(HighSsaRegister dest, HighPhiLink[] links)
        {
            if (dest.IsConstant)
                throw new ArgumentException("Can't use a constant as a phi destination");
            m_dest = dest;
            m_links = links;
        }

        public HighPhi(HighSsaRegister dest, HighUnresolvedPhiCollection unresolvedCollection)
        {
            m_dest = dest;
            m_unresolvedCollection = unresolvedCollection;
        }

        public void Write(HighFileBuilder fileBuilder, HighRegionBuilder regionBuilder, BinaryWriter writer)
        {
            m_dest.WriteDestinationDef(fileBuilder, regionBuilder, writer);

            writer.Write((uint)m_links.Length);

            foreach (HighPhiLink link in m_links)
                link.Write(fileBuilder, regionBuilder, writer);
        }

        public static HighPhi Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, BinaryReader reader)
        {
            HighSsaRegister dest = HighSsaRegister.ReadDestinationDef(rpa, catalog, reader);
            if (dest == null)
                throw new Rpa.RpaLoadException("Phi has no destination");

            HighUnresolvedPhiCollection unresolvedCollection = HighUnresolvedPhiCollection.Read(rpa, catalog, cfgNodes, reader);

            return new HighPhi(dest, unresolvedCollection);
        }

        public void Resolve(RegionPhiResolver phiResolver)
        {
            if (m_unresolvedCollection == null)
                throw new Exception("Phi collection has already been resolved");

            m_links = m_unresolvedCollection.Resolve(phiResolver);

            m_unresolvedCollection = null;
        }

        public void VisitSsaDests(HighInstruction.VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public void VisitSsaUses(HighInstruction.VisitSsaDelegate visitor)
        {
            foreach (HighPhiLink phiLink in m_links)
                phiLink.VisitSsaUses(visitor);
        }
    }
}
