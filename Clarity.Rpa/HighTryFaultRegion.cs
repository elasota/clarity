using System;
using System.IO;

namespace Clarity.Rpa
{
    public sealed class HighTryFaultRegion : HighProtectedRegion
    {
        private HighRegion m_faultRegion;

        public HighRegion FaultRegion { get { return m_faultRegion; } }

        public HighTryFaultRegion(HighRegion tryRegion) : base(tryRegion)
        {
        }

        public HighTryFaultRegion(HighRegion tryRegion, HighRegion faultRegion)
            : base(tryRegion)
        {
            m_faultRegion = faultRegion;
        }

        protected override RegionTypeEnum RegionType { get { return RegionTypeEnum.TryFault; } }

        protected override void WriteHandlers(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            m_faultRegion.Write(fileBuilder, methodBuilder, haveDebugInfo, writer);
        }

        protected override void ReadHandlers(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_faultRegion = HighRegion.Read(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);
        }

        protected override void VisitCfgNodesInternal(HighInstruction.VisitCfgNodeDelegate visitor)
        {
            m_faultRegion.VisitCfgNodes(visitor);
        }
    }
}
