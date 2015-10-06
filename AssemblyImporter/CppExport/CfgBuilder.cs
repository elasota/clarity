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

        private CppBuilder m_builder;
        private CppClass m_cls;
        private CppMethod m_method;
        private VReg[] m_args;
        private VReg[] m_locals;
        private HLInstruction[] m_instrs;

        private Dictionary<int, CfgNode> m_cfgNodes;

        private bool[] m_instrIsBranchTarget;

        private HashSet<int> m_pendingNodesSet;
        private Queue<int> m_pendingNodesQueue;
        private CLRTypeDefRow m_inClass;
        private CLRMethodDefRow m_inMethod;

        public CfgBuilder(CppBuilder builder, CppClass cls, CppMethod method, VReg[] args, VReg[] locals)
        {
            m_builder = builder;
            m_cls = cls;
            m_method = method;
            m_args = args;
            m_locals = locals;
            m_instrs = method.MethodDef.Method.Instructions;
            m_inClass = cls.TypeDef;
            m_inMethod = method.MethodDef;

            LocateBranchTargets();
            ConstructCfg();
        }

        public void LocateBranchTargets()
        {
            bool[] knownStarts = new bool[m_instrs.Length];
            Queue<int> unprocessedStarts = new Queue<int>();

            knownStarts[0] = true;
            unprocessedStarts.Enqueue(0);

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
                    case HLOpcode.leave:
                        AddBranchTarget((int)instr.Arguments.U32Value, knownStarts, unprocessedStarts);
                        isTerminal = true;
                        break;

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

            AddCfgTarget(null, 0, new VType[] { });

            while (m_pendingNodesQueue.Count > 0)
            {
                int startLoc = m_pendingNodesQueue.Dequeue();
                m_pendingNodesSet.Remove(startLoc);

                CfgNode cfgNode = m_cfgNodes[startLoc];
                cfgNode.Parse();
            }
        }

        public bool InstrIsJumpTarget(int instrNum)
        {
            return m_instrIsBranchTarget[instrNum];
        }
    }
}
