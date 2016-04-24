using System;
using System.Collections.Generic;
using Clarity.Rpa;
using Clarity.Rpa.Instructions;

namespace Clarity.RpaCompiler
{
    public abstract class RloPerNodePass
    {
        private HashSet<HighCfgNode> m_alreadyQueuedNodesSet = new HashSet<HighCfgNode>();
        private Queue<HighCfgNode> m_queuedNodes = new Queue<HighCfgNode>();
        private Queue<HighCfgNode> m_finalQueue = new Queue<HighCfgNode>();
        private RloMethodBody m_methodBody;
        private Compiler m_compiler;

        public RloMethodBody MethodBody { get { return m_methodBody; } }
        public Compiler Compiler { get { return m_compiler; } }

        public RloPerNodePass(Compiler compiler, RloMethodBody methodBody)
        {
            m_methodBody = methodBody;
            m_compiler = compiler;
        }

        public void Run()
        {
            QueueNode(m_methodBody.EntryNode.Value);

            HighInstruction.VisitCfgEdgeDelegate edgeVisitor = delegate (ref HighCfgEdge cfgEdge)
            {
                QueueNode(cfgEdge.Dest.Value);
            };
            HighInstruction.VisitCfgNodeDelegate nodeVisitor = delegate (ref HighCfgNodeHandle cfgNode)
            {
                QueueNode(cfgNode.Value);
            };

            while (m_queuedNodes.Count > 0)
            {
                HighCfgNode cfgNode = m_queuedNodes.Dequeue();

                foreach (HighInstruction instr in cfgNode.Instructions)
                {
                    EnterProtectedBlockInstruction epbInstr = instr as EnterProtectedBlockInstruction;

                    IBranchingInstruction branching = instr as IBranchingInstruction;
                    if (branching != null)
                        branching.VisitSuccessors(edgeVisitor);

                    if (epbInstr != null)
                    {
                        HighEHCluster cluster = epbInstr.EHCluster;
                        foreach (HighEscapePathTerminator terminator in cluster.EscapePathTerminators)
                            QueueNode(terminator.CfgNode.Value);
                        cluster.ProtectedRegion.VisitCfgNodes(nodeVisitor);
                    }
                }
            }

            m_alreadyQueuedNodesSet = null;
            m_queuedNodes = null;

            while (m_finalQueue.Count > 0)
            {
                HighCfgNode cfgNode = m_finalQueue.Dequeue();
                ProcessNode(cfgNode);
            }

            m_finalQueue = null;

            FinalizePass();
        }

        public void QueueNode(HighCfgNode cfgNode)
        {
            if (!m_alreadyQueuedNodesSet.Add(cfgNode))
                return;
            m_queuedNodes.Enqueue(cfgNode);
            m_finalQueue.Enqueue(cfgNode);
        }

        protected abstract void ProcessNode(HighCfgNode cfgNode);

        protected virtual void FinalizePass()
        {
        }
    }
}
