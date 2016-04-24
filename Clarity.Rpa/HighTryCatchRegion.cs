using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public sealed class HighTryCatchRegion : HighProtectedRegion
    {
        private HighCatchHandler[] m_catchHandlers;

        public HighCatchHandler[] CatchHandlers { get { return m_catchHandlers; } }

        public HighTryCatchRegion(HighRegion tryRegion) : base(tryRegion)
        {
        }

        public HighTryCatchRegion(HighRegion tryRegion, HighCatchHandler[] catchHandlers)
            : base(tryRegion)
        {
            m_catchHandlers = catchHandlers;
        }

        protected override RegionTypeEnum RegionType { get { return RegionTypeEnum.TryCatch; } }

        protected override void WriteHandlers(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((uint)m_catchHandlers.Length);
            foreach (HighCatchHandler catchHandler in m_catchHandlers)
                catchHandler.Write(fileBuilder, methodBuilder, haveDebugInfo, writer);
        }

        protected override void ReadHandlers(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            uint numCatchHandlers = reader.ReadUInt32();
            m_catchHandlers = new HighCatchHandler[numCatchHandlers];

            for (uint i = 0; i < numCatchHandlers; i++)
            {
                HighCatchHandler hdl = new HighCatchHandler();
                hdl.Read(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);
                m_catchHandlers[i] = hdl;
            }
        }

        protected override void VisitCfgNodesInternal(HighInstruction.VisitCfgNodeDelegate visitor)
        {
            foreach (HighCatchHandler catchHandler in m_catchHandlers)
                catchHandler.Region.VisitCfgNodes(visitor);
        }
    }
}
