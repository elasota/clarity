using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;
using Clarity.Rpa;

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

        private enum NumericStackType
        {
            Int32,
            Int64,
            NativeInt,
            Float32,
            Float64
        }

        private CppRegisterAllocator m_regAllocator;
        private CppBuilder m_builder;
        private ExceptionHandlingRegion m_region;
        private Queue<CfgNode> m_pendingNodes;
        private Dictionary<CfgNode, HighCfgNodeHandle> m_nodesToEmittedNodes;
        private Dictionary<CfgNode, CppCfgNodeOutline> m_nodeOutlines;
        private Dictionary<SsaRegister, HighSsaRegister> m_ssaToEmittedSsa;
        private IDictionary<VReg, HighLocal> m_localLookup;
        private Dictionary<CfgOutboundEdge, CppTranslatedOutboundEdge> m_translatedOutboundEdges;

        public CppRegionEmitter(CppBuilder builder, ExceptionHandlingRegion region, CppRegisterAllocator regAllocator, IDictionary<VReg, Clarity.Rpa.HighLocal> localLookup)
        {
            m_localLookup = localLookup;
            m_region = region;
            m_builder = builder;
            m_regAllocator = regAllocator;
            m_nodesToEmittedNodes = new Dictionary<CfgNode, HighCfgNodeHandle>();
            m_nodeOutlines = new Dictionary<CfgNode, CppCfgNodeOutline>();
            m_ssaToEmittedSsa = new Dictionary<SsaRegister, HighSsaRegister>();
            m_translatedOutboundEdges = new Dictionary<CfgOutboundEdge, CppTranslatedOutboundEdge>();

            m_pendingNodes = new Queue<CfgNode>();

            InternHighCfgNode(region.RootCfgNode);
        }

        private CppTranslatedOutboundEdge InternOutboundEdge(CfgNode node, CfgOutboundEdge edge)
        {
            CppTranslatedOutboundEdge outboundEdge;
            if (m_translatedOutboundEdges.TryGetValue(edge, out outboundEdge))
                return outboundEdge;

            bool needRegTranslation = false;

            int survivingRegsCount = edge.SurvivingRegs.Length;
            CfgNode successorNode = edge.SuccessorNode;

            for (int i = 0; i < survivingRegsCount; i++)
            {
                CLR.CLRTypeSpec outType = edge.SurvivingRegs[i].VType.TypeSpec;
                CLR.CLRTypeSpec inType = successorNode.EntryTypes[i].TypeSpec;

                if (!outType.Equals(inType))
                {
                    needRegTranslation = true;
                    break;
                }
            }

            Clarity.Rpa.HighCfgNodeHandle prevNode = InternHighCfgNode(node);

            if (!needRegTranslation)
            {
                List<Clarity.Rpa.HighSsaRegister> regs = new List<Clarity.Rpa.HighSsaRegister>();
                foreach (SsaRegister reg in edge.SurvivingRegs)
                    regs.Add(InternSsaRegister(reg));
                Clarity.Rpa.HighCfgNodeHandle nextNode = InternHighCfgNode(edge.SuccessorNode);
                outboundEdge = new CppTranslatedOutboundEdge(prevNode, nextNode, regs);
            }
            else
            {
                List<Clarity.Rpa.HighInstruction> instrs = new List<Clarity.Rpa.HighInstruction>();
                List<Clarity.Rpa.HighSsaRegister> regs = new List<Clarity.Rpa.HighSsaRegister>();
                List<Clarity.Rpa.HighPhi> phis = new List<Clarity.Rpa.HighPhi>();

                Clarity.Rpa.HighCfgNodeHandle nextNode = InternHighCfgNode(edge.SuccessorNode);

                MidInstruction[] midInstrs = edge.SuccessorNode.MidInstructions;
                for (int i = 0; i < survivingRegsCount; i++)
                {
                    MidInstruction midInstr = midInstrs[i];
                    if (midInstr.Opcode != MidInstruction.OpcodeEnum.EntryReg)
                        throw new Exception("Internal error");

                    Clarity.Rpa.HighSsaRegister sourceReg = InternSsaRegister(edge.SurvivingRegs[i]);
                    Clarity.Rpa.HighSsaRegister targetReg = InternSsaRegister(midInstr.RegArg);

                    if (!targetReg.IsConstant)
                    {
                        HighSsaRegister importReg = new HighSsaRegister(sourceReg.ValueType, sourceReg.Type, sourceReg.ConstantValue);
                        HighSsaRegister exportReg = new HighSsaRegister(targetReg.ValueType, targetReg.Type, targetReg.ConstantValue);

                        HighPhiLink phiLink = new HighPhiLink(prevNode, sourceReg);
                        phis.Add(new HighPhi(importReg, new HighPhiLink[] { phiLink }));

                        instrs.Add(new Clarity.Rpa.Instructions.PassiveConvertInstruction(
                            midInstr.CodeLocation,
                            exportReg,
                            importReg
                            ));
                        regs.Add(exportReg);
                    }
                    else
                        regs.Add(targetReg);
                }

                instrs.Add(new Clarity.Rpa.Instructions.BranchInstruction(edge.CodeLocation, nextNode));

                Clarity.Rpa.HighCfgNode cfgNode = new HighCfgNode(new Clarity.Rpa.HighCfgNodeHandle[] { prevNode }, phis.ToArray(), instrs.ToArray());
                Clarity.Rpa.HighCfgNodeHandle cfgNodeHandle = new Clarity.Rpa.HighCfgNodeHandle(cfgNode);

                outboundEdge = new CppTranslatedOutboundEdge(cfgNodeHandle, cfgNodeHandle, regs);
            }

            m_translatedOutboundEdges.Add(edge, outboundEdge);
            return outboundEdge;
        }

        private void AliasSsaRegister(Clarity.Rpa.HighSsaRegister src, SsaRegister copy)
        {
            if (m_ssaToEmittedSsa.ContainsKey(copy))
                throw new Exception();
            m_ssaToEmittedSsa.Add(copy, src);
        }

        private Clarity.Rpa.HighSsaRegister InternSsaRegister(SsaRegister ssaRegister)
        {
            if (ssaRegister == null)
                return null;

            Clarity.Rpa.HighSsaRegister highSsa;
            if (!m_ssaToEmittedSsa.TryGetValue(ssaRegister, out highSsa))
            {
                Clarity.Rpa.HighValueType valType;
                object constValue = null;

                switch (ssaRegister.VType.ValType)
                {
                    case VType.ValTypeEnum.ManagedPtr:
                        valType = Clarity.Rpa.HighValueType.ManagedPtr;
                        break;
                    case VType.ValTypeEnum.ConstantValue:
                        constValue = ssaRegister.ConstantValue;
                        valType = Clarity.Rpa.HighValueType.ConstantValue;
                        break;
                    case VType.ValTypeEnum.ConstantReference:
                        constValue = ssaRegister.ConstantValue;
                        valType = Clarity.Rpa.HighValueType.ConstantString;
                        break;
                    case VType.ValTypeEnum.ReferenceValue:
                        valType = Clarity.Rpa.HighValueType.ReferenceValue;
                        break;
                    case VType.ValTypeEnum.Null:
                        valType = Clarity.Rpa.HighValueType.Null;
                        break;
                    case VType.ValTypeEnum.ValueValue:
                        valType = Clarity.Rpa.HighValueType.ValueValue;
                        break;
                    default:
                        throw new ArgumentException();
                }

                highSsa = new Clarity.Rpa.HighSsaRegister(valType, RpaTagFactory.CreateTypeTag(ssaRegister.VType.TypeSpec), constValue);
                m_ssaToEmittedSsa.Add(ssaRegister, highSsa);
            }

            return highSsa;
        }

        private Clarity.Rpa.HighCfgNodeHandle InternHighCfgNode(CfgNode cfgNode)
        {
            Clarity.Rpa.HighCfgNodeHandle highNode = null;
            if (!m_nodesToEmittedNodes.TryGetValue(cfgNode, out highNode))
            {
                highNode = new Clarity.Rpa.HighCfgNodeHandle();
                CppCfgNodeOutline outline = new CppCfgNodeOutline(cfgNode);

                m_nodesToEmittedNodes.Add(cfgNode, highNode);
                m_nodeOutlines.Add(cfgNode, outline);

                m_pendingNodes.Enqueue(cfgNode);
            }

            return highNode;
        }

        public Clarity.Rpa.HighRegion Emit()
        {
            //writer.WriteLine("Escape terminators:");
            //foreach (KeyValuePair<uint, CfgNode> terminator in m_region.EscapeTerminators)
            //    writer.WriteLine("    " + terminator.Key + " --> " + m_regAllocator.TargetIDForCfgNode(terminator.Value));
            
            CfgNode lastCfgNode = null;
            while (m_pendingNodes.Count > 0)
            {
                CfgNode cfgNode = m_pendingNodes.Dequeue();

                EmitCfgNode(cfgNode);
                lastCfgNode = cfgNode;

                foreach (VReg vReg in m_regAllocator.AllRegisters)
                    if (vReg.IsAlive)
                        throw new Exception("VReg leaked");
            }

            Clarity.Rpa.HighCfgNodeHandle hdl = InternHighCfgNode(m_region.RootCfgNode);
            return new Clarity.Rpa.HighRegion(hdl);
        }

        public static string StorageLocForVReg(VReg vReg, bool makeLive, bool zombify)
        {
            return vReg.SlotName;
        }

        private CLR.CLRTypeSpec TypeSpecForArrayIndex(CLR.CLRTypeSpec indexBaseSpec)
        {
            NumericStackType indexNst = StackTypeForTypeSpec(indexBaseSpec);
            CLR.CLRSigType.ElementType indexElementType;

            if (indexNst == NumericStackType.Int32)
                indexElementType = CLR.CLRSigType.ElementType.I4;
            else if (indexNst == NumericStackType.NativeInt)
                indexElementType = CLR.CLRSigType.ElementType.I;    // CLARITYTODO: Test UIntPtr
            else
                throw new ArgumentException("Unusual array index type");

            return m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(indexElementType));
        }

        private NumericStackType StackTypeForTypeSpec(CLR.CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLR.CLRTypeSpecClass)
            {
                CLR.CLRTypeSpecClass asClass = (CLR.CLRTypeSpecClass)typeSpec;
                if (asClass.TypeDef.ContainerClass == null && asClass.TypeDef.TypeNamespace == "System")
                {
                    string typeName = asClass.TypeDef.TypeName;
                    if (typeName == "Char" ||
                        typeName == "Boolean" ||
                        typeName == "Byte" ||
                        typeName == "SByte" ||
                        typeName == "Int16" ||
                        typeName == "UInt16" ||
                        typeName == "Int32" ||
                        typeName == "UInt32")
                        return NumericStackType.Int32;
                    if (typeName == "Int64" || typeName == "UInt64")
                        return NumericStackType.Int64;
                    if (typeName == "IntPtr" || typeName == "UIntPtr")
                        return NumericStackType.NativeInt;
                    if (typeName == "Single")
                        return NumericStackType.Float32;
                    if (typeName == "Double")
                        return NumericStackType.Float64;
                }

                CppClass typeClass = m_builder.GetCachedClass(typeSpec);
                if (typeClass.IsEnum)
                    return StackTypeForTypeSpec(typeClass.GetEnumUnderlyingType());
            }
            throw new Exception("Unrecognized numeric type spec");
        }

        private CLR.CLRTypeSpec TypeSpecForNumericBinaryOp(SsaRegister regA, SsaRegister regB, bool isUnsigned)
        {
            return TypeSpecForNumericStackType(NumericStackTypeForNumericBinaryOp(regA, regB), isUnsigned);
        }

        private CLR.CLRTypeSpec TypeSpecForNumericStackType(NumericStackType combinedType, bool isUnsigned)
        {

            CLR.CLRSigType.ElementType elementType;
            switch (combinedType)
            {
                case NumericStackType.Int32:
                    elementType = isUnsigned ? CLR.CLRSigType.ElementType.U4 : CLR.CLRSigType.ElementType.I4;
                    break;
                case NumericStackType.Int64:
                    elementType = isUnsigned ? CLR.CLRSigType.ElementType.U8 : CLR.CLRSigType.ElementType.I8;
                    break;
                case NumericStackType.NativeInt:
                    elementType = isUnsigned ? CLR.CLRSigType.ElementType.U : CLR.CLRSigType.ElementType.I;
                    break;
                case NumericStackType.Float32:
                    if (isUnsigned)
                        throw new ArgumentException();
                    elementType = CLR.CLRSigType.ElementType.R4;
                    break;
                case NumericStackType.Float64:
                    if (isUnsigned)
                        throw new ArgumentException();
                    elementType = CLR.CLRSigType.ElementType.R8;
                    break;
                default:
                    throw new Exception("Unexpected operand type in comparison");
            };

            return m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(elementType));
        }

        private NumericStackType NumericStackTypeForNumericBinaryOp(SsaRegister regA, SsaRegister regB)
        {
            // III.1.5 table III.4
            NumericStackType stackTypeA = StackTypeForTypeSpec(regA.VType.TypeSpec);
            NumericStackType stackTypeB = StackTypeForTypeSpec(regB.VType.TypeSpec);
            NumericStackType combinedType;

            if (stackTypeA == NumericStackType.Int32)
            {
                if (stackTypeB == NumericStackType.NativeInt)
                    combinedType = NumericStackType.NativeInt;
                else if (stackTypeB == NumericStackType.Int32)
                    combinedType = NumericStackType.Int32;
                else
                    throw new Exception("Unexpected binary numeric operation operands");
            }
            else if (stackTypeA == NumericStackType.Int64)
            {
                if (stackTypeB == NumericStackType.Int64)
                    combinedType = NumericStackType.Int64;
                else
                    throw new Exception("Unexpected binary numeric operation operands");
            }
            else if (stackTypeA == NumericStackType.NativeInt)
            {
                if (stackTypeB == NumericStackType.NativeInt
                    || stackTypeB == NumericStackType.Int32)
                    combinedType = NumericStackType.NativeInt;
                else
                    throw new Exception("Unexpected binary numeric operation operands");
            }
            else if (stackTypeB == NumericStackType.Float32)
            {
                if (stackTypeB == NumericStackType.Float32)
                    combinedType = NumericStackType.Float32;
                else if (stackTypeB == NumericStackType.Float64)
                    combinedType = NumericStackType.Float32;
                else
                    throw new Exception("Unexpected binary numeric operation operands");
            }
            else if (stackTypeA == NumericStackType.Float64)
            {
                if (stackTypeB == NumericStackType.Float32
                    || stackTypeB == NumericStackType.Float64)
                    combinedType = NumericStackType.Float64;
                else
                    throw new Exception("Unexpected binary numeric operation operands");
            }
            else
                throw new Exception("Unexpected binary numeric operation operands");

            return combinedType;
        }

        private CLR.CLRTypeSpec StackTypeSpecForSsaReg(SsaRegister regA, bool isUnsigned)
        {
            NumericStackType stackTypeA = StackTypeForTypeSpec(regA.VType.TypeSpec);

            CLR.CLRSigType.ElementType elementType;
            switch (stackTypeA)
            {
                case NumericStackType.Float32:
                    elementType = CLR.CLRSigType.ElementType.R4;
                    break;
                case NumericStackType.Float64:
                    elementType = CLR.CLRSigType.ElementType.R8;
                    break;
                case NumericStackType.Int32:
                    elementType = isUnsigned ? CLR.CLRSigType.ElementType.U4 : CLR.CLRSigType.ElementType.I4;
                    break;
                case NumericStackType.Int64:
                    elementType = isUnsigned ? CLR.CLRSigType.ElementType.U8 : CLR.CLRSigType.ElementType.I8;
                    break;
                case NumericStackType.NativeInt:
                    elementType = isUnsigned ? CLR.CLRSigType.ElementType.U : CLR.CLRSigType.ElementType.I;
                    break;
                default:
                    throw new ArgumentException();
            }
            return m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(elementType));
        }

        private void EmitCfgNode(CfgNode cfgNode)
        {
            // Build predecessor list
            List<CppTranslatedOutboundEdge> predecessorEdges = new List<CppTranslatedOutboundEdge>();
            foreach (CfgNode pred in cfgNode.Predecessors)
            {
                // Find the successor edge
                CppTranslatedOutboundEdge edge = null;
                foreach (CfgOutboundEdge outEdge in pred.Successors)
                {
                    if (outEdge.SuccessorNode == cfgNode)
                    {
                        edge = InternOutboundEdge(pred, outEdge);
                        break;
                    }
                }

                if (edge == null)
                    throw new Exception("Mismatched CFG edge");

                predecessorEdges.Add(edge);
            }

            List<Clarity.Rpa.HighInstruction> outInstructions = new List<Clarity.Rpa.HighInstruction>();
            List<Clarity.Rpa.HighPhi> outPhis = new List<Clarity.Rpa.HighPhi>();

            Clarity.Rpa.HighCfgNodeHandle highNode = m_nodesToEmittedNodes[cfgNode];
            CppCfgNodeOutline outline = m_nodeOutlines[cfgNode];
            List<SsaRegister> leakedRegs = new List<SsaRegister>();

            int numContinuedRegs = 0;

            int debugInstrNum = -1;
            MidInstruction[] midInstrs = cfgNode.MidInstructions;

            bool fallthroughMerged = false;

            foreach (MidInstruction midInstr in midInstrs)
            {
                debugInstrNum++;
                switch (midInstr.Opcode)
                {
                    case MidInstruction.OpcodeEnum.AllocObject:
                        {
                            Clarity.Rpa.HighSsaRegister dest = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.TypeSpecTag type = RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg);
                            outInstructions.Add(new Clarity.Rpa.Instructions.AllocObjInstruction(midInstr.CodeLocation, dest, type));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.CallMethod:
                    case MidInstruction.OpcodeEnum.CallConstructor:
                    case MidInstruction.OpcodeEnum.ConstrainedCallMethod:
                        {
                            CppMethodSpec methodSpec = midInstr.MethodSpecArg;
                            CppMethod cppMethod = methodSpec.CppMethod;
                            CppClass thisClass = m_builder.GetCachedClass(cppMethod.DeclaredInClassSpec);

                            SsaRegister returnReg = midInstr.RegArg;
                            SsaRegister thisReg = midInstr.RegArg2;
                            List<Clarity.Rpa.HighSsaRegister> parameters = new List<Clarity.Rpa.HighSsaRegister>();

                            if (!cppMethod.MethodDef.Static && midInstr.Opcode != MidInstruction.OpcodeEnum.ConstrainedCallMethod)
                                thisReg = EmitPassiveConversion_PermitRefs(midInstr.CodeLocation, thisReg, cppMethod.DeclaredInClassSpec, outline, outInstructions);

                            // CLARITYTODO: Verify that this works with generic method parameters
                            for (int pIdx = 0; pIdx < midInstr.RegArgs.Length; pIdx++)
                            {
                                SsaRegister inReg = EmitPassiveConversion_PermitRefs(midInstr.CodeLocation, midInstr.RegArgs[pIdx], cppMethod.MethodSignature.ParamTypes[pIdx].Type, outline, outInstructions);
                                parameters.Add(InternSsaRegister(inReg));
                            }

                            Clarity.Rpa.HighSsaRegister outDestReg = null;

                            if (returnReg != null)
                                outDestReg = InternSsaRegister(returnReg);

                            Clarity.Rpa.TypeSpecTag constraintTag = null;

                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.ConstrainedCallMethod)
                                constraintTag = RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg);

                            Clarity.Rpa.HighSsaRegister instanceReg = null;
                            if (!cppMethod.MethodDef.Static)
                                instanceReg = InternSsaRegister(thisReg);

                            CLR.CLRMethodSignatureInstance methodSig = cppMethod.MethodSignature;

                            int numParams = parameters.Count;
                            if (numParams != methodSig.ParamTypes.Length)
                                throw new Exception("Internal error: CallMethod param count mismatch");

                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.CallMethod || midInstr.Opcode == MidInstruction.OpcodeEnum.CallConstructor)
                            {
                                if (cppMethod.MethodDef.Static)
                                {
                                    outInstructions.Add(new Clarity.Rpa.Instructions.CallStaticMethodInstruction(
                                        midInstr.CodeLocation,
                                        outDestReg,
                                        RpaTagFactory.CreateMethodSpec(Clarity.Rpa.MethodSlotType.Static, methodSpec),
                                        parameters.ToArray()
                                        ));
                                }
                                else
                                {
                                    outInstructions.Add(new Clarity.Rpa.Instructions.CallInstanceMethodInstruction(
                                        midInstr.CodeLocation,
                                        outDestReg,
                                        RpaTagFactory.CreateMethodSpec(Clarity.Rpa.MethodSlotType.Instance, methodSpec),
                                        instanceReg,
                                        parameters.ToArray()
                                        ));
                                }
                            }
                            else if (midInstr.Opcode == MidInstruction.OpcodeEnum.ConstrainedCallMethod)
                            {
                                outInstructions.Add(new Clarity.Rpa.Instructions.CallConstrainedMethodInstruction(
                                    midInstr.CodeLocation,
                                    outDestReg,
                                    constraintTag,
                                    RpaTagFactory.CreateMethodSpec(Clarity.Rpa.MethodSlotType.Instance, methodSpec),
                                    instanceReg,
                                    parameters.ToArray()
                                    ));
                            }
                        }
                        break;
                    case MidInstruction.OpcodeEnum.CallVirtualMethod:
                    case MidInstruction.OpcodeEnum.ConstrainedCallVirtualMethod:
                        {
                            CppMethodSpec methodSpec = midInstr.MethodSpecArg;
                            CppMethod cppMethod = methodSpec.CppMethod;
                            CppClass thisClass = m_builder.GetCachedClass(cppMethod.DeclaredInClassSpec);

                            Clarity.Rpa.HighSsaRegister returnReg = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister thisReg = null;
                            List<Clarity.Rpa.HighSsaRegister> paramRegs = new List<Clarity.Rpa.HighSsaRegister>();

                            if (midInstr.Opcode != MidInstruction.OpcodeEnum.ConstrainedCallVirtualMethod)
                                thisReg = InternSsaRegister(EmitPassiveConversion_PermitRefs(midInstr.CodeLocation, midInstr.RegArg2, cppMethod.DeclaredInClassSpec, outline, outInstructions));
                            else
                                thisReg = InternSsaRegister(midInstr.RegArg2);

                            for (int pIdx = 0; pIdx < midInstr.RegArgs.Length; pIdx++)
                                paramRegs.Add(InternSsaRegister(EmitPassiveConversion_PermitRefs(midInstr.CodeLocation, midInstr.RegArgs[pIdx], cppMethod.MethodSignature.ParamTypes[pIdx].Type, outline, outInstructions)));

                            CppVtableSlot vtableSlot = cppMethod.CreatesSlot;
                            if (vtableSlot == null)
                                vtableSlot = cppMethod.ReplacesStandardSlot;
                            if (vtableSlot == null)
                                throw new Exception("Internal error: Couldn't resolve vtable slot for method");

                            Clarity.Rpa.TypeSpecTag constraintType = null;
                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.ConstrainedCallVirtualMethod)
                                constraintType = RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg);

                            if (cppMethod.MethodDef.Static)
                                throw new Exception();

                            CLR.CLRMethodSignatureInstance methodSig = cppMethod.MethodSignature;

                            int numParams = paramRegs.Count;
                            if (numParams != methodSig.ParamTypes.Length)
                                throw new Exception("Internal error: CallMethod param count mismatch");

                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.CallVirtualMethod)
                            {
                                outInstructions.Add(new Clarity.Rpa.Instructions.CallVirtualMethodInstruction(
                                    midInstr.CodeLocation,
                                    returnReg,
                                    RpaTagFactory.CreateMethodSpec(Clarity.Rpa.MethodSlotType.Virtual, methodSpec),
                                    thisReg,
                                    paramRegs.ToArray()
                                ));
                            }
                            else if (midInstr.Opcode == MidInstruction.OpcodeEnum.ConstrainedCallVirtualMethod)
                                outInstructions.Add(new Clarity.Rpa.Instructions.CallConstrainedVirtualMethodInstruction(
                                    midInstr.CodeLocation,
                                    returnReg,
                                    constraintType,
                                    RpaTagFactory.CreateMethodSpec(Clarity.Rpa.MethodSlotType.Virtual, methodSpec),
                                    thisReg,
                                    paramRegs.ToArray()
                                    ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.KillReg:
                        break;
                    case MidInstruction.OpcodeEnum.LivenReg:
                        {
                            SsaRegister reg = midInstr.RegArg;
                            reg.GenerateUniqueID(m_regAllocator);
                            reg.MakeUsable();
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Return:
                        outInstructions.Add(new Clarity.Rpa.Instructions.ReturnInstruction(midInstr.CodeLocation));
                        break;
                    case MidInstruction.OpcodeEnum.ReturnValue:
                        {
                            SsaRegister returnRegister = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.TypeSpecArg, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.ReturnValueInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(returnRegister)));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadReg_ManagedPtr:
                        {
                            Clarity.Rpa.HighLocal lcl = m_localLookup[midInstr.VRegArg];
                            Clarity.Rpa.HighSsaRegister dest = InternSsaRegister(midInstr.RegArg);
                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadLocalInstruction(
                                midInstr.CodeLocation,
                                dest,
                                lcl
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadReg_Value:
                        {
                            CLR.CLRTypeSpec srcSpec = midInstr.VRegArg.VType.TypeSpec;
                            CLR.CLRTypeSpec destSpec = midInstr.RegArg.VType.TypeSpec;
                            if (srcSpec.Equals(destSpec))
                            {
                                outInstructions.Add(new Clarity.Rpa.Instructions.LoadLocalInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(midInstr.RegArg),
                                    m_localLookup[midInstr.VRegArg]
                                    ));
                            }
                            else
                            {
                                SsaRegister tempReg = new SsaRegister(midInstr.VRegArg.VType);
                                tempReg.MakeUsable();
                                tempReg.GenerateUniqueID(m_regAllocator);

                                outline.AddRegister(tempReg);


                                outInstructions.Add(new Clarity.Rpa.Instructions.LoadLocalInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(tempReg),
                                    m_localLookup[midInstr.VRegArg]
                                    ));

                                outInstructions.Add(new Clarity.Rpa.Instructions.PassiveConvertInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(midInstr.RegArg),
                                    InternSsaRegister(tempReg)
                                    ));
                            }
                        }
                        break;
                    case MidInstruction.OpcodeEnum.StoreReg_ManagedPtr:
                        outInstructions.Add(new Clarity.Rpa.Instructions.StoreLocalInstruction(
                            midInstr.CodeLocation,
                            m_localLookup[midInstr.VRegArg],
                            InternSsaRegister(midInstr.RegArg)
                            ));
                        break;
                    case MidInstruction.OpcodeEnum.StoreReg_Value:
                        {
                            SsaRegister srcReg = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.VRegArg.VType.TypeSpec, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.StoreLocalInstruction(
                                midInstr.CodeLocation,
                                m_localLookup[midInstr.VRegArg],
                                InternSsaRegister(srcReg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.beq_ref:
                    case MidInstruction.OpcodeEnum.bne_ref:
                        {
                            fallthroughMerged = true;
                            CppTranslatedOutboundEdge edgeA = InternOutboundEdge(cfgNode, midInstr.CfgEdgeArg);
                            CppTranslatedOutboundEdge edgeB = InternOutboundEdge(cfgNode, cfgNode.FallThroughEdge);

                            CppTranslatedOutboundEdge trueEdge, falseEdge;
                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.beq_ref)
                            {
                                trueEdge = edgeA;
                                falseEdge = edgeB;
                            }
                            else if (midInstr.Opcode == MidInstruction.OpcodeEnum.bne_ref)
                            {
                                trueEdge = edgeB;
                                falseEdge = edgeA;
                            }
                            else
                                throw new Exception();

                            Clarity.Rpa.HighSsaRegister regA = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister regB = InternSsaRegister(midInstr.RegArg2);

                            outInstructions.Add(new Clarity.Rpa.Instructions.BranchCompareRefsInstruction(midInstr.CodeLocation, regA, regB, trueEdge.NextNode, falseEdge.NextNode));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.cne_ref:
                    case MidInstruction.OpcodeEnum.ceq_ref:
                        {
                            int trueValue, falseValue;
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg);

                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.ceq_ref)
                            {
                                trueValue = 1;
                                falseValue = 0;
                            }
                            else if (midInstr.Opcode == MidInstruction.OpcodeEnum.cne_ref)
                            {
                                trueValue = 0;
                                falseValue = 1;
                            }
                            else
                                throw new Exception();

                            Clarity.Rpa.HighSsaRegister regA = InternSsaRegister(midInstr.RegArg2);
                            Clarity.Rpa.HighSsaRegister regB = InternSsaRegister(midInstr.RegArg3);

                            outInstructions.Add(new Clarity.Rpa.Instructions.CompareRefsInstruction(midInstr.CodeLocation, destReg, regA, regB, trueValue, falseValue));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.beq_val:
                    case MidInstruction.OpcodeEnum.bne_val:
                    case MidInstruction.OpcodeEnum.bge:
                    case MidInstruction.OpcodeEnum.bgt:
                    case MidInstruction.OpcodeEnum.ble:
                    case MidInstruction.OpcodeEnum.blt:
                    case MidInstruction.OpcodeEnum.clt:
                    case MidInstruction.OpcodeEnum.cgt:
                    case MidInstruction.OpcodeEnum.ceq_numeric:
                        {
                            bool isBranch;
                            SsaRegister leftReg, rightReg;

                            switch (midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.beq_val:
                                case MidInstruction.OpcodeEnum.bne_val:
                                case MidInstruction.OpcodeEnum.bge:
                                case MidInstruction.OpcodeEnum.bgt:
                                case MidInstruction.OpcodeEnum.ble:
                                case MidInstruction.OpcodeEnum.blt:
                                    isBranch = true;
                                    leftReg = midInstr.RegArg;
                                    rightReg = midInstr.RegArg2;
                                    break;
                                case MidInstruction.OpcodeEnum.clt:
                                case MidInstruction.OpcodeEnum.cgt:
                                case MidInstruction.OpcodeEnum.ceq_numeric:
                                    isBranch = false;
                                    leftReg = midInstr.RegArg2;
                                    rightReg = midInstr.RegArg3;
                                    break;
                                default:
                                    throw new Exception();
                            }

                            bool isUnsigned = midInstr.FlagArg;
                            bool isReversed = false;

                            NumericStackType nst = NumericStackTypeForNumericBinaryOp(leftReg, rightReg);

                            if (isUnsigned && (nst == NumericStackType.Float32 || nst == NumericStackType.Float64))
                            {
                                isUnsigned = false;
                                isReversed = true;
                            }

                            CLR.CLRTypeSpec comparisonSpec = this.TypeSpecForNumericBinaryOp(leftReg, rightReg, isUnsigned);

                            Clarity.Rpa.HighSsaRegister leftHighReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, leftReg, comparisonSpec, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister rightHighReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, rightReg, comparisonSpec, outline, outInstructions));

                            Clarity.Rpa.Instructions.NumberCompareOperation op;

                            switch (midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.blt:
                                case MidInstruction.OpcodeEnum.clt:
                                    op = isReversed ?
                                        Clarity.Rpa.Instructions.NumberCompareOperation.GreaterOrEqual :
                                        Clarity.Rpa.Instructions.NumberCompareOperation.LessThan;
                                    break;
                                case MidInstruction.OpcodeEnum.bgt:
                                case MidInstruction.OpcodeEnum.cgt:
                                    op = isReversed ?
                                        Clarity.Rpa.Instructions.NumberCompareOperation.LessOrEqual :
                                        Clarity.Rpa.Instructions.NumberCompareOperation.GreaterThan;
                                    break;
                                case MidInstruction.OpcodeEnum.ble:
                                    op = isReversed ?
                                        Clarity.Rpa.Instructions.NumberCompareOperation.GreaterThan :
                                        Clarity.Rpa.Instructions.NumberCompareOperation.LessOrEqual;
                                    break;
                                case MidInstruction.OpcodeEnum.bge:
                                    op = isReversed ?
                                        Clarity.Rpa.Instructions.NumberCompareOperation.LessThan :
                                        Clarity.Rpa.Instructions.NumberCompareOperation.GreaterOrEqual;
                                    break;
                                case MidInstruction.OpcodeEnum.beq_val:
                                case MidInstruction.OpcodeEnum.ceq_numeric:
                                    op = isReversed ?
                                        Clarity.Rpa.Instructions.NumberCompareOperation.NotEqual :
                                        Clarity.Rpa.Instructions.NumberCompareOperation.Equal;
                                    break;
                                case MidInstruction.OpcodeEnum.bne_val:
                                    op = isReversed ?
                                        Clarity.Rpa.Instructions.NumberCompareOperation.Equal :
                                        Clarity.Rpa.Instructions.NumberCompareOperation.NotEqual;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            Clarity.Rpa.Instructions.NumberArithType numType;

                            switch (nst)
                            {
                                case NumericStackType.Float32:
                                    numType = Clarity.Rpa.Instructions.NumberArithType.Float32;
                                    break;
                                case NumericStackType.Float64:
                                    numType = Clarity.Rpa.Instructions.NumberArithType.Float64;
                                    break;
                                case NumericStackType.Int32:
                                    numType = isUnsigned ?
                                        Clarity.Rpa.Instructions.NumberArithType.UInt32 :
                                        Clarity.Rpa.Instructions.NumberArithType.Int32;
                                    break;
                                case NumericStackType.Int64:
                                    numType = isUnsigned ?
                                        Clarity.Rpa.Instructions.NumberArithType.UInt64 :
                                        Clarity.Rpa.Instructions.NumberArithType.Int64;
                                    break;
                                case NumericStackType.NativeInt:
                                    numType = isUnsigned ?
                                        Clarity.Rpa.Instructions.NumberArithType.NativeUInt :
                                        Clarity.Rpa.Instructions.NumberArithType.NativeInt;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            if (isBranch)
                            {
                                fallthroughMerged = true;
                                CppTranslatedOutboundEdge trueEdge = InternOutboundEdge(cfgNode, midInstr.CfgEdgeArg);
                                CppTranslatedOutboundEdge falseEdge = InternOutboundEdge(cfgNode, cfgNode.FallThroughEdge);

                                if (isReversed)
                                {
                                    CppTranslatedOutboundEdge temp = trueEdge;
                                    trueEdge = falseEdge;
                                    falseEdge = temp;
                                }

                                outInstructions.Add(new Clarity.Rpa.Instructions.BranchCompareNumbersInstruction(
                                    midInstr.CodeLocation,
                                    op,
                                    numType,
                                    leftHighReg,
                                    rightHighReg,
                                    trueEdge.NextNode,
                                    falseEdge.NextNode
                                    ));
                            }
                            else
                            {
                                int trueValue = 0;
                                int falseValue = 1;

                                if (isReversed)
                                {
                                    trueValue = 0;
                                    falseValue = 1;
                                }

                                Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg);

                                outInstructions.Add(new Clarity.Rpa.Instructions.CompareNumbersInstruction(
                                midInstr.CodeLocation,
                                    destReg,
                                    op,
                                    numType,
                                    leftHighReg,
                                    rightHighReg,
                                    trueValue,
                                    falseValue
                                    ));
                            }
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadArgA_Value:
                        {
                            Clarity.Rpa.HighSsaRegister dest = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighLocal src = m_localLookup[midInstr.VRegArg];

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetLocalPtrInstruction(
                                midInstr.CodeLocation,
                                dest,
                                src
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.brzero:
                    case MidInstruction.OpcodeEnum.brnotzero:
                        {
                            SsaRegister inReg = midInstr.RegArg;
                            CLR.CLRTypeSpec targetType;
                            object zeroValue;

                            CLR.CLRTypeSpecClass cls = (CLR.CLRTypeSpecClass)inReg.VType.TypeSpec;
                            string typeName = cls.TypeDef.TypeName;
                            Clarity.Rpa.Instructions.NumberArithType arithType;

                            if (typeName == "Char" || typeName == "Boolean" ||
                                typeName == "Byte" || typeName == "UInt16" ||
                                typeName == "UInt32")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.Int32, true);
                                zeroValue = (uint)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.UInt32;
                            }
                            else if (typeName == "SByte" || typeName == "Int16" || typeName == "Int32")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.Int32, false);
                                zeroValue = (int)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.Int32;
                            }
                            else if (typeName == "UInt64")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.Int64, true);
                                zeroValue = (ulong)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.UInt64;
                            }
                            else if (typeName == "Int64")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.Int64, false);
                                zeroValue = (long)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.Int64;
                            }
                            else if (typeName == "IntPtr")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.NativeInt, false);
                                zeroValue = (long)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.NativeInt;
                            }
                            else if (typeName == "UIntPtr")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.NativeInt, true);
                                zeroValue = (ulong)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.NativeUInt;
                            }
                            else if (typeName == "Single")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.Float32, false);
                                zeroValue = (float)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.Float32;
                            }
                            else if (typeName == "Double")
                            {
                                targetType = TypeSpecForNumericStackType(NumericStackType.Float64, false);
                                zeroValue = (double)0;
                                arithType = Clarity.Rpa.Instructions.NumberArithType.Float64;
                            }
                            else
                                throw new Exception();

                            fallthroughMerged = true;
                            CppTranslatedOutboundEdge trueEdge = InternOutboundEdge(cfgNode, midInstr.CfgEdgeArg);
                            CppTranslatedOutboundEdge falseEdge = InternOutboundEdge(cfgNode, cfgNode.FallThroughEdge);

                            Clarity.Rpa.Instructions.NumberCompareOperation compareOp;
                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.brzero)
                                compareOp = Clarity.Rpa.Instructions.NumberCompareOperation.Equal;
                            else if (midInstr.Opcode == MidInstruction.OpcodeEnum.brnotzero)
                                compareOp = Clarity.Rpa.Instructions.NumberCompareOperation.NotEqual;
                            else
                                throw new Exception();

                            SsaRegister converted = EmitPassiveConversion(midInstr.CodeLocation, inReg, targetType, outline, outInstructions);
                            Clarity.Rpa.TypeSpecTag typeTag = RpaTagFactory.CreateTypeTag(targetType);

                            outInstructions.Add(new Clarity.Rpa.Instructions.BranchCompareNumbersInstruction(
                                midInstr.CodeLocation,
                                compareOp,
                                arithType,
                                InternSsaRegister(converted),
                                new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ConstantValue, typeTag, zeroValue),
                                trueEdge.NextNode,
                                falseEdge.NextNode
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.brnull:
                    case MidInstruction.OpcodeEnum.brnotnull:
                        {
                            fallthroughMerged = true;
                            CppTranslatedOutboundEdge isNullEdge = InternOutboundEdge(cfgNode, midInstr.CfgEdgeArg);
                            CppTranslatedOutboundEdge isNotNullEdge = InternOutboundEdge(cfgNode, cfgNode.FallThroughEdge);

                            if (midInstr.Opcode == MidInstruction.OpcodeEnum.brnotnull)
                            {
                                CppTranslatedOutboundEdge temp = isNullEdge;
                                isNullEdge = isNotNullEdge;
                                isNotNullEdge = temp;
                            }

                            outInstructions.Add(new Clarity.Rpa.Instructions.BranchRefNullInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg),
                                isNullEdge.NextNode,
                                isNotNullEdge.NextNode
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LeakReg:
                        {
                            leakedRegs.Add(midInstr.RegArg);
                        }
                        break;
                    case MidInstruction.OpcodeEnum.EntryReg:
                        {
                            SsaRegister reg = midInstr.RegArg;

                            reg.GenerateUniqueID(m_regAllocator);
                            reg.MakeUsable();

                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(reg);

                            // Blocks can have constants on entry if all predecessors have the same value
                            if (!destReg.IsConstant)
                            {
                                List<Clarity.Rpa.HighPhiLink> phiLinks = new List<Clarity.Rpa.HighPhiLink>();

                                foreach (CppTranslatedOutboundEdge predEdge in predecessorEdges)
                                    phiLinks.Add(new Clarity.Rpa.HighPhiLink(predEdge.PrevNode, predEdge.Regs[numContinuedRegs]));

                                outPhis.Add(new Clarity.Rpa.HighPhi(
                                    InternSsaRegister(reg),
                                    phiLinks.ToArray()
                                    ));
                            }

                            numContinuedRegs++;
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Throw:
                        {
                            CLR.CLRTypeSpec objSpec = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(CLR.CLRSigType.ElementType.OBJECT));

                            SsaRegister converted = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, objSpec, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.ThrowInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(converted)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.NewSZArray:
                        {
                            NumericStackType indexNst = StackTypeForTypeSpec(midInstr.RegArg2.VType.TypeSpec);

                            CLR.CLRTypeSpec indexSpec = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(CLR.CLRSigType.ElementType.I));

                            SsaRegister indexReg = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg2, indexSpec, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.AllocArrayInstruction(
                                midInstr.CodeLocation,
                                    InternSsaRegister(midInstr.RegArg),
                                    new Clarity.Rpa.HighSsaRegister[] { InternSsaRegister(indexReg) },
                                    RpaTagFactory.CreateTypeTag(midInstr.RegArg2.VType.TypeSpec)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_ManagedPtr:
                        {
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg2);
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, destReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.PtrFieldInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                objReg,
                                midInstr.StrArg
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadPtrInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                addrReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldA_ManagedPtr:
                        {
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg2);
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(midInstr.RegArg);

                            outInstructions.Add(new Clarity.Rpa.Instructions.PtrFieldInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                objReg,
                                midInstr.StrArg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_Object:
                        {
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg2);
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.TypeSpecArg, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, destReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.RefFieldInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                objReg,
                                midInstr.StrArg,
                                RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg)
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadPtrInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                addrReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldA_Object:
                        {
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg2);
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.TypeSpecArg, outline, outInstructions));

                            outInstructions.Add(new Clarity.Rpa.Instructions.RefFieldInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                objReg,
                                midInstr.StrArg,
                                RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_Value:
                        {
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg2);
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(midInstr.RegArg);

                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadValueFieldInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                objReg,
                                midInstr.StrArg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadRegA:
                        {
                            Clarity.Rpa.HighLocal local = m_localLookup[midInstr.VRegArg];
                            Clarity.Rpa.HighSsaRegister dest = InternSsaRegister(midInstr.RegArg);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetLocalPtrInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg),
                                m_localLookup[midInstr.VRegArg]
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayElem:
                        {
                            CLR.CLRTypeSpec indexSpec = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(CLR.CLRSigType.ElementType.I));
                            Clarity.Rpa.HighSsaRegister arrayReg = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister indexReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg2, indexSpec, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg3);
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, destReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetArrayElementPtrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                arrayReg,
                                new Clarity.Rpa.HighSsaRegister[] { indexReg }
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadPtrInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                addrReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayElemAddr:
                        {
                            CLR.CLRTypeSpec indexSpec = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(CLR.CLRSigType.ElementType.I));
                            Clarity.Rpa.HighSsaRegister arrayReg = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister indexReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg2, indexSpec, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg3);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetArrayElementPtrInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                arrayReg,
                                new Clarity.Rpa.HighSsaRegister[] { indexReg }
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.StoreField_ManagedPtr:
                        {
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister valueReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg2, midInstr.TypeSpecArg2, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, valueReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.PtrFieldInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                objReg,
                                midInstr.StrArg));
                            outInstructions.Add(new Clarity.Rpa.Instructions.StorePtrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                valueReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.StoreField_Object:
                        {
                            Clarity.Rpa.HighSsaRegister objReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.TypeSpecArg, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister valueReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg2, midInstr.TypeSpecArg2, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, valueReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.RefFieldInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                objReg,
                                midInstr.StrArg,
                                RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg)
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.StorePtrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                valueReg
                                ));
                        }
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
                    case MidInstruction.OpcodeEnum.shr: // [.un]
                        {
                            bool isUnsigned = (midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Un) != 0;
                            bool isOvf = (midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Ovf) != 0;
                            NumericStackType nst = NumericStackTypeForNumericBinaryOp(midInstr.RegArg, midInstr.RegArg2);

                            Clarity.Rpa.Instructions.NumberArithType arithType;

                            switch(nst)
                            {
                                case NumericStackType.Float32:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.Float32;
                                    break;
                                case NumericStackType.Float64:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.Float64;
                                    break;
                                case NumericStackType.Int32:
                                    arithType = isUnsigned ? Clarity.Rpa.Instructions.NumberArithType.UInt32 : Clarity.Rpa.Instructions.NumberArithType.Int32;
                                    break;
                                case NumericStackType.Int64:
                                    arithType = isUnsigned ? Clarity.Rpa.Instructions.NumberArithType.UInt64 : Clarity.Rpa.Instructions.NumberArithType.Int64;
                                    break;
                                case NumericStackType.NativeInt:
                                    arithType = isUnsigned ? Clarity.Rpa.Instructions.NumberArithType.NativeUInt : Clarity.Rpa.Instructions.NumberArithType.NativeInt;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            CLR.CLRTypeSpec opSpec = TypeSpecForNumericStackType(nst, isUnsigned);

                            Clarity.Rpa.Instructions.NumberArithOp arithOp;
                            bool canThrow = isOvf;
                            switch (midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.add:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.Add;
                                    break;
                                case MidInstruction.OpcodeEnum.sub:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.Subtract;
                                    break;
                                case MidInstruction.OpcodeEnum.mul:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.Multiply;
                                    break;
                                case MidInstruction.OpcodeEnum.div:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.Divide;
                                    break;
                                case MidInstruction.OpcodeEnum.rem:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.Modulo;
                                    break;
                                case MidInstruction.OpcodeEnum.and:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.BitAnd;
                                    break;
                                case MidInstruction.OpcodeEnum.or:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.BitOr;
                                    break;
                                case MidInstruction.OpcodeEnum.xor:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.BitXor;
                                    break;
                                case MidInstruction.OpcodeEnum.shl:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.ShiftLeft;
                                    break;
                                case MidInstruction.OpcodeEnum.shr:
                                    arithOp = Clarity.Rpa.Instructions.NumberArithOp.ShiftRight;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            SsaRegister left = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, opSpec, outline, outInstructions);
                            SsaRegister right = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, opSpec, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.ArithInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg3),
                                arithOp,
                                arithType,
                                InternSsaRegister(left),
                                InternSsaRegister(right),
                                isOvf
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.neg:
                    case MidInstruction.OpcodeEnum.not:
                        {
                            Clarity.Rpa.HighSsaRegister srcReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.RegArg2.VType.TypeSpec, outline, outInstructions));
                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg2);

                            Clarity.Rpa.Instructions.NumberArithType arithType;
                            switch (midInstr.ArithArg)
                            {
                                case MidInstruction.ArithEnum.ArithType_Int32:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.Int32;
                                    break;
                                case MidInstruction.ArithEnum.ArithType_Int64:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.Int64;
                                    break;
                                case MidInstruction.ArithEnum.ArithType_NativeInt:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.NativeInt;
                                    break;
                                case MidInstruction.ArithEnum.ArithType_Float32:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.Float32;
                                    break;
                                case MidInstruction.ArithEnum.ArithType_Float64:
                                    arithType = Clarity.Rpa.Instructions.NumberArithType.Float64;
                                    break;
                                default:
                                    throw new Exception();
                            }

                            Clarity.Rpa.Instructions.NumberUnaryArithOp arithOp;
                            switch(midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.neg:
                                    arithOp = Clarity.Rpa.Instructions.NumberUnaryArithOp.Negate;
                                    break;
                                case MidInstruction.OpcodeEnum.not:
                                    arithOp = Clarity.Rpa.Instructions.NumberUnaryArithOp.BitNot;
                                    break;
                                default:
                                    throw new Exception();
                            }

                            outInstructions.Add(new Clarity.Rpa.Instructions.UnaryArithInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                arithOp,
                                arithType,
                                srcReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.TryConvertObj:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.DynamicCastInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg2),
                                InternSsaRegister(midInstr.RegArg),
                                RpaTagFactory.CreateTypeTag(midInstr.RegArg2.VType.TypeSpec)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Leave:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.LeaveRegionInstruction(
                                midInstr.CodeLocation,
                                midInstr.UIntArg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.DuplicateReg:
                        AliasSsaRegister(InternSsaRegister(midInstr.RegArg), midInstr.RegArg2);
                        break;
                    case MidInstruction.OpcodeEnum.StoreStaticField:
                        {
                            Clarity.Rpa.TypeSpecTag staticType = RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg);
                            string fieldName = midInstr.StrArg;

                            Clarity.Rpa.HighSsaRegister srcReg = InternSsaRegister(EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.TypeSpecArg2, outline, outInstructions));

                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, srcReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetStaticFieldAddrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                staticType,
                                fieldName
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.StorePtrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                srcReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadIndirect:
                        {
                            SsaRegister srcReg = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, midInstr.RegArg2.VType.TypeSpec, outline, outInstructions);
                            SsaRegister destReg = midInstr.RegArg2;

                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadPtrInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(destReg),
                                InternSsaRegister(srcReg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadStaticField:
                        {
                            Clarity.Rpa.TypeSpecTag staticType = RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg);
                            string fieldName = midInstr.StrArg;

                            Clarity.Rpa.HighSsaRegister destReg = InternSsaRegister(midInstr.RegArg);
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, destReg.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetStaticFieldAddrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                staticType,
                                fieldName
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.LoadPtrInstruction(
                                midInstr.CodeLocation,
                                destReg,
                                addrReg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadStaticFieldAddr:
                        {
                            Clarity.Rpa.TypeSpecTag staticType = RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg);
                            string fieldName = midInstr.StrArg;

                            Clarity.Rpa.HighSsaRegister addrReg = InternSsaRegister(midInstr.RegArg);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetStaticFieldAddrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                staticType,
                                fieldName
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Box:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.BoxInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg2),
                                InternSsaRegister(midInstr.RegArg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ConvertNumber:
                        {
                            bool isOvf = ((midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Ovf) != 0);
                            bool isUn = ((midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Un) != 0);

                            SsaRegister srcReg = midInstr.RegArg;
                            SsaRegister destReg = midInstr.RegArg2;

                            NumericStackType sourceNST = StackTypeForTypeSpec(midInstr.RegArg.VType.TypeSpec);
                            CLR.CLRTypeSpec srcSignAdjustedType = TypeSpecForNumericStackType(sourceNST, isUn);

                            srcReg = EmitPassiveConversion(midInstr.CodeLocation, srcReg, srcSignAdjustedType, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.NumberConvertInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(destReg),
                                InternSsaRegister(srcReg),
                                isOvf
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayLength:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.GetArrayLengthInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg2),
                                InternSsaRegister(midInstr.RegArg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadTypeInfoHandle:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.GetTypeInfoInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg),
                                RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ConvertObj:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.ForceDynamicCastInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg2),
                                InternSsaRegister(midInstr.RegArg),
                                RpaTagFactory.CreateTypeTag(midInstr.RegArg2.VType.TypeSpec)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.StoreArrayElem:
                        {
                            CLR.CLRTypeSpec szArraySpec = midInstr.RegArg.VType.TypeSpec;
                            CLR.CLRTypeSpec subscriptType = ((CLR.CLRTypeSpecSZArray)szArraySpec).SubType;

                            SsaRegister arrayReg = midInstr.RegArg;
                            SsaRegister indexReg = midInstr.RegArg2;
                            SsaRegister valueReg = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg3, subscriptType, outline, outInstructions);

                            Clarity.Rpa.HighSsaRegister arrayRegHigh = InternSsaRegister(arrayReg);
                            Clarity.Rpa.HighSsaRegister indexRegHigh = InternSsaRegister(indexReg);
                            Clarity.Rpa.HighSsaRegister valueRegHigh = InternSsaRegister(valueReg);
                            Clarity.Rpa.HighSsaRegister addrReg = new Clarity.Rpa.HighSsaRegister(Clarity.Rpa.HighValueType.ManagedPtr, valueRegHigh.Type, null);

                            outInstructions.Add(new Clarity.Rpa.Instructions.GetArrayElementPtrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                arrayRegHigh,
                                new Clarity.Rpa.HighSsaRegister[] { indexRegHigh }
                                ));
                            outInstructions.Add(new Clarity.Rpa.Instructions.StorePtrInstruction(
                                midInstr.CodeLocation,
                                addrReg,
                                valueRegHigh));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Switch:
                        {
                            CLR.CLRTypeSpec intType = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(CLR.CLRSigType.ElementType.U4));

                            SsaRegister caseReg = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg, intType, outline, outInstructions);

                            int numCases = midInstr.CfgEdgesArg.Length;
                            Clarity.Rpa.HighCfgNodeHandle[] outNodes = new Clarity.Rpa.HighCfgNodeHandle[numCases];
                            for (int i = 0; i < numCases; i++)
                            {
                                CfgOutboundEdge edge = midInstr.CfgEdgesArg[i];
                                CfgNode caseNode = edge.SuccessorNode;

                                CppTranslatedOutboundEdge outEdge = InternOutboundEdge(cfgNode, edge);

                                outNodes[i] = outEdge.NextNode;
                            }

                            fallthroughMerged = true;
                            CppTranslatedOutboundEdge defaultEdge = InternOutboundEdge(cfgNode, cfgNode.FallThroughEdge);

                            outInstructions.Add(new Clarity.Rpa.Instructions.SwitchInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(caseReg),
                                outNodes,
                                defaultEdge.NextNode
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.StoreIndirect:
                        {
                            SsaRegister destReg = midInstr.RegArg;
                            SsaRegister valueReg = EmitPassiveConversion(midInstr.CodeLocation, midInstr.RegArg2, midInstr.RegArg.VType.TypeSpec, outline, outInstructions);

                            outInstructions.Add(new Clarity.Rpa.Instructions.StorePtrInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(destReg),
                                InternSsaRegister(valueReg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldInfoHandle:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.GetFieldInfoInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg),
                                RpaTagFactory.CreateTypeTag(midInstr.TypeSpecArg),
                                midInstr.StrArg,
                                midInstr.FlagArg
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.UnboxPtr:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.UnboxPtrInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg2),
                                InternSsaRegister(midInstr.RegArg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.UnboxValue:
                        {
                            outInstructions.Add(new Clarity.Rpa.Instructions.UnboxValueInstruction(
                                midInstr.CodeLocation,
                                InternSsaRegister(midInstr.RegArg2),
                                InternSsaRegister(midInstr.RegArg)
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ZeroFillPtr:
                        outInstructions.Add(new Clarity.Rpa.Instructions.ZeroFillPtrInstruction(
                            midInstr.CodeLocation,
                            InternSsaRegister(midInstr.RegArg)
                            ));
                        break;
                    case MidInstruction.OpcodeEnum.EnterProtectedBlock:
                        {
                            ExceptionHandlingCluster cluster = midInstr.EhClusterArg;

                            Clarity.Rpa.HighRegion tryRegion;
                            {
                                CppRegionEmitter tryEmitter = new CppRegionEmitter(m_builder, cluster.TryRegion, m_regAllocator, m_localLookup);
                                tryRegion = tryEmitter.Emit();
                            }

                            List<HighCatchHandler> catchHandlers = new List<HighCatchHandler>();
                            List<HighRegion> otherRegions = new List<HighRegion>();
                            foreach (ExceptionHandlingRegion handlerRegion in cluster.ExceptionHandlingRegions)
                            {
                                CppRegionEmitter hdlEmitter = new CppRegionEmitter(m_builder, handlerRegion, m_regAllocator, m_localLookup);
                                HighRegion hdlRegion = hdlEmitter.Emit();

                                switch (cluster.ClusterType)
                                {
                                    case ExceptionHandlingCluster.ClusterTypeEnum.TryCatch:
                                        {
                                            HighCfgNode entryNode = hdlRegion.EntryNode.Value;

                                            // Inject a catch landing instruction and recreate the phi node.
                                            // This is safe because III.1.7.5 disallows backward branches with stack,
                                            // so the only valid way to reach a catch handler is by catching, never via
                                            // a predecessor.
                                            if (entryNode.Phis.Length != 1 || entryNode.Phis[0].Links.Length != 0)
                                                throw new Exception("Catch handler should start with an unlinked phi node");

                                            HighSsaRegister exceptionReg = entryNode.Phis[0].Dest;

                                            CodeLocationTag codeLoc = entryNode.Instructions[0].CodeLocation;

                                            List<HighInstruction> catchLandingInstrs = new List<HighInstruction>();
                                            HighSsaRegister catchDest = new HighSsaRegister(HighValueType.ReferenceValue, exceptionReg.Type, null);
                                            catchLandingInstrs.Add(new Clarity.Rpa.Instructions.CatchInstruction(codeLoc, catchDest, exceptionReg.Type));
                                            catchLandingInstrs.Add(new Clarity.Rpa.Instructions.BranchInstruction(codeLoc, hdlRegion.EntryNode));

                                            HighCfgNode landingNode = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], catchLandingInstrs.ToArray());

                                            HighCfgNodeHandle landingHandle = new HighCfgNodeHandle(landingNode);

                                            entryNode.Predecessors = new HighCfgNodeHandle[] { landingHandle };
                                            entryNode.Phis[0].Links = new HighPhiLink[] { new HighPhiLink(landingHandle, catchDest) };

                                            HighRegion replacementRegion = new HighRegion(landingHandle);

                                            catchHandlers.Add(new HighCatchHandler(
                                                RpaTagFactory.CreateTypeTag(handlerRegion.ExceptionType),
                                                replacementRegion
                                                ));
                                        }
                                        break;
                                    case ExceptionHandlingCluster.ClusterTypeEnum.TryFault:
                                    case ExceptionHandlingCluster.ClusterTypeEnum.TryFinally:
                                        otherRegions.Add(hdlRegion);
                                        break;
                                    default:
                                        throw new Exception();
                                }
                            }

                            List<Clarity.Rpa.HighEscapePathTerminator> terminators = new List<Clarity.Rpa.HighEscapePathTerminator>();
                            foreach (uint escapePath in cluster.EscapePaths)
                            {
                                CfgNode targetNode;
                                if (m_region.EscapeTerminators.TryGetValue(escapePath, out targetNode))
                                {
                                    Clarity.Rpa.HighCfgNodeHandle terminatorNode = InternHighCfgNode(targetNode);
                                    terminators.Add(new Clarity.Rpa.HighEscapePathTerminator(escapePath, terminatorNode));
                                }
                            }

                            Clarity.Rpa.HighProtectedRegion protRegion;
                            switch (cluster.ClusterType)
                            {
                                case ExceptionHandlingCluster.ClusterTypeEnum.TryCatch:
                                    protRegion = new Clarity.Rpa.HighTryCatchRegion(tryRegion, catchHandlers.ToArray());
                                    break;
                                case ExceptionHandlingCluster.ClusterTypeEnum.TryFault:
                                    protRegion = new Clarity.Rpa.HighTryFaultRegion(tryRegion, otherRegions[0]);
                                    break;
                                case ExceptionHandlingCluster.ClusterTypeEnum.TryFinally:
                                    protRegion = new Clarity.Rpa.HighTryFinallyRegion(tryRegion, otherRegions[0]);
                                    break;
                                default:
                                    throw new Exception();
                            }

                            Clarity.Rpa.HighEHCluster highCluster = new Clarity.Rpa.HighEHCluster(protRegion, terminators.ToArray());

                            outInstructions.Add(new Clarity.Rpa.Instructions.EnterProtectedBlockInstruction(
                                midInstr.CodeLocation,
                                highCluster
                                ));
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ExitFinally:
                        outInstructions.Add(new Clarity.Rpa.Instructions.ReturnInstruction(midInstr.CodeLocation));
                        break;
                    case MidInstruction.OpcodeEnum.BindDelegate:
                        {
                            SsaRegister objReg = midInstr.RegArg;
                            CppMethodSpec methodSpec = (CppMethodSpec)midInstr.RegArg2.ConstantValue;
                            SsaRegister destReg = midInstr.RegArg3;

                            Clarity.Rpa.MethodSignatureTag sigTag = RpaTagFactory.CreateMethodSignature(methodSpec.CppMethod.MethodSignature);

                            Clarity.Rpa.HighSsaRegister destRegHigh = InternSsaRegister(destReg);

                            Clarity.Rpa.MethodSlotType slotType;

                            if (methodSpec.CppMethod.Static)
                                slotType = Clarity.Rpa.MethodSlotType.Static;
                            else
                            {
                                objReg = EmitPassiveConversion(midInstr.CodeLocation, objReg, methodSpec.CppMethod.DeclaredInClassSpec, outline, outInstructions);
                                if (midInstr.RegArg2.VType.ValType == VType.ValTypeEnum.DelegateSimpleMethod)
                                    slotType = Clarity.Rpa.MethodSlotType.Instance;
                                else if (midInstr.RegArg2.VType.ValType == VType.ValTypeEnum.DelegateVirtualMethod)
                                    slotType = Clarity.Rpa.MethodSlotType.Virtual;
                                else
                                    throw new ArgumentException();
                            }

                            Clarity.Rpa.MethodSpecTag methodSpecTag = RpaTagFactory.CreateMethodSpec(slotType, methodSpec);

                            if (methodSpec.CppMethod.Static)
                            {
                                outInstructions.Add(new Clarity.Rpa.Instructions.BindStaticDelegateInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(destReg),
                                    methodSpecTag
                                    ));
                            }
                            else if (midInstr.RegArg2.VType.ValType == VType.ValTypeEnum.DelegateSimpleMethod)
                            {
                                outInstructions.Add(new Clarity.Rpa.Instructions.BindInstanceDelegateInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(destReg),
                                    InternSsaRegister(objReg),
                                    methodSpecTag
                                    ));
                            }
                            else if (midInstr.RegArg2.VType.ValType == VType.ValTypeEnum.DelegateVirtualMethod)
                            {
                                outInstructions.Add(new Clarity.Rpa.Instructions.BindVirtualDelegateInstruction(
                                    midInstr.CodeLocation,
                                    InternSsaRegister(destReg),
                                    InternSsaRegister(objReg),
                                    methodSpecTag
                                    ));
                            }
                        }
                        break;
                    default:
                        throw new ArgumentException("Invalid mid IL opcode");
                }
            }

            if (cfgNode.FallThroughEdge != null && !fallthroughMerged)
            {
                CppTranslatedOutboundEdge outEdge = InternOutboundEdge(cfgNode, cfgNode.FallThroughEdge);

                outInstructions.Add(new Clarity.Rpa.Instructions.BranchInstruction(cfgNode.FallThroughEdge.CodeLocation, outEdge.NextNode));
            }

            List<HighCfgNodeHandle> highPreds = new List<HighCfgNodeHandle>();
            foreach (CppTranslatedOutboundEdge predEdge in predecessorEdges)
                highPreds.Add(predEdge.PrevNode);

            highNode.Value = new HighCfgNode(highPreds.ToArray(), outPhis.ToArray(), outInstructions.ToArray());
        }

        private SsaRegister EmitPassiveConversion_PermitRefs(Clarity.Rpa.CodeLocationTag codeLocation, SsaRegister sourceReg, CLRTypeSpec destType, CppCfgNodeOutline outline, IList<Clarity.Rpa.HighInstruction> instrs)
        {
            switch (sourceReg.VType.ValType)
            {
                case VType.ValTypeEnum.ManagedPtr:
                    if (sourceReg.VType.TypeSpec.Equals(destType))
                        return sourceReg;
                    throw new ArgumentException();
                default:
                    return EmitPassiveConversion(codeLocation, sourceReg, destType, outline, instrs);
            }
        }

        private SsaRegister EmitPassiveConversion(Clarity.Rpa.CodeLocationTag codeLocation, SsaRegister sourceReg, CLRTypeSpec destType, CppCfgNodeOutline outline, IList<Clarity.Rpa.HighInstruction> instrs)
        {
            if (sourceReg.VType.ValType == VType.ValTypeEnum.DelegateSimpleMethod ||
                sourceReg.VType.ValType == VType.ValTypeEnum.DelegateVirtualMethod)
                return sourceReg;

            if (sourceReg.VType.TypeSpec.Equals(destType))
                return sourceReg;

            if (sourceReg.VType.ValType == VType.ValTypeEnum.Null)
            {
                SsaRegister nullReg = new SsaRegister(new VType(VType.ValTypeEnum.Null, destType));
                nullReg.MakeUsable();
                nullReg.GenerateUniqueID(m_regAllocator);

                outline.AddRegister(nullReg);
                return nullReg;
            }

            SsaRegister newReg;
            switch (sourceReg.VType.ValType)
            {
                case VType.ValTypeEnum.ConstantReference:
                case VType.ValTypeEnum.ReferenceValue:
                    newReg = new SsaRegister(new VType(VType.ValTypeEnum.ReferenceValue, destType));
                    break;
                case VType.ValTypeEnum.ValueValue:
                case VType.ValTypeEnum.ConstantValue:
                    newReg = new SsaRegister(new VType(VType.ValTypeEnum.ValueValue, destType));
                    break;
                default:
                    throw new ArgumentException();
            }

            newReg.MakeUsable();
            newReg.GenerateUniqueID(m_regAllocator);

            instrs.Add(new Clarity.Rpa.Instructions.PassiveConvertInstruction(
                codeLocation,
                InternSsaRegister(newReg),
                InternSsaRegister(sourceReg)
                ));

            return newReg;
        }
    }
}
