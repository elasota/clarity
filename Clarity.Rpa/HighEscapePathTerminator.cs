using System;
using System.IO;

namespace Clarity.Rpa
{
    public class HighEscapePathTerminator
    {
        private uint m_escapePath;
        private HighCfgNodeHandle m_cfgNode;

        public uint EscapePath { get { return m_escapePath; } }
        public HighCfgNodeHandle CfgNode { get { return m_cfgNode; } }

        public HighEscapePathTerminator(uint escapePath, HighCfgNodeHandle cfgNode)
        {
            m_escapePath = escapePath;
            m_cfgNode = cfgNode;
        }

        public void Write(HighFileBuilder fileBuilder, HighRegionBuilder regionBuilder, BinaryWriter writer)
        {
            writer.Write((uint)m_escapePath);
            writer.Write(regionBuilder.IndexCfgNode(m_cfgNode.Value));
        }

        public static HighEscapePathTerminator Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, BinaryReader reader)
        {
            uint escapePath = reader.ReadUInt32();
            HighCfgNodeHandle cfgNode = cfgNodes[reader.ReadUInt32()];

            return new HighEscapePathTerminator(escapePath, cfgNode);
        }
    }
}
