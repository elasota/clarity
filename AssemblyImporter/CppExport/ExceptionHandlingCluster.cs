using System;
using System.Collections.Generic;

namespace AssemblyImporter.CppExport
{
    public class ExceptionHandlingCluster
    {
        public enum ClusterTypeEnum
        {
            Unknown,
            TryCatch,
            TryFinally,
            TryFault,
        }

        public IEnumerable<uint> EscapePaths { get { return m_escapePaths; } }
        public ExceptionHandlingRegion TryRegion { get { return m_tryRegion; } }
        public ExceptionHandlingRegion[] ExceptionHandlingRegions { get { return m_exceptionHandlingRegions; } }
        public ClusterTypeEnum ClusterType { get { return m_clusterType; } }

        private uint m_checkInstr;
        private ExceptionHandlingRegion[] m_exceptionHandlingRegions;
        private ExceptionHandlingRegion m_tryRegion;
        private bool m_isParsed;
        private SortedSet<uint> m_escapePaths;
        private ClusterTypeEnum m_clusterType;

        public ExceptionHandlingCluster(ClusterTypeEnum clusterType, uint checkInstr, ExceptionHandlingRegion tryRegion, ExceptionHandlingRegion[] exceptionHandlingRegions)
        {
            m_clusterType = clusterType;
            m_checkInstr = checkInstr;
            m_tryRegion = tryRegion;
            m_exceptionHandlingRegions = exceptionHandlingRegions;
            m_isParsed = false;
            m_escapePaths = new SortedSet<uint>();

            tryRegion.ContainingCluster = this;
            foreach (ExceptionHandlingRegion region in exceptionHandlingRegions)
                region.ContainingCluster = this;
        }

        public void Parse(ExceptionHandlingRegion ownerRegion, CfgBuilder ownerBuilder)
        {
            if (m_isParsed)
                return;

            List<ExceptionHandlingRegion> allRegions = new List<ExceptionHandlingRegion>();
            allRegions.Add(m_tryRegion);
            foreach (ExceptionHandlingRegion ehRegion in m_exceptionHandlingRegions)
                allRegions.Add(ehRegion);

            foreach (ExceptionHandlingRegion region in allRegions)
            {
                CfgBuilder builder = new CfgBuilder(region, ownerBuilder.CppBuilder, ownerBuilder.Class, ownerBuilder.CppMethod, ownerBuilder.Args, ownerBuilder.Locals);
                region.RootCfgNode = builder.RootNode;

                foreach (uint escapePath in region.EscapePaths)
                    m_escapePaths.Add(escapePath);
            }

            m_isParsed = true;
        }
    }
}
