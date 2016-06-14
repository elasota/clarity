using System;
using System.IO;

namespace Clarity.Rpa
{
    public class HighRegion
    {
        private HighCfgNodeHandle m_entryNode;

        public HighCfgNodeHandle EntryNode { get { return m_entryNode; } }

        public HighRegion(HighCfgNodeHandle entryNode)
        {
            m_entryNode = entryNode;
        }

        public void Write(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            HighRegionBuilder regionBuilder = new HighRegionBuilder();
            regionBuilder.IndexCfgNode(m_entryNode.Value);

            MemoryStream regionNodesStream = new MemoryStream();
            BinaryWriter regionNodesWriter = new BinaryWriter(regionNodesStream);

            HighCfgNode cfgNode = regionBuilder.DequeueUnemittedNode();
            while (cfgNode != null)
            {
                cfgNode.Write(fileBuilder, methodBuilder, regionBuilder, haveDebugInfo, regionNodesWriter);

                cfgNode = regionBuilder.DequeueUnemittedNode();
            }

            regionNodesWriter.Flush();

            regionBuilder.WriteHeader(writer);
            writer.Flush();
            regionNodesStream.WriteTo(writer.BaseStream);
        }

        public static HighRegion Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            uint numCfgNodes = reader.ReadUInt32();

            if (numCfgNodes == 0)
                throw new Exception("Region has no CFG nodes");

            HighCfgNodeHandle[] cfgNodes = new HighCfgNodeHandle[numCfgNodes];
            for (uint i = 0; i < numCfgNodes; i++)
                cfgNodes[i] = new HighCfgNodeHandle();

            for (uint i = 0; i < numCfgNodes; i++)
                cfgNodes[i].Value = HighCfgNode.Read(rpa, catalog, methodBody, cfgNodes, baseLocation, haveDebugInfo, reader);

            RegionPhiResolver phiResolver = new RegionPhiResolver(cfgNodes);
            for (uint i = 0; i < numCfgNodes; i++)
            {
                foreach (HighPhi phi in cfgNodes[i].Value.Phis)
                    phi.Resolve(phiResolver);
            }

            HighCfgNodeHandle entryNode = cfgNodes[0];

            if (entryNode.Value.Phis.Length != 0)
                throw new RpaLoadException("Region entry node has phis");

            return new HighRegion(entryNode);
        }

        public void VisitCfgNodes(HighInstruction.VisitCfgNodeDelegate visitor)
        {
            visitor(ref m_entryNode);
        }
    }
}
