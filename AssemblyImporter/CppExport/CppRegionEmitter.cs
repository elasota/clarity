using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.CppExport
{
    // We support 3 register allocation cases for eval values crossing CFG edges:
    // If there is only one predecessor and it is continuous, then we recycle the predecessor's SSAs.
    // If there is only one predecessor and it is not continuous, then the predecessor computes spills.
    // If there are multiple predecessors, then the register allocation is determined using a uniform process.
    public class CppRegionEmitter
    {
        private struct SsaVRegMapping
        {
            public VReg VReg { get { return m_vReg; } }
            public SsaRegister SsaRegister { get { return m_ssaReg; } }

            private SsaRegister m_ssaReg;
            private VReg m_vReg;

            public SsaVRegMapping(SsaRegister ssaReg, VReg vReg)
            {
                m_ssaReg = ssaReg;
                m_vReg = vReg;
            }
        }

        private CppRegisterAllocator m_regAllocator;
        private ExceptionHandlingRegion m_region;
        private Stack<CfgNode> m_unemittedNodesStack;   // May contain duplicates, check set to dedupe
        private HashSet<CfgNode> m_emittedNodesSet;

        public CppRegionEmitter(ExceptionHandlingRegion region, CppRegisterAllocator regAllocator)
        {
            m_region = region;
            m_regAllocator = regAllocator;
            m_emittedNodesSet = new HashSet<CfgNode>();
            m_unemittedNodesStack = new Stack<CfgNode>();

            AddNode(region.RootCfgNode);
        }

        private bool AddNode(CfgNode cfgNode)
        {
            if (m_emittedNodesSet.Contains(cfgNode))
                return false;
            m_unemittedNodesStack.Push(cfgNode);
            return true;
        }

        private void SpillCfgEdge(CfgNode node, CfgOutboundEdge edge, StreamWriter writer)
        {
            List<SsaRegister> outputRegs = new List<SsaRegister>();

            MidInstruction[] instrs = node.MidInstructions;
            for (int i = instrs.Length - 1; i >= 0; i--)
            {
                MidInstruction instr = instrs[i];
                if (instr.Opcode != MidInstruction.OpcodeEnum.LeakReg)
                    break;
                outputRegs.Add(instr.RegArg);
            }

            if (edge.SuccessorNode.Predecessors.Count == 1)
            {
                // Single-predecessor edge, we can decide the spills here
                List<VReg> tempVRegs = new List<VReg>();

                foreach (SsaRegister outReg in outputRegs)
                {
                    if (outReg.IsSpilled)
                    {
                        if (outReg.SpillVReg == null)
                            throw new Exception("Spilled SSA reg has no vreg?");
                        outReg.SinglePredecessorSpillVReg = outReg.SpillVReg;
                    }
                    else
                    {
                        VType vt = outReg.VType;
                        if (CppRegisterAllocator.IsVTypeSpillable(vt))
                        {
                            VReg tempVReg = m_regAllocator.AllocReg(vt);
                            outReg.SinglePredecessorSpillVReg = tempVReg;
                            tempVRegs.Add(tempVReg);
                            writer.WriteLine("spill spe ssa " + outReg.SsaID + " to local " + tempVReg.SlotName);
                        }
                    }
                }

                foreach (VReg vReg in tempVRegs)
                    vReg.Kill();
            }
            else
            {
                // Multiple predecessor target, need to translate into standard spilling
                // What we do here is find all SSA registers that need new VRegs, load the ones that aren't
                // already in the correct VReg, then emit stores
                VReg[] vRegs = m_regAllocator.TargetRegsForCfgInput(edge.SuccessorNode.EntryTypes);

                if (outputRegs.Count != vRegs.Length)
                    throw new Exception("Mismatched CFG edge vreg count");

                int numTranslations = vRegs.Length;

                SsaRegister[] storeBases = new SsaRegister[numTranslations];
                for (int i = 0; i < numTranslations; i++)
                {
                    VReg vReg = vRegs[i];
                    if (vReg == null)
                        continue;
                    SsaRegister storeBase = outputRegs[i];
                    if (storeBase.IsSpilled)
                    {
                        if (storeBase.SpillVReg == null)
                            throw new Exception("Bad spill vreg");
                        if (storeBase.SpillVReg != vReg)
                        {
                            storeBase = new SsaRegister(storeBase.VType, -1);
                            storeBase.SsaID = m_regAllocator.NewSsaID();

                            writer.WriteLine("unspill midpoint temp " + storeBase.SsaID + " reg to SSA " + storeBase.SsaID);
                            storeBases[i] = storeBase;
                        }
                    }
                    else
                        storeBases[i] = storeBase;
                }

                for (int i = 0; i < numTranslations; i++)
                {
                    VReg vReg = vRegs[i];
                    if (vReg == null || storeBases[i] == null)
                        continue;
                    SsaRegister storeBase = storeBases[i];
                    writer.WriteLine("spill ssa " + storeBases[i].SsaID + " to local " + vRegs[i].SlotName);
                }
            }
        }

        private void MirrorFallThroughEdge(CfgNode node, bool isDownward)
        {
            MidInstruction[] outInstrs = node.MidInstructions;
            MidInstruction[] inInstrs = node.FallThroughEdge.SuccessorNode.MidInstructions;

            for (int i = 0; i < inInstrs.Length; i++)
            {
                MidInstruction inInstr = inInstrs[i];

                if (inInstr.Opcode != MidInstruction.OpcodeEnum.EntryReg)
                    break;
                MidInstruction outInstr = outInstrs[outInstrs.Length - 1 - i];
                if (outInstr.Opcode != MidInstruction.OpcodeEnum.LeakReg)
                    throw new ParseFailedException("Mismatched CFG edge registers");

                SsaRegister outReg = outInstr.RegArg;
                SsaRegister inReg = inInstr.RegArg;

                // Propagate spill status
                if (isDownward && outReg.IsSpilled)
                    inReg.Spill();
                if (!isDownward && inReg.IsSpilled)
                    outReg.Spill();
            }
        }

        public void LinkContinuationChain(CfgNode firstNode)
        {
            CfgNode lastNode = firstNode;

            while (true)
            {
                if (!lastNode.CanBeContinuous)
                    break;
                CfgNode successor = lastNode.FallThroughEdge.SuccessorNode;
                if (m_emittedNodesSet.Contains(successor))
                    break;
                if (successor == firstNode)
                    break;  // Circular CFG
                lastNode = successor;
            }

            // Mirror nodes down
            {
                CfgNode scanNode = firstNode;
                while (scanNode != lastNode)
                {
                    MirrorFallThroughEdge(scanNode, true);
                    scanNode = scanNode.FallThroughEdge.SuccessorNode;
                }
            }

            // Mirror nodes up
            {
                CfgNode scanNode = lastNode;
                while (scanNode != firstNode)
                {
                    if (scanNode.Predecessors.Count != 1)
                        throw new Exception("Continuous fallthrough can't enter a multi-predecessor node");
                    scanNode = scanNode.Predecessors[0];
                    MirrorFallThroughEdge(scanNode, false);
                }
            }
        }

        public void Emit(StreamWriter writer)
        {
            writer.WriteLine("Escape terminators:");
            foreach (KeyValuePair<uint, CfgNode> terminator in m_region.EscapeTerminators)
                writer.WriteLine("    " + terminator.Key + " --> " + m_regAllocator.TargetIDForCfgNode(terminator.Value));
            
            CfgNode lastCfgNode = null;
            bool exitIsContinuous = false;
            while (m_unemittedNodesStack.Count > 0)
            {
                CfgNode cfgNode = m_unemittedNodesStack.Pop();
                if (m_emittedNodesSet.Contains(cfgNode))
                    continue;

                bool enterIsContinuous = exitIsContinuous;
                exitIsContinuous = 
                    cfgNode.CanBeContinuous
                    && !m_emittedNodesSet.Contains(cfgNode.FallThroughEdge.SuccessorNode);

                if (!enterIsContinuous)
                    LinkContinuationChain(cfgNode);

                m_emittedNodesSet.Add(cfgNode);

                EmitCfgNode(cfgNode, writer, enterIsContinuous, exitIsContinuous);
                lastCfgNode = cfgNode;

                if (!exitIsContinuous)
                {
                    foreach (VReg vReg in m_regAllocator.AllRegisters)
                        if (vReg.IsAlive)
                            throw new Exception("VReg leaked");
                }
            }
        }

        private void EmitCfgNode(CfgNode cfgNode, StreamWriter writer, bool enterIsContinuous, bool exitIsContinuous)
        {
            Console.WriteLine("Starting CFG edge");
            CppScopeStack scopeStack = new CppScopeStack();

            Queue<SsaRegister> continuedRegs = null;
            if (enterIsContinuous || cfgNode.Predecessors.Count == 1)
            {
                continuedRegs = new Queue<SsaRegister>();
                CfgNode predecessor = cfgNode.Predecessors[0];
                MidInstruction[] instrs = predecessor.MidInstructions;
                for (int i = instrs.Length - 1; i >= 0; i--)
                {
                    MidInstruction instr = instrs[i];
                    if (instr.Opcode != MidInstruction.OpcodeEnum.LeakReg)
                        break;
                    continuedRegs.Enqueue(instr.RegArg);
                }
            }

            writer.WriteLine("CFG Node " + m_regAllocator.TargetIDForCfgNode(cfgNode) + ":");
            int debugInstrNum = -1;
            MidInstruction[] midInstrs = cfgNode.MidInstructions;
            foreach (MidInstruction midInstr in midInstrs)
            {
                debugInstrNum++;
                switch (midInstr.Opcode)
                {
                    case MidInstruction.OpcodeEnum.AllocObject:
                        writer.WriteLine("AllocObject SSA " + midInstr.RegArg.SsaID + " Type " + midInstr.TypeSpecArg);
                        break;
                    case MidInstruction.OpcodeEnum.CallConstructor:
                        writer.WriteLine("CallConstructor SSA " + midInstr.RegArg.SsaID + " Method " + midInstr.MethodSpecArg);
                        if (midInstr.RegArgs != null)
                        {
                            writer.Write("    Parameter SSAs:");
                            foreach (SsaRegister reg in midInstr.RegArgs)
                            {
                                writer.Write(" ");
                                writer.Write(reg.SsaID);
                            }
                            writer.WriteLine();
                        }
                        break;
                    case MidInstruction.OpcodeEnum.CallMethod:
                        writer.WriteLine("CallMethod " + midInstr.MethodSpecArg);
                        if (midInstr.RegArg != null)
                            writer.WriteLine("    Return SSA: " + midInstr.RegArg.SsaID);
                        if (midInstr.RegArg2 != null)
                            writer.WriteLine("    This SSA: " + midInstr.RegArg2.SsaID);
                        if (midInstr.RegArgs != null)
                        {
                            writer.Write("    Parameter SSAs:");
                            foreach (SsaRegister reg in midInstr.RegArgs)
                            {
                                writer.Write(" ");
                                writer.Write(reg.SsaID);
                            }
                            writer.WriteLine();
                        }
                        break;
                    case MidInstruction.OpcodeEnum.CallVirtualMethod:
                        writer.WriteLine("CallVirtualMethod " + midInstr.MethodSpecArg);
                        if (midInstr.RegArg != null)
                            writer.WriteLine("    Return SSA: " + midInstr.RegArg.SsaID);
                        if (midInstr.RegArg2 != null)
                            writer.WriteLine("    This SSA: " + midInstr.RegArg2.SsaID);
                        if (midInstr.RegArgs != null)
                        {
                            writer.Write("    Parameter SSAs:");
                            foreach (SsaRegister reg in midInstr.RegArgs)
                            {
                                writer.Write(" ");
                                writer.Write(reg.SsaID);
                            }
                            writer.WriteLine();
                        }
                        break;
                    case MidInstruction.OpcodeEnum.KillReg:
                        if (midInstr.RegArg.IsSpilled)
                        {
                            writer.WriteLine("// vreg " + midInstr.RegArg.SpillVReg.SlotName + " killed");
                            midInstr.RegArg.SpillVReg.Kill();
                        }
                        else
                            scopeStack.KillReg(midInstr.RegArg, writer);
                        break;
                    case MidInstruction.OpcodeEnum.LivenReg:
                        {
                            SsaRegister reg = midInstr.RegArg;
                            reg.SsaID = m_regAllocator.NewSsaID();

                            if (reg.IsSpilled)
                            {
                                reg.SpillVReg = m_regAllocator.AllocReg(reg.VType);
                                writer.WriteLine("// ssa " + reg.SsaID + " is spilled to " + reg.SpillVReg.SlotName);
                            }
                            else
                                scopeStack.LivenReg(reg, writer);
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Return:
                        writer.WriteLine("Return");
                        break;
                    case MidInstruction.OpcodeEnum.ReturnValue:
                        writer.WriteLine("ReturnValue SSA: " + midInstr.RegArg.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadReg_ManagedPtr:
                        writer.WriteLine("LoadReg_ManagedPtr SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.LoadReg_Value:
                        writer.WriteLine("LoadReg_Value SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.Store:
                        writer.WriteLine("Store SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.beq:
                        writer.WriteLine("beq Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bne:
                        writer.WriteLine("bne Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bge:
                        writer.WriteLine("bge Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bgt:
                        writer.WriteLine("bgt Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.ble:
                        writer.WriteLine("ble Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.blt:
                        writer.WriteLine("blt Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.clt:
                        writer.WriteLine("clt Output SSA: " + midInstr.RegArg.SsaID + "  Value 1 SSA: " + midInstr.RegArg2.SsaID + "  Value 2 SSA: " + midInstr.RegArg3.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.cgt:
                        writer.WriteLine("cgt Output SSA: " + midInstr.RegArg.SsaID + "  Value 1 SSA: " + midInstr.RegArg2.SsaID + "  Value 2 SSA: " + midInstr.RegArg3.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.ceq:
                        writer.WriteLine("ceq Output SSA: " + midInstr.RegArg.SsaID + "  Value 1 SSA: " + midInstr.RegArg2.SsaID + "  Value 2 SSA: " + midInstr.RegArg3.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadArgA_Value:
                        writer.WriteLine("LoadArgA_Value SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.brtrue:
                        writer.WriteLine("brtrue SSA: " + midInstr.RegArg.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.brfalse:
                        writer.WriteLine("brfalse SSA: " + midInstr.RegArg.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.LeakReg:
                        {
                            writer.WriteLine("LeakReg SSA: " + midInstr.RegArg.SsaID);
                            if (!exitIsContinuous && midInstr.RegArg.IsSpilled)
                                midInstr.RegArg.SpillVReg.Kill();
                        }
                        break;
                    case MidInstruction.OpcodeEnum.EntryReg:
                        {
                            if (enterIsContinuous)
                            {
                                SsaRegister newReg = midInstr.RegArg;
                                SsaRegister oldReg = continuedRegs.Dequeue();

                                newReg.SsaID = oldReg.SsaID;
                                if (newReg.IsSpilled)
                                {
                                    newReg.SpillVReg = oldReg.SpillVReg;
                                    if (newReg.SpillVReg == null || !newReg.SpillVReg.IsAlive)
                                        throw new Exception("Continued a spilled SSA register, but the vreg is dead");
                                }
                                else
                                    scopeStack.RecycleReg(midInstr.RegArg);
                            }
                            else
                            {
                                SsaRegister oldReg = null;
                                if (cfgNode.Predecessors.Count == 1)
                                    oldReg = continuedRegs.Dequeue();

                                SsaRegister reg = midInstr.RegArg;
                                reg.SsaID = m_regAllocator.NewSsaID();
                                if (CppRegisterAllocator.IsVTypeSpillable(reg.VType))
                                {
                                    reg.Spill();
                                    if (cfgNode.Predecessors.Count == 1)
                                    {
                                        reg.SpillVReg = oldReg.SinglePredecessorSpillVReg;
                                        reg.SpillVReg.Liven();
                                        if (reg.SpillVReg == null)
                                            throw new Exception("Single-predecessor edge-crossing register wasn't spilled");
                                    }
                                    else
                                        reg.SpillVReg = m_regAllocator.AllocReg(reg.VType);

                                    writer.WriteLine("linked spe ssa " + reg.SsaID + " to prespilled local " + reg.SpillVReg.SlotName);
                                }
                            }
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Throw:
                        writer.WriteLine("Throw SSA: " + midInstr.RegArg.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.NewSZArray:
                        writer.WriteLine("NewSZArray Result SSA: " + midInstr.RegArg.SsaID + "  NumElems SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_ManagedPtr:
                        writer.WriteLine("LoadField_ManagedPtr Object SSA: " + midInstr.RegArg.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldA_ManagedPtr:
                        writer.WriteLine("LoadFieldA_ManagedPtr Object SSA: " + midInstr.RegArg.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_Object:
                        writer.WriteLine("LoadField_Object Object SSA: " + midInstr.RegArg.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldA_Object:
                        writer.WriteLine("LoadFieldA_Object Object SSA: " + midInstr.RegArg.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadRegA:
                        writer.WriteLine("LoadRegA SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayElem:
                        writer.WriteLine("LoadArrayElem Array SSA: " + midInstr.RegArg.SsaID + "  Index SSA: " + midInstr.RegArg2.SsaID + "  Contents SSA: " + midInstr.RegArg3.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayElemAddr:
                        writer.WriteLine("LoadArrayElemAddr Array SSA: " + midInstr.RegArg.SsaID + "  Index SSA: " + midInstr.RegArg2.SsaID + "  Addr SSA: " + midInstr.RegArg3.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.StoreField_ManagedPtr:
                        writer.WriteLine("StoreField_ManagedPtr Object SSA: " + midInstr.RegArg.SsaID + "  Value SSA: " + midInstr.RegArg2.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.add:
                    case MidInstruction.OpcodeEnum.sub:
                    case MidInstruction.OpcodeEnum.mul:
                    case MidInstruction.OpcodeEnum.div:
                    case MidInstruction.OpcodeEnum.rem:
                    case MidInstruction.OpcodeEnum.and:
                    case MidInstruction.OpcodeEnum.or:
                    case MidInstruction.OpcodeEnum.xor:
                    case MidInstruction.OpcodeEnum.shl:
                    case MidInstruction.OpcodeEnum.shr:
                        writer.WriteLine("BinaryArith " + midInstr.Opcode.ToString() + "  Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Result SSA: " + midInstr.RegArg3.SsaID + "  Arith mode " + (int)midInstr.ArithArg);
                        break;
                    case MidInstruction.OpcodeEnum.neg:
                    case MidInstruction.OpcodeEnum.not:
                        writer.WriteLine("UnaryArith " + midInstr.Opcode.ToString() + "  Value SSA: " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID + "  Arith mode " + (int)midInstr.ArithArg);
                        break;
                    case MidInstruction.OpcodeEnum.TryConvertObj:
                        writer.WriteLine("TryConvertObj Value SSA: " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.Leave:
                        writer.WriteLine("Leave Esc " + midInstr.UIntArg);
                        break;
                    case MidInstruction.OpcodeEnum.DuplicateReg:
                        writer.WriteLine("DuplicateReg SSA: " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.StoreStaticField:
                        writer.WriteLine("StoreStaticField SSA: " + midInstr.RegArg.SsaID + "  Class: " + midInstr.TypeSpecArg + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadIndirect:
                        writer.WriteLine("StoreStaticField SSA: " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadStaticField:
                        writer.WriteLine("LoadStaticField SSA: " + midInstr.RegArg.SsaID + "  Class: " + midInstr.TypeSpecArg + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.Box:
                        writer.WriteLine("Box SSA " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.ConvertNumber:
                        writer.WriteLine("ConvertNumber SSA " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayLength:
                        writer.WriteLine("LoadArrayLength SSA " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadTypeInfoHandle:
                        writer.WriteLine("LoadTypeInfoHandle SSA " + midInstr.RegArg.SsaID + "  Type: " + midInstr.TypeSpecArg);
                        break;
                    case MidInstruction.OpcodeEnum.ConvertObj:
                        writer.WriteLine("ConvertObj SSA " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.StoreArrayElem:
                        writer.WriteLine("StoreArrayElem SSA " + midInstr.RegArg.SsaID + "  Index SSA: " + midInstr.RegArg2.SsaID + "  Value SSA: " + midInstr.RegArg3.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.Switch:
                        {
                            writer.WriteLine("Switch Value SSA " + midInstr.RegArg.SsaID);
                            int numCases = midInstr.CfgEdgesArg.Length;
                            for (int i = 0; i < numCases; i++)
                            {
                                CfgOutboundEdge edge = midInstr.CfgEdgesArg[numCases - 1 - i];
                                CfgNode caseNode = edge.SuccessorNode;
                                SpillCfgEdge(cfgNode, edge, writer);
                                AddNode(caseNode);
                            }
                            foreach (CfgOutboundEdge outboundEdge in midInstr.CfgEdgesArg)
                                writer.WriteLine("    Case target: " + m_regAllocator.TargetIDForCfgNode(outboundEdge.SuccessorNode));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.StoreReg_Value:
                        writer.WriteLine("StoreReg_Value SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.StoreIndirect:
                        writer.WriteLine("StoreIndirect SSA: " + midInstr.RegArg.SsaID + "  Value SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldInfoHandle:
                        writer.WriteLine("LoadFieldInfoHandle SSA: " + midInstr.RegArg.SsaID + "  Class: " + midInstr.TypeSpecArg + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.UnboxPtr:
                        writer.WriteLine("UnboxPtr SSA: " + midInstr.RegArg.SsaID + "  Value SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.UnboxValue:
                        writer.WriteLine("UnboxValue SSA: " + midInstr.RegArg.SsaID + "  Value SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.ZeroFillPtr:
                        writer.WriteLine("ZeroFillPtr SSA: " + midInstr.RegArg.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.EnterProtectedBlock:
                        {
                            writer.WriteLine("EnterProtectedBlock");
                            ExceptionHandlingCluster cluster = midInstr.EhClusterArg;
                            writer.WriteLine("Try {");
                            {
                                CppRegionEmitter tryEmitter = new CppRegionEmitter(cluster.TryRegion, m_regAllocator);
                                tryEmitter.Emit(writer);
                            }
                            writer.WriteLine("}");
                            foreach (ExceptionHandlingRegion handlerRegion in cluster.ExceptionHandlingRegions)
                            {
                                if (cluster.ClusterType == ExceptionHandlingCluster.ClusterTypeEnum.TryCatch)
                                    writer.WriteLine("Catch " + handlerRegion.ExceptionType + " {");
                                else if (cluster.ClusterType == ExceptionHandlingCluster.ClusterTypeEnum.TryFault)
                                    writer.WriteLine("Fault {");
                                else if (cluster.ClusterType == ExceptionHandlingCluster.ClusterTypeEnum.TryFinally)
                                    writer.WriteLine("Finally {");
                                CppRegionEmitter hdlEmitter = new CppRegionEmitter(handlerRegion, m_regAllocator);
                                hdlEmitter.Emit(writer);
                                writer.WriteLine("}");
                            }

                            foreach (uint escapePath in cluster.EscapePaths)
                            {
                                CfgNode targetNode;
                                if (m_region.EscapeTerminators.TryGetValue(escapePath, out targetNode))
                                    AddNode(targetNode);
                            }
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ExitFinally:
                        writer.WriteLine("ExitFinally");
                        break;
                    default:
                        throw new ArgumentException("Invalid mid IL opcode");
                }
            }

            if (cfgNode.FallThroughEdge != null)
            {
                writer.WriteLine("FallThrough CFG " + m_regAllocator.TargetIDForCfgNode(cfgNode.FallThroughEdge.SuccessorNode));

                if (!exitIsContinuous)
                    SpillCfgEdge(cfgNode, cfgNode.FallThroughEdge, writer);

                AddNode(cfgNode.FallThroughEdge.SuccessorNode);
            }
        }
    }
}
