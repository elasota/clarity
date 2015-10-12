using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using AssemblyImporter.CLR.CIL;

// .NET allows branches to leave values on the stack, so we have to construct a CFG to determine stack
// value lifetimes and conversion requirements across edges.
//
// The rules for merging stack states are described in III.1.8.1.3, in this order:
// - If the new type can be assigned to the existing type, then the merged type is the existing type
// - If the existing type can be assigned to the new type, then the merged type is the new type
// - If both types are object types, then the merged type is the closest common supertype
//
// "Closest common supertype" does NOT account for interface implementation.
// If two ref types implement the same interface, then the merged type is NOT that interface.
namespace AssemblyImporter.CppExport
{
    public class CfgBuilder
    {
        public CppBuilder CppBuilder { get { return m_builder; } }
        public CppClass Class { get { return m_cls; } }
        public CppMethod CppMethod { get { return m_method; } }
        public VReg[] Args { get { return m_args; } }
        public VReg[] Locals { get { return m_locals; } }
        public HLInstruction[] CilInstructions { get { return m_instrs; } }
        public CLRTypeDefRow InClass { get { return m_inClass; } }
        public CLRMethodDefRow InMethod { get { return m_inMethod; } }
        public IDictionary<uint, ExceptionHandlingCluster> EhClusters { get { return m_ehClusters; } }
        public ExceptionHandlingRegion Region { get { return m_region; } }
        public CfgNode RootNode { get { return m_rootNode; } }

        private CppBuilder m_builder;
        private CppClass m_cls;
        private CppMethod m_method;
        private VReg[] m_args;
        private VReg[] m_locals;
        private HLInstruction[] m_instrs;
        private ExceptionHandlingRegion m_region;
        private IDictionary<uint, ExceptionHandlingCluster> m_ehClusters;

        private Dictionary<int, CfgNode> m_cfgNodes;

        private bool[] m_instrIsBranchTarget;
        
        private HashSet<int> m_pendingNodesSet;
        private Queue<int> m_pendingNodesQueue;
        private CLRTypeDefRow m_inClass;
        private CLRMethodDefRow m_inMethod;

        private int m_startInstr;
        private int m_endInstr;

        private CfgNode m_rootNode;

        public CfgBuilder(ExceptionHandlingRegion region, CppBuilder builder, CppClass cls, CppMethod method, VReg[] args, VReg[] locals)
        {
            m_builder = builder;
            m_cls = cls;
            m_method = method;
            m_args = args;
            m_locals = locals;
            m_instrs = method.MethodDef.Method.Instructions;
            m_inClass = cls.TypeDef;
            m_inMethod = method.MethodDef;
            m_region = region;

            m_startInstr = (int)region.StartInstr;
            m_endInstr = (int)region.EndInstr;
            m_ehClusters = region.Clusters;

            LocateBranchTargets();
            ConstructCfg();
            CreateSuccessionGraph();
        }

        private static void LinkSuccessor(CfgNode node, CfgOutboundEdge outboundEdge)
        {
            if (outboundEdge == null)
                return;
            node.AddSuccessor(outboundEdge);
            outboundEdge.SuccessorNode.AddPredecessor(node);
        }

        private void CreateSuccessionGraph()
        {
            foreach (CfgNode node in m_cfgNodes.Values)
            {
                foreach (MidInstruction minstr in node.MidInstructions)
                {
                    LinkSuccessor(node, minstr.CfgEdgeArg);
                    LinkSuccessor(node, minstr.CfgEdgeArg2);
                    if (minstr.CfgEdgesArg != null)
                        foreach (CfgOutboundEdge edgeArg in minstr.CfgEdgesArg)
                            LinkSuccessor(node, edgeArg);
                }
                LinkSuccessor(node, node.FallThroughEdge);
            }
        }

        public void LocateBranchTargets()
        {
            bool[] knownStarts = new bool[m_instrs.Length];
            Queue<int> unprocessedStarts = new Queue<int>();

            knownStarts[m_startInstr] = true;
            unprocessedStarts.Enqueue(m_startInstr);

            while (unprocessedStarts.Count > 0)
            {
                int startLoc = unprocessedStarts.Dequeue();

                ScanBranchTargets(startLoc, knownStarts, unprocessedStarts);
            }

            m_instrIsBranchTarget = knownStarts;
        }

        private static void AddBranchTarget(int target, bool[] knownStarts, Queue<int> unprocessedStarts)
        {
            if (!knownStarts[target])
            {
                knownStarts[target] = true;
                unprocessedStarts.Enqueue(target);
            }
        }

        private void ScanBranchTargets(int startInstr, bool[] knownStarts, Queue<int> unprocessedStarts)
        {
            int nextInstr = startInstr;

            while (true)
            {
                {
                    ExceptionHandlingCluster cluster;
                    if (m_ehClusters.TryGetValue((uint)nextInstr, out cluster))
                    {
                        cluster.Parse(m_region, this);
                        foreach (uint escapePath in cluster.EscapePaths)
                        {
                            if (escapePath >= m_region.StartInstr && escapePath <= m_region.EndInstr)
                                AddBranchTarget((int)escapePath, knownStarts, unprocessedStarts);
                        }
                        break;
                    }
                }

                HLInstruction instr = m_instrs[nextInstr];
                nextInstr++;

                bool isTerminal = false;

                switch (instr.Opcode)
                {
                    case HLOpcode.beq:
                    case HLOpcode.bge:
                    case HLOpcode.bgt:
                    case HLOpcode.ble:
                    case HLOpcode.blt:
                    case HLOpcode.bne:
                    case HLOpcode.brfalse:
                    case HLOpcode.brtrue:
                        AddBranchTarget((int)instr.Arguments.U32Value, knownStarts, unprocessedStarts);
                        AddBranchTarget(nextInstr, knownStarts, unprocessedStarts);
                        isTerminal = true;
                        break;

                    case HLOpcode.br:
                        AddBranchTarget((int)instr.Arguments.U32Value, knownStarts, unprocessedStarts);
                        isTerminal = true;
                        break;

                    case HLOpcode.leave:
                    case HLOpcode.ret:
                    case HLOpcode.endfinally:
                    case HLOpcode.rethrow:
                    case HLOpcode.@throw:
                        isTerminal = true;
                        break;

                    case HLOpcode.@switch:
                        {
                            isTerminal = true;
                            uint[] targets = (uint[])instr.Arguments.ObjValue;
                            foreach (uint target in targets)
                                AddBranchTarget((int)target, knownStarts, unprocessedStarts);
                            AddBranchTarget(nextInstr, knownStarts, unprocessedStarts);
                        }
                        break;
                    default:
                        break;
                }

                if (isTerminal)
                    break;
            }
        }

        public CfgNode AddCfgTarget(CfgNodeCompiler nodeCompiler, int startInstr, VType[] entryTypes)
        {
            CfgNode node;
            bool needsReparse = false;
            if (m_cfgNodes.TryGetValue(startInstr, out node))
                node.UpdateEntryTypes(nodeCompiler, entryTypes, out needsReparse);
            else
            {
                node = new CfgNode(this, startInstr, entryTypes);
                needsReparse = true;
                m_cfgNodes.Add(startInstr, node);
            }

            if (needsReparse)
            {
                if (!m_pendingNodesSet.Contains(startInstr))
                {
                    m_pendingNodesSet.Add(startInstr);
                    m_pendingNodesQueue.Enqueue(startInstr);
                }
            }

            return node;
        }

        public void ConstructCfg()
        {
            m_pendingNodesQueue = new Queue<int>();
            m_pendingNodesSet = new HashSet<int>();

            m_cfgNodes = new Dictionary<int, CfgNode>();

            List<VType> entryTypes = new List<VType>();

            if (m_region.ExceptionType != null)
                entryTypes.Add(new VType(VType.ValTypeEnum.NotNullReferenceValue, m_region.ExceptionType));
            AddCfgTarget(null, m_startInstr, entryTypes.ToArray());

            while (m_pendingNodesQueue.Count > 0)
            {
                int startLoc = m_pendingNodesQueue.Dequeue();
                m_pendingNodesSet.Remove(startLoc);

                CfgNode cfgNode = m_cfgNodes[startLoc];
                cfgNode.Parse();
            }

            m_rootNode = m_cfgNodes[m_startInstr];
        }

        public bool InstrIsJumpTarget(int instrNum)
        {
            return m_instrIsBranchTarget[instrNum];
        }
    }
}
