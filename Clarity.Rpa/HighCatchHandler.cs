using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighCatchHandler
    {
        private TypeSpecTag m_catchType;
        private HighRegion m_region;

        public TypeSpecTag CatchType { get { return m_catchType; } }
        public HighRegion Region { get { return m_region; } }

        public HighCatchHandler()
        {
        }

        public HighCatchHandler(TypeSpecTag catchType, HighRegion region)
        {
            m_catchType = catchType;
            m_region = region;
        }

        public void Write(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexTypeSpecTag(m_catchType));
            m_region.Write(fileBuilder, methodBuilder, haveDebugInfo, writer);
        }

        public void Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_catchType = catalog.GetTypeSpec(reader.ReadUInt32());
            m_region = HighRegion.Read(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);
        }
    }
}
