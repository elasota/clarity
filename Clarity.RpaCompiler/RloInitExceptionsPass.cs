using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    // This generates incomplete predecessor sets and requires a separate pass to clean them up
    public class RloInitExceptionsPass
    {
        private Compiler m_compiler;
        private RloMethodBody m_methodBody;
        private Dictionary<uint, int> m_routeCompactionDict;
        private Dictionary<uint, HighCfgNode> m_terminators;
        private TypeSpecTag m_objectType;
        private TypeSpecTag m_intType;

        private TypeSpecTag ObjectType { get { return GetOrReturnTypeSpec(ref m_objectType, "Object"); } }
        private TypeSpecTag IntType { get { return GetOrReturnTypeSpec(ref m_intType, "Int32"); } }

        private TypeSpecTag GetOrReturnTypeSpec(ref TypeSpecTag typeSpecTag, string typeName)
        {
            if (typeSpecTag == null)
            {
                TypeNameTag nameTag = new TypeNameTag("mscorlib", "System", typeName, null);
                nameTag = m_compiler.TagRepository.InternTypeName(nameTag);
                TypeSpecTag typeSpec = new TypeSpecClassTag(nameTag, new TypeSpecTag[0]);
                typeSpecTag = m_compiler.TagRepository.InternTypeSpec(typeSpec);
            }
            return typeSpecTag;
        }

        private class RegionStack
        {
            private RegionStack m_next;
            private HighEHCluster m_ehCluster;
            private HighCfgNode m_exceptionHandler;

            public RegionStack Next { get { return m_next; } }
            public HighEHCluster EHCluster { get { return m_ehCluster; } }
            public HighCfgNode ExceptionHandler { get { return m_exceptionHandler; } }

            public RegionStack()
            {
            }

            public RegionStack(RegionStack next, HighEHCluster ehCluster, HighCfgNode exceptionHandler)
            {
                m_next = next;
                m_ehCluster = ehCluster;
                m_exceptionHandler = exceptionHandler;
            }
        }

        private class RegionProcessor
        {
            private HashSet<HighCfgNode> m_alreadyQueuedNodes;
            private Queue<HighCfgNode> m_queuedNodes;
            private RegionStack m_regionStack;
            private bool m_isTry;
            private HighCfgNode m_exceptionNode;
            private HighCfgNode m_finallyCleanupNode;
            private bool m_canReturnNothing;
            private RloInitExceptionsPass m_initPass;

            public RegionProcessor(RloInitExceptionsPass initPass, RegionStack regionStack, bool isTry, bool canReturnNothing)
            {
                m_initPass = initPass;
                m_isTry = isTry;
                m_alreadyQueuedNodes = new HashSet<HighCfgNode>();
                m_queuedNodes = new Queue<HighCfgNode>();
                m_canReturnNothing = canReturnNothing;

                if (regionStack != null)
                {
                    m_regionStack = regionStack;
                    if (m_isTry)
                        m_exceptionNode = regionStack.ExceptionHandler;
                    else
                    {
                        RegionStack parent = regionStack.Next;
                        if (parent != null)
                            m_exceptionNode = parent.ExceptionHandler;
                    }

                    HighTryFinallyRegion tryFinallyRegion = regionStack.EHCluster.ProtectedRegion as HighTryFinallyRegion;
                    if (tryFinallyRegion != null && !m_isTry)
                        m_finallyCleanupNode = GenerateFinallyCleanup(regionStack.EHCluster, tryFinallyRegion, regionStack.ExceptionHandler);
                }
            }

            private HighCfgNode GenerateFinallyCleanup(HighEHCluster ehCluster, HighTryFinallyRegion tryFinallyRegion, HighCfgNode handlerNode)
            {
                Instructions.CatchOrRouteInstruction catchInstr = (Instructions.CatchOrRouteInstruction)handlerNode.Instructions[0];

                List<Instructions.RloTerminateRoutesInstruction.RouteTermination> terminations = new List<Instructions.RloTerminateRoutesInstruction.RouteTermination>();

                HighCfgNode trappingFinally = null;

                RegionStack stack = m_regionStack;
                while (stack != null)
                {
                    if (stack != m_regionStack && stack.EHCluster.ProtectedRegion is HighTryFinallyRegion)
                        trappingFinally = stack.ExceptionHandler;

                    foreach (HighEscapePathTerminator terminator in stack.EHCluster.EscapePathTerminators)
                    {
                        int routeID = m_initPass.m_routeCompactionDict[terminator.EscapePath];

                        HighCfgNode successor;
                        if (trappingFinally != null)
                            successor = GenerateRepeatRoute(routeID, trappingFinally);
                        else
                            successor = terminator.CfgNode.Value;
                        terminations.Add(new Instructions.RloTerminateRoutesInstruction.RouteTermination(routeID, successor));
                    }
                    stack = stack.Next;
                }

                List<HighInstruction> instrs = new List<HighInstruction>();
                Instructions.RloTerminateRoutesInstruction trInstr = new Instructions.RloTerminateRoutesInstruction(null, terminations.ToArray());

                if (m_regionStack.Next != null)
                    trInstr.ExceptionEdge = new HighCfgEdge(trInstr, new HighCfgNodeHandle(m_regionStack.Next.ExceptionHandler));
                instrs.Add(trInstr);

                return new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            private HighCfgNode GenerateRepeatRoute(int routeID, HighCfgNode trappingFinally)
            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                instrs.Add(new Instructions.RloRoutedBranchInstruction(null, routeID, trappingFinally));
                return new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            public void Run()
            {
                while (m_queuedNodes.Count > 0)
                    ProcessNode(m_queuedNodes.Dequeue());
            }

            private void ProcessNode(HighCfgNode cfgNode)
            {
                HighCfgNode lastNode = cfgNode;
                HighInstruction lastInstr = cfgNode.Instructions[cfgNode.Instructions.Length - 1];

                // Rebuild handles so they're definitely not shared, collect handles
                List<HighCfgNodeHandle> successorLinkHandles = new List<HighCfgNodeHandle>();

                IBranchingInstruction brInstr = lastInstr as IBranchingInstruction;
                if (brInstr != null)
                {
                    brInstr.VisitSuccessors(delegate (ref HighCfgEdge edge)
                    {
                        foreach (HighPhi phi in edge.Dest.Value.Phis)
                        {
                            HighPhiLink[] links = phi.Links;
                            for (int i = 0; i < links.Length; i++)
                            {
                                if (links[i].Predecessor.Value == cfgNode)
                                {
                                    HighCfgNodeHandle hdl = new HighCfgNodeHandle(cfgNode);
                                    links[i].Predecessor = hdl;
                                    successorLinkHandles.Add(hdl);
                                }
                            }
                        }

                        if (edge.Dest.Value.Instructions[0] is Rpa.Instructions.CatchInstruction)
                            throw new RpaCompileException("Branch target is a catch instruction");

                        // Relink edges to their new sources
                        edge = new HighCfgEdge(lastInstr, edge.Dest);
                    });
                }

                List<HighInstruction> newInstructions = new List<HighInstruction>();
                foreach (HighInstruction instr in cfgNode.Instructions)
                {
                    if (instr is Rpa.Instructions.ReturnValueInstruction)
                    {
                        if (m_regionStack != null)
                            throw new RpaCompileException("ReturnValueInstruction in a protected region");
                        newInstructions.Add(instr);
                    }
                    else if (instr is Rpa.Instructions.ReturnInstruction)
                    {
                        if (m_regionStack != null)
                        {
                            if (m_finallyCleanupNode == null)
                                throw new RpaCompileException("ReturnInstruction in a protected region doesn't exit a finally");
                            newInstructions.Add(new Rpa.Instructions.BranchInstruction(instr.CodeLocation, new HighCfgNodeHandle(m_finallyCleanupNode)));
                        }
                        else if (!m_canReturnNothing)
                            throw new RpaCompileException("ReturnInstruction in a function that returns a value");
                    }
                    else if (instr is Rpa.Instructions.LeaveRegionInstruction)
                    {
                        Rpa.Instructions.LeaveRegionInstruction tInstr = (Rpa.Instructions.LeaveRegionInstruction)instr;

                        if (m_finallyCleanupNode != null && !m_isTry)
                            throw new RpaCompileException("LeaveRegionInstruction isn't valid in a finally handler");

                        uint routeID = tInstr.RouteID;

                        bool isRouted = false;
                        HighCfgNode matchingNode = null;

                        RegionStack stack = m_regionStack;
                        while (stack != null)
                        {
                            if (stack.EHCluster.ProtectedRegion is HighTryFinallyRegion)
                            {
                                isRouted = true;
                                matchingNode = stack.ExceptionHandler;
                                break;
                            }
                            else
                            {
                                bool matched = false;
                                foreach (HighEscapePathTerminator terminator in stack.EHCluster.EscapePathTerminators)
                                {
                                    if (terminator.EscapePath == routeID)
                                    {
                                        matched = true;
                                        matchingNode = terminator.CfgNode.Value;
                                        break;
                                    }
                                }
                                if (matched)
                                    break;
                            }
                            stack = stack.Next;
                        }

                        if (matchingNode == null)
                            throw new RpaCompileException("Unmatched exception escape route");

                        if (isRouted)
                            newInstructions.Add(new Instructions.RloRoutedBranchInstruction(tInstr.CodeLocation, m_initPass.m_routeCompactionDict[routeID], matchingNode));
                        else
                            newInstructions.Add(new Rpa.Instructions.BranchInstruction(tInstr.CodeLocation, new HighCfgNodeHandle(matchingNode)));
                    }
                    else if (instr is Rpa.Instructions.ThrowInstruction)
                    {
                        Rpa.Instructions.ThrowInstruction tInstr = (Rpa.Instructions.ThrowInstruction)instr;

                        if (m_exceptionNode != null)
                            tInstr.ExceptionEdge = new HighCfgEdge(tInstr, new HighCfgNodeHandle(m_exceptionNode));
                        newInstructions.Add(tInstr);
                    }
                    else if (instr is Rpa.Instructions.CatchInstruction)
                    {
                        throw new RpaCompileException("Invalid location of a catch instruction");
                    }
                    else if (instr is Rpa.Instructions.EnterProtectedBlockInstruction)
                    {
                        Rpa.Instructions.EnterProtectedBlockInstruction tInstr = (Rpa.Instructions.EnterProtectedBlockInstruction)instr;
                        HighEHCluster ehCluster = tInstr.EHCluster;
                        HighProtectedRegion protRegion = ehCluster.ProtectedRegion;

                        AddEscapePaths(ehCluster.EscapePathTerminators);

                        HighCfgNode tryNode;
                        if (protRegion is HighTryCatchRegion)
                        {
                            HighTryCatchRegion tProtRegion = (HighTryCatchRegion)protRegion;
                            RegionStack regionStack = new RegionStack(m_regionStack, ehCluster, GenerateTryCatchHandler(tProtRegion));

                            tryNode = ProcessSubRegion(tProtRegion.TryRegion, regionStack, true);

                            foreach (HighCatchHandler handler in tProtRegion.CatchHandlers)
                                ProcessSubRegion(handler.Region, regionStack, false);
                        }
                        else if (protRegion is HighTryFaultRegion)
                        {
                            HighTryFaultRegion tProtRegion = (HighTryFaultRegion)protRegion;
                            RegionStack regionStack = new RegionStack(m_regionStack, ehCluster, tProtRegion.FaultRegion.EntryNode.Value);

                            tryNode = ProcessSubRegion(tProtRegion.TryRegion, regionStack, true);
                            ProcessSubRegion(tProtRegion.FaultRegion, regionStack, false);
                        }
                        else if (protRegion is HighTryFinallyRegion)
                        {
                            HighTryFinallyRegion tProtRegion = (HighTryFinallyRegion)protRegion;

                            HighCfgNode finallyInit = GenerateFinallyInit(tProtRegion.FinallyRegion.EntryNode.Value);

                            RegionStack regionStack = new RegionStack(m_regionStack, ehCluster, finallyInit);

                            tryNode = ProcessSubRegion(tProtRegion.TryRegion, regionStack, true);
                            ProcessSubRegion(tProtRegion.FinallyRegion, regionStack, false);
                        }
                        else
                            throw new Exception();

                        newInstructions.Add(new Rpa.Instructions.BranchInstruction(instr.CodeLocation, new HighCfgNodeHandle(tryNode)));
                    }
                    else
                    {
                        newInstructions.Add(instr);
                        if (instr.MayThrow)
                        {
                            cfgNode.Instructions = newInstructions.ToArray();
                            HighCfgNode newNode = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], null);
                            HighCfgNodeHandle destHandle = new HighCfgNodeHandle(newNode);

                            instr.ContinuationEdge = new HighCfgEdge(instr, destHandle);
                            cfgNode = newNode;
                            newInstructions.Clear();
                        }
                    }
                }

                cfgNode.Instructions = newInstructions.ToArray();

                foreach (HighCfgNodeHandle linkHdl in successorLinkHandles)
                    linkHdl.Value = cfgNode;
            }

            private HighCfgNode GenerateFinallyInit(HighCfgNode handler)
            {
                List<HighInstruction> instrs = new List<HighInstruction>();

                HighSsaRegister exceptionReg = new HighSsaRegister(HighValueType.ReferenceValue, m_initPass.ObjectType, null);
                HighSsaRegister routeReg = new HighSsaRegister(HighValueType.ReferenceValue, m_initPass.IntType, null);
                instrs.Add(new Instructions.CatchOrRouteInstruction(null, routeReg, exceptionReg));
                instrs.Add(new Rpa.Instructions.BranchInstruction(null, new HighCfgNodeHandle(handler)));

                return new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            private void AddEscapePaths(HighEscapePathTerminator[] escapePathTerminators)
            {
                foreach (HighEscapePathTerminator terminator in escapePathTerminators)
                {
                    RloInitExceptionsPass pass = m_initPass;
                    if (pass.m_routeCompactionDict.ContainsKey(terminator.EscapePath))
                        throw new RpaCompileException("Duplicate exception escape route");

                    pass.m_routeCompactionDict.Add(terminator.EscapePath, pass.m_routeCompactionDict.Count);
                }
            }

            private HighCfgNode GenerateTryCatchHandler(HighTryCatchRegion tProtRegion)
            {
                if (tProtRegion.CatchHandlers.Length == 0)
                    throw new RpaCompileException("HighTryCatchRegion has no catch handlers");

                List<HighInstruction> startInstrs = new List<HighInstruction>();
                HighSsaRegister exceptionObj = new HighSsaRegister(HighValueType.ReferenceValue, m_initPass.ObjectType, null);
                startInstrs.Add(new Rpa.Instructions.CatchInstruction(null, exceptionObj, m_initPass.ObjectType));

                List<HighInstruction> rethrowInstrs = new List<HighInstruction>();
                Rpa.Instructions.ThrowInstruction throwInstr = new Rpa.Instructions.ThrowInstruction(null, exceptionObj);
                if (m_exceptionNode != null)
                    throwInstr.ExceptionEdge = new HighCfgEdge(throwInstr, new HighCfgNodeHandle(m_exceptionNode));
                rethrowInstrs.Add(throwInstr);

                HighCfgNode fallThroughNode = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], rethrowInstrs.ToArray());

                // Build stack handlers from the bottom up
                HighCatchHandler[] catchHandlers = tProtRegion.CatchHandlers;
                for (int i = 0; i < catchHandlers.Length; i++)
                {
                    HighCatchHandler handler = catchHandlers[catchHandlers.Length - 1 - i];
                    TypeSpecTag catchType = handler.CatchType;
                    if (m_initPass.m_compiler.TypeIsValueType(catchType))
                        throw new RpaCompileException("Exception catch type is value type");

                    HighCfgNode catchNode = handler.Region.EntryNode.Value;
                    Rpa.Instructions.CatchInstruction catchInstr = catchNode.Instructions[0] as Rpa.Instructions.CatchInstruction;
                    if (catchInstr == null)
                        throw new RpaCompileException("Catch handler target isn't a catch instruction");

                    if (catchInstr.Dest.ValueType != HighValueType.ReferenceValue || catchInstr.Dest.Type != catchType)
                        throw new RpaCompileException("Catch handler destination is invalid");

                    HighSsaRegister castCatch = new HighSsaRegister(HighValueType.ReferenceValue, catchType, null);

                    catchNode.Instructions[0] = new Instructions.CopyInstruction(catchInstr.CodeLocation, catchInstr.Dest, castCatch);

                    List<HighInstruction> handlerInstrs = new List<HighInstruction>();
                    handlerInstrs.Add(new Rpa.Instructions.DynamicCastInstruction(null, castCatch, exceptionObj, catchType));
                    handlerInstrs.Add(new Rpa.Instructions.BranchRefNullInstruction(null, castCatch, new HighCfgNodeHandle(fallThroughNode), new HighCfgNodeHandle(catchNode)));

                    fallThroughNode = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], handlerInstrs.ToArray());
                }

                startInstrs.Add(new Rpa.Instructions.BranchInstruction(null, new HighCfgNodeHandle(fallThroughNode)));
                return new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], startInstrs.ToArray());
            }

            private HighCfgNode ProcessSubRegion(HighRegion region, RegionStack regionStack, bool isTry)
            {
                HighCfgNode cfgNode = region.EntryNode.Value;
                RegionProcessor processor = new RegionProcessor(m_initPass, regionStack, isTry, false);
                processor.QueueNode(cfgNode);
                processor.Run();
                return cfgNode;
            }

            public void QueueNode(HighCfgNode cfgNode)
            {
                if (m_alreadyQueuedNodes.Add(cfgNode))
                    m_queuedNodes.Enqueue(cfgNode);
            }
        }

        public RloInitExceptionsPass(Compiler compiler, RloMethodBody methodBody)
        {
            m_compiler = compiler;
            m_methodBody = methodBody;
            m_routeCompactionDict = new Dictionary<uint, int>();
            m_terminators = new Dictionary<uint, HighCfgNode>();
        }

        public void Run()
        {
            RegionProcessor processor = new RegionProcessor(this, null, false, m_methodBody.ReturnType is TypeSpecVoidTag);
            processor.QueueNode(m_methodBody.EntryRegion.EntryNode.Value);
            processor.Run();
        }
    }
}
