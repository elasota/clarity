using System;
using System.Collections.Generic;
using Clarity.Rpa;
using Clarity.Rpa.Instructions;

namespace Clarity.RpaCompiler
{
    public class RloRegionConverter
    {
        private RloMethodConverter m_methodConverter;
        private HighCfgNodeHandle m_entryNode;
        private bool m_checkInstructions;

        private Dictionary<HighCfgNode, HighCfgNodeHandle> m_sourceToConverted = new Dictionary<HighCfgNode, HighCfgNodeHandle>();
        private Queue<KeyValuePair<HighCfgNode, HighCfgNodeHandle>> m_unconvertedNodes = new Queue<KeyValuePair<HighCfgNode, HighCfgNodeHandle>>();
        private HighInstruction.VisitSsaDelegate m_ssaTranslate;
        private HighInstruction.VisitLocalDelegate m_localTranslate;
        private HighInstruction.VisitCfgNodeDelegate m_cfgNodeTranslate;
        private HighInstruction.VisitCfgEdgeDelegate m_cfgEdgeTranslate;
        private HighInstruction.VisitTypeSpecDelegate m_typeSpecTranslate;
        private HighInstruction.VisitMethodSpecDelegate m_methodSpecTranslate;

        public HighCfgNodeHandle EntryNode { get { return m_entryNode; } }

        public RloRegionConverter(RloMethodConverter methodConverter, HighRegion region, bool checkInstructions)
        {
            m_ssaTranslate = TranslateSSA;
            m_localTranslate = TranslateLocal;
            m_cfgNodeTranslate = TranslateCfgNode;
            m_cfgEdgeTranslate = TranslateCfgEdge;
            m_typeSpecTranslate = TranslateTypeSpec;
            m_methodSpecTranslate = TranslateMethodSpec;

            m_methodConverter = methodConverter;
            m_checkInstructions = checkInstructions;

            m_entryNode = GetNode(region.EntryNode.Value);

            while (m_unconvertedNodes.Count > 0)
            {
                KeyValuePair<HighCfgNode, HighCfgNodeHandle> workItem = m_unconvertedNodes.Dequeue();

                ConvertNode(workItem.Key, workItem.Value);
            }
        }

        private void TranslateMethodSpec(ref MethodSpecTag methodSpec)
        {
            methodSpec = m_methodConverter.InstantiateMethodSpec(methodSpec);
        }

        private void TranslateLocal(ref HighLocal highLocal)
        {
            highLocal = m_methodConverter.GetLocal(highLocal);
        }

        private void TranslateSSA(ref HighSsaRegister highSsaReg)
        {
            if (highSsaReg != null)
                highSsaReg = m_methodConverter.GetReg(highSsaReg);
        }

        private void TranslateCfgNode(ref HighCfgNodeHandle cfgNode)
        {
            cfgNode = this.GetNode(cfgNode.Value);
        }

        private void TranslateCfgEdge(ref HighCfgEdge cfgEdge)
        {
            cfgEdge = new HighCfgEdge(cfgEdge.Source, this.GetNode(cfgEdge.Dest.Value));
        }

        private void TranslateTypeSpec(ref TypeSpecTag typeSpec)
        {
            typeSpec = m_methodConverter.InstantiateType(typeSpec);
        }

        public HighCfgNodeHandle GetNode(HighCfgNode srcNode)
        {
            HighCfgNodeHandle nodeHandle;
            if (m_sourceToConverted.TryGetValue(srcNode, out nodeHandle))
                return nodeHandle;

            nodeHandle = new HighCfgNodeHandle();
            m_sourceToConverted.Add(srcNode, nodeHandle);

            m_unconvertedNodes.Enqueue(new KeyValuePair<HighCfgNode, HighCfgNodeHandle>(srcNode, nodeHandle));

            return nodeHandle;
        }

        public void ConvertNode(HighCfgNode srcNode, HighCfgNodeHandle cfgNodeHandle)
        {
            List<HighInstruction> instructions = new List<HighInstruction>();

            List<HighPhi> newPhis = new List<HighPhi>();
            foreach (HighPhi phi in srcNode.Phis)
            {
                List<HighPhiLink> newLinks = new List<HighPhiLink>();
                foreach (HighPhiLink link in phi.Links)
                {
                    HighCfgNodeHandle predecessor = GetNode(link.Predecessor.Value);
                    HighSsaRegister reg = m_methodConverter.GetReg(link.Reg);

                    newLinks.Add(new HighPhiLink(predecessor, reg));
                }

                HighSsaRegister dest = m_methodConverter.GetReg(phi.Dest);
                newPhis.Add(new HighPhi(dest, newLinks.ToArray()));
            }

            List<HighInstruction> newInstructions = new List<HighInstruction>();
            foreach (HighInstruction instr in srcNode.Instructions)
                ConvertInstruction(instr, newInstructions);

            if (newInstructions.Count == 0)
                throw new Exception("CFG node was empty");

            HighCfgNode newNode = new HighCfgNode(newPhis.ToArray(), newInstructions.ToArray());

            cfgNodeHandle.Value = newNode;
        }

        private void ConvertInstruction(HighInstruction instr, List<HighInstruction> newInstrs)
        {
            switch (instr.Opcode)
            {
                case HighInstruction.Opcodes.AllocArray:
                case HighInstruction.Opcodes.LoadLocal:
                case HighInstruction.Opcodes.PassiveConvert:
                case HighInstruction.Opcodes.Arith:
                case HighInstruction.Opcodes.Box:
                case HighInstruction.Opcodes.BranchCompareNumbers:
                case HighInstruction.Opcodes.Branch:
                case HighInstruction.Opcodes.DynamicCast:
                case HighInstruction.Opcodes.ForceDynamicCast:
                case HighInstruction.Opcodes.GetArrayElementPtr:
                case HighInstruction.Opcodes.CompareRefs:
                case HighInstruction.Opcodes.BranchCompareRefs:
                case HighInstruction.Opcodes.GetStaticFieldAddr:
                case HighInstruction.Opcodes.BranchRefNull:
                case HighInstruction.Opcodes.GetTypeInfo:
                case HighInstruction.Opcodes.CallConstrainedMethod:
                case HighInstruction.Opcodes.CallConstrainedVirtualMethod:
                case HighInstruction.Opcodes.LoadPtr:
                case HighInstruction.Opcodes.PtrField:
                case HighInstruction.Opcodes.CallInstanceMethod:
                case HighInstruction.Opcodes.CallStaticMethod:
                case HighInstruction.Opcodes.RefField:
                case HighInstruction.Opcodes.NumberConvert:
                case HighInstruction.Opcodes.CompareNumbers:
                case HighInstruction.Opcodes.CallVirtualMethod:
                case HighInstruction.Opcodes.GetArrayLength:
                case HighInstruction.Opcodes.Return:
                case HighInstruction.Opcodes.GetLocalPtr:
                case HighInstruction.Opcodes.ReturnValue:
                case HighInstruction.Opcodes.UnaryArith:
                case HighInstruction.Opcodes.StoreLocal:
                case HighInstruction.Opcodes.UnboxPtr:
                case HighInstruction.Opcodes.ZeroFillPtr:
                case HighInstruction.Opcodes.UnboxValue:
                case HighInstruction.Opcodes.Switch:
                case HighInstruction.Opcodes.Throw:
                case HighInstruction.Opcodes.StorePtr:
                case HighInstruction.Opcodes.GetFieldInfo:
                case HighInstruction.Opcodes.LoadValueField:
                case HighInstruction.Opcodes.BindStaticDelegate:
                case HighInstruction.Opcodes.BindInstanceDelegate:
                case HighInstruction.Opcodes.BindVirtualDelegate:
                case HighInstruction.Opcodes.Catch:
                case HighInstruction.Opcodes.LeaveRegion:
                case HighInstruction.Opcodes.AllocObj:
                    {
                        HighInstruction clonedInstr = instr.Clone();
                        clonedInstr.VisitAllSsa(m_ssaTranslate);

                        ILocalUsingInstruction localUsing = clonedInstr as ILocalUsingInstruction;
                        if (localUsing != null)
                            localUsing.VisitLocalRefs(m_localTranslate);

                        IBranchingInstruction branching = clonedInstr as IBranchingInstruction;
                        if (branching != null)
                            branching.VisitSuccessors(m_cfgEdgeTranslate);

                        ITypeReferencingInstruction typeReferencing = clonedInstr as ITypeReferencingInstruction;
                        if (typeReferencing != null)
                            typeReferencing.VisitTypes(m_typeSpecTranslate);

                        IMethodReferencingInstruction methodReferencing = clonedInstr as IMethodReferencingInstruction;
                        if (methodReferencing != null)
                            methodReferencing.VisitMethodSpecs(m_methodSpecTranslate);

                        newInstrs.Add(clonedInstr);
                    }
                    break;
                case HighInstruction.Opcodes.EnterProtectedBlock:
                    {
                        EnterProtectedBlockInstruction tInstr = (EnterProtectedBlockInstruction)instr;
                        HighEHCluster oldCluster = tInstr.EHCluster;

                        List<HighEscapePathTerminator> terminators = new List<HighEscapePathTerminator>();
                        foreach (HighEscapePathTerminator terminator in oldCluster.EscapePathTerminators)
                        {
                            uint escapePath = terminator.EscapePath;
                            HighCfgNodeHandle cfgNode = GetNode(terminator.CfgNode.Value);
                            terminators.Add(new HighEscapePathTerminator(escapePath, cfgNode));
                        }

                        HighProtectedRegion oldProtRegion = oldCluster.ProtectedRegion;
                        HighProtectedRegion convertedProtRegion = ConvertProtectedRegion(oldProtRegion);

                        HighEHCluster newCluster = new HighEHCluster(convertedProtRegion, terminators.ToArray());

                        newInstrs.Add(new EnterProtectedBlockInstruction(tInstr.CodeLocation, newCluster));
                    }
                    break;
                default:
                    throw new Exception("Unrecognized instruction");
            }
        }

        private HighProtectedRegion ConvertProtectedRegion(HighProtectedRegion oldProtRegion)
        {
            HighRegion oldTryRegion = oldProtRegion.TryRegion;
            HighCfgNodeHandle tryEntryNode = GetNode(oldTryRegion.EntryNode.Value);
            HighRegion newTryRegion = new HighRegion(tryEntryNode);

            HighTryCatchRegion tryCatchRegion = oldProtRegion as HighTryCatchRegion;
            if (tryCatchRegion != null)
            {
                List<HighCatchHandler> catchHandlers = new List<HighCatchHandler>();
                foreach (HighCatchHandler catchHandler in tryCatchRegion.CatchHandlers)
                {
                    TypeSpecTag catchType = m_methodConverter.InstantiateType(catchHandler.CatchType);
                    HighRegion region = new HighRegion(GetNode(catchHandler.Region.EntryNode.Value));

                    catchHandlers.Add(new HighCatchHandler(catchType, region));
                }
                return new HighTryCatchRegion(newTryRegion, catchHandlers.ToArray());
            }

            HighTryFaultRegion tryFaultRegion = oldProtRegion as HighTryFaultRegion;
            if (tryFaultRegion != null)
            {
                HighRegion region = new HighRegion(GetNode(tryFaultRegion.FaultRegion.EntryNode.Value));

                return new HighTryFaultRegion(newTryRegion, region);
            }

            HighTryFinallyRegion tryFinallyRegion = oldProtRegion as HighTryFinallyRegion;
            if (tryFinallyRegion != null)
            {
                HighRegion region = new HighRegion(GetNode(tryFinallyRegion.FinallyRegion.EntryNode.Value));

                return new HighTryFinallyRegion(newTryRegion, region);
            }

            throw new Exception();
        }
    }
}
