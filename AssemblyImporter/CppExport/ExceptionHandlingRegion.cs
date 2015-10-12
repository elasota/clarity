using System;
using System.Collections.Generic;
using AssemblyImporter.CLR.CIL;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class ExceptionHandlingRegion
    {
        public uint StartInstr { get { return m_startInstr; } }
        public uint EndInstr { get { return m_endInstr; } }
        public IDictionary<uint, ExceptionHandlingCluster> Clusters { get { return m_clusters; } }
        public CLRTypeSpec ExceptionType { get { return m_exceptionType; } }
        public ExceptionHandlingCluster ContainingCluster { get; set; }
        public ExceptionHandlingRegion ContainingRegion { get { return m_containingRegion; } }
        public IEnumerable<uint> EscapePaths { get { return m_escapePaths; } }
        public IDictionary<uint, CfgNode> EscapeTerminators { get { return m_escapeTerminators; } }

        public CfgNode RootCfgNode { get; set; }

        private uint m_startInstr;
        private uint m_endInstr;
        private Dictionary<uint, ExceptionHandlingCluster> m_clusters;
        private CLRTypeSpec m_exceptionType;
        private ExceptionHandlingRegion m_containingRegion;
        private SortedSet<uint> m_escapePaths;
        private SortedList<uint, CfgNode> m_escapeTerminators;

        private static void BlockOutRegion(bool[] blockedOut, uint start, uint end)
        {
            for (uint i = start; i <= end; i++)
                blockedOut[i] = true;
        }

        public ExceptionHandlingRegion(ExceptionHandlingRegion containingRegion, CppBuilder builder, CppMethod method, uint startInstr, uint endInstr, CLRTypeSpec exceptionType)
        {
            m_startInstr = startInstr;
            m_endInstr = endInstr;
            m_clusters = new Dictionary<uint, ExceptionHandlingCluster>();
            m_containingRegion = containingRegion;
            m_exceptionType = exceptionType;
            m_escapePaths = new SortedSet<uint>();
            m_escapeTerminators = new SortedList<uint, CfgNode>();

            HLInstruction[] instrs = method.MethodDef.Method.Instructions;
            MethodDataSection[] sections = method.MethodDef.Method.Sections;
            List<MethodEHClause> ehClauses = new List<MethodEHClause>();

            bool[] blockedOutInstr = new bool[instrs.Length];
            if (sections != null)
            {
                foreach (MethodDataSection section in sections)
                {
                    if (section is MethodEHSection)
                    {
                        foreach (MethodEHClause clause in ((MethodEHSection)section).Clauses)
                            ehClauses.Add(clause);
                    }
                }
            }

            for (uint checkInstr = startInstr; checkInstr <= endInstr; checkInstr++)
            {
                if (blockedOutInstr[checkInstr])
                    continue;

                MethodEHClause largestMatch = null;
                foreach (MethodEHClause ehClause in ehClauses)
                {
                    // Ignore the current clause and any clauses enclosing it
                    if (ehClause.TryOffset == (uint)startInstr && (ehClause.TryOffset + ehClause.TryLength - 1) >= (uint)endInstr)
                        continue;

                    if (ehClause.TryOffset == (uint)checkInstr)
                    {
                        if (largestMatch == null || ehClause.TryLength > largestMatch.TryLength)
                            largestMatch = ehClause;
                    }
                }

                if (largestMatch != null)
                {
                    List<ExceptionHandlingRegion> handlers = new List<ExceptionHandlingRegion>();

                    uint tryStart = largestMatch.TryOffset;
                    uint tryEnd = tryStart + largestMatch.TryLength - 1;
                    ExceptionHandlingRegion tryRegion = new ExceptionHandlingRegion(this, builder, method, tryStart, tryEnd, null);

                    BlockOutRegion(blockedOutInstr, tryStart, tryEnd);

                    ExceptionHandlingCluster.ClusterTypeEnum clusterType = ExceptionHandlingCluster.ClusterTypeEnum.Unknown;

                    // Find all matches
                    foreach (MethodEHClause ehClause in ehClauses)
                    {
                        if (ehClause.TryOffset != largestMatch.TryOffset || ehClause.TryLength != largestMatch.TryLength)
                            continue;

                        uint handlerStart = ehClause.HandlerOffset;
                        uint handlerEnd = handlerStart + ehClause.HandlerLength - 1;
                        BlockOutRegion(blockedOutInstr, handlerStart, handlerEnd);

                        CLRTypeSpec exType = null;
                        if (ehClause.ClauseType == MethodEHClause.ExceptionClauseType.Fault)
                        {
                            if (clusterType != ExceptionHandlingCluster.ClusterTypeEnum.Unknown)
                                throw new ParseFailedException("Malformed exception handling stucture: Mixed fault");
                            clusterType = ExceptionHandlingCluster.ClusterTypeEnum.TryFault;
                        }
                        else if (ehClause.ClauseType == MethodEHClause.ExceptionClauseType.Finally)
                        {
                            if (clusterType != ExceptionHandlingCluster.ClusterTypeEnum.Unknown)
                                throw new ParseFailedException("Malformed exception handling stucture: Mixed finally");
                            clusterType = ExceptionHandlingCluster.ClusterTypeEnum.TryFinally;
                        }
                        else if (ehClause.ClauseType == MethodEHClause.ExceptionClauseType.Exception)
                        {
                            if (clusterType != ExceptionHandlingCluster.ClusterTypeEnum.Unknown && clusterType != ExceptionHandlingCluster.ClusterTypeEnum.TryCatch)
                                throw new ParseFailedException("Malformed exception handling stucture: Mixed exception");
                            clusterType = ExceptionHandlingCluster.ClusterTypeEnum.TryCatch;
                            exType = builder.ResolveTypeDefOrRefOrSpec(ehClause.ClassToken);
                        }
                        else if (ehClause.ClauseType == MethodEHClause.ExceptionClauseType.Filter)
                            throw new NotImplementedException("Filter exceptions are not implemented yet");
                        else
                            throw new ArgumentException();

                        handlers.Add(new ExceptionHandlingRegion(this, builder, method, handlerStart, handlerEnd, exType));
                    }

                    ExceptionHandlingCluster cluster = new ExceptionHandlingCluster(clusterType, checkInstr, tryRegion, handlers.ToArray());

                    m_clusters.Add(checkInstr, cluster);
                }
            }
        }

        public void AddEscapePath(uint escapeInstr)
        {
            m_escapePaths.Add(escapeInstr);
        }

        public void AddLeaveTarget(uint escapePath, CfgNode targetNode)
        {
            m_escapeTerminators.Add(escapePath, targetNode);
        }
    }
}
