using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloFindPredecessorsAndSuccessorsPass : RloPerNodePass
    {
        private Dictionary<HighCfgNode, HashSet<HighCfgNode>> m_predecessors = new Dictionary<HighCfgNode, HashSet<HighCfgNode>>();
        private Dictionary<HighCfgNode, HashSet<HighCfgNode>> m_successors = new Dictionary<HighCfgNode, HashSet<HighCfgNode>>();

        public RloFindPredecessorsAndSuccessorsPass(Compiler compiler, RloMethodBody methodBody)
            : base(compiler, methodBody)
        {
        }

        protected override void ProcessNode(HighCfgNode cfgNode)
        {
            HighInstruction.VisitCfgEdgeDelegate visitor = delegate (ref HighCfgEdge successorEdge)
            {
                HighCfgNode successor = successorEdge.Dest.Value;

                HashSet<HighCfgNode> predSet;
                if (!m_predecessors.TryGetValue(successor, out predSet))
                {
                    predSet = new HashSet<HighCfgNode>();
                    m_predecessors.Add(successor, predSet);
                }
                predSet.Add(cfgNode);

                HashSet<HighCfgNode> successorSet;
                if (!m_successors.TryGetValue(cfgNode, out successorSet))
                {
                    successorSet = new HashSet<HighCfgNode>();
                    m_successors.Add(cfgNode, successorSet);
                }
                successorSet.Add(successor);
            };
 
            foreach (HighInstruction instr in cfgNode.Instructions)
            {
                IBranchingInstruction branching = instr as IBranchingInstruction;
                if (branching != null)
                    branching.VisitSuccessors(visitor);
            }
        }

        public ISet<HighCfgNode> PredecessorsForNode(HighCfgNode cfgNode)
        {
            HashSet<HighCfgNode> preds;
            if (m_predecessors.TryGetValue(cfgNode, out preds))
                return preds;
            return null;
        }

        public ISet<HighCfgNode> SuccessorsForNode(HighCfgNode cfgNode)
        {
            HashSet<HighCfgNode> successors;
            if (m_successors.TryGetValue(cfgNode, out successors))
                return successors;
            return null;
        }
    }
}
