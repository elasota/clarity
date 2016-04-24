using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighEHCluster
    {
        private HighEscapePathTerminator[] m_escapePathTerminators;
        private HighProtectedRegion m_protRegion;

        public HighEscapePathTerminator[] EscapePathTerminators { get { return m_escapePathTerminators; } }
        public HighProtectedRegion ProtectedRegion { get { return m_protRegion; } }

        public HighEHCluster(HighProtectedRegion protRegion, HighEscapePathTerminator[] escapePathTerminators)
        {
            m_protRegion = protRegion;
            m_escapePathTerminators = escapePathTerminators;
        }

        public void Write(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((uint)m_escapePathTerminators.Length);

            foreach (HighEscapePathTerminator terminator in m_escapePathTerminators)
                terminator.Write(fileBuilder, regionBuilder, writer);

            m_protRegion.Write(fileBuilder, methodBuilder, haveDebugInfo, writer);
        }

        public static HighEHCluster Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            uint numTerminators = reader.ReadUInt32();

            HighEscapePathTerminator[] escapePathTerminators = new HighEscapePathTerminator[numTerminators];
            for (uint i = 0; i < numTerminators; i++)
            {
                HighEscapePathTerminator terminator = HighEscapePathTerminator.Read(rpa, catalog, methodBody, cfgNodes, reader);
                escapePathTerminators[i] = terminator;
            }

            HighProtectedRegion protRegion = HighProtectedRegion.Read(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);

            return new HighEHCluster(protRegion, escapePathTerminators);
        }
    }
}
