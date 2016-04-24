using System;
using System.IO;

namespace Clarity.Rpa
{
    public abstract class HighProtectedRegion
    {
        protected enum RegionTypeEnum
        {
            TryCatch,
            TryFault,
            TryFinally,
        }

        private HighRegion m_tryRegion;

        public HighRegion TryRegion { get { return m_tryRegion; } }

        public HighProtectedRegion(HighRegion tryRegion)
        {
            m_tryRegion = tryRegion;
        }

        protected abstract void WriteHandlers(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer);
        protected abstract void ReadHandlers(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader);
        protected abstract RegionTypeEnum RegionType { get; }
        protected abstract void VisitCfgNodesInternal(HighInstruction.VisitCfgNodeDelegate visitor);

        public void VisitCfgNodes(HighInstruction.VisitCfgNodeDelegate visitor)
        {
            m_tryRegion.VisitCfgNodes(visitor);
            this.VisitCfgNodesInternal(visitor);
        }

        public void Write(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((byte)this.RegionType);
            m_tryRegion.Write(fileBuilder, methodBuilder, haveDebugInfo, writer);
            WriteHandlers(fileBuilder, methodBuilder, haveDebugInfo, writer);
        }

        public static HighProtectedRegion Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            HighProtectedRegion region;

            RegionTypeEnum regionType = (RegionTypeEnum)reader.ReadByte();

            HighRegion tryRegion = HighRegion.Read(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);

            switch (regionType)
            {
                case RegionTypeEnum.TryCatch:
                    region = new HighTryCatchRegion(tryRegion);
                    break;
                case RegionTypeEnum.TryFault:
                    region = new HighTryFaultRegion(tryRegion);
                    break;
                case RegionTypeEnum.TryFinally:
                    region = new HighTryFinallyRegion(tryRegion);
                    break;
                default:
                    throw new Exception("Invalid protected region type");
            }

            region.ReadHandlers(rpa, catalog, methodBody, baseLocation, haveDebugInfo, reader);

            return region;
        }
    }
}
