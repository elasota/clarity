using System;
using System.IO;

namespace Clarity.Rpa
{
    public sealed class HighTryFinallyRegion : HighProtectedRegion
    {
        private HighRegion m_finallyRegion;

        public HighRegion FinallyRegion { get { return m_finallyRegion; } }

        public HighTryFinallyRegion(HighRegion tryRegion) : base(tryRegion)
        {
        }

        public HighTryFinallyRegion(HighRegion tryRegion, HighRegion finallyRegion)
            : base(tryRegion)
        {
            m_finallyRegion = finallyRegion;
        }

        protected override RegionTypeEnum RegionType { get { return RegionTypeEnum.TryFinally; } }

        protected override void WriteHandlers(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            m_finallyRegion.Write(fileBuilder, methodBuilder, haveDebugInfo, writer);
        }

        protected override void ReadHandlers(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_finallyRegion = HighRegion.Read(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);
        }

        protected override void VisitCfgNodesInternal(HighInstruction.VisitCfgNodeDelegate visitor)
        {
            m_finallyRegion.VisitCfgNodes(visitor);
        }
    }
}
