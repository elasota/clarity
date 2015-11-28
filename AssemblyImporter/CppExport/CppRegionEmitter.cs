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
        private Stack<CfgNode> m_unemittedNodesStack;   // May contain duplicates, check set to dedupe
        private HashSet<CfgNode> m_emittedNodesSet;
        private int m_baseIndentLevel;
        private string m_frameVarName;
        private CppDependencySet m_depSet;

        public CppRegionEmitter(CppDependencySet depSet, int baseIndentLevel, CppBuilder builder, ExceptionHandlingRegion region, CppRegisterAllocator regAllocator, string frameVarName)
        {
            m_region = region;
            m_builder = builder;
            m_regAllocator = regAllocator;
            m_emittedNodesSet = new HashSet<CfgNode>();
            m_unemittedNodesStack = new Stack<CfgNode>();
            m_baseIndentLevel = baseIndentLevel;
            m_frameVarName = frameVarName;
            m_depSet = depSet;

            AddNode(region.RootCfgNode);
        }

        private bool AddNode(CfgNode cfgNode)
        {
            if (m_emittedNodesSet.Contains(cfgNode))
                return false;
            m_unemittedNodesStack.Push(cfgNode);
            return true;
        }

        private void SpillCfgEdge(string indent, CfgNode node, CfgOutboundEdge edge, StreamWriter writer)
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
                    string test = PassiveConvertValue(storeBase.VType, vReg.VType.TypeSpec, StorageLocForSsaReg(storeBase, false, false));

                    writer.Write(indent);
                    writer.WriteLine(
                        StorageLocForVReg(vReg, false, false)
                        + " = "
                        + PassiveConvertValue(storeBase.VType, vReg.VType.TypeSpec, StorageLocForSsaReg(storeBase, false, false))
                        + ";  // cfg edge spillover");
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
            //writer.WriteLine("Escape terminators:");
            //foreach (KeyValuePair<uint, CfgNode> terminator in m_region.EscapeTerminators)
            //    writer.WriteLine("    " + terminator.Key + " --> " + m_regAllocator.TargetIDForCfgNode(terminator.Value));
            
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

                EmitCfgNode(m_baseIndentLevel, cfgNode, writer, enterIsContinuous, exitIsContinuous);
                lastCfgNode = cfgNode;

                if (!exitIsContinuous)
                {
                    foreach (VReg vReg in m_regAllocator.AllRegisters)
                        if (vReg.IsAlive)
                            throw new Exception("VReg leaked");
                }
            }
        }

        private static string StorageLocForVReg(VReg vReg, bool makeLive, bool zombify)
        {
            bool isTraced = (vReg.Traceability != CppTraceabilityEnum.NotTraced);
            string prefix;
            if (isTraced)
            {
                if (makeLive)
                    prefix = "::CLRVM::LivenVReg(";
                else if (zombify)
                    prefix = "::CLRVM::KillAndReturnVReg(";
                else
                    prefix = "::CLRVM::VRegValue(";
                return prefix + vReg.SlotName + ")";
            }
            else
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

        private string StorageLocForSsaReg(SsaRegister ssaReg, bool forWrite, bool zombify)
        {
            if (forWrite && zombify)
                throw new ArgumentException();
            if (ssaReg.IsSpilled)
            {
                VReg vReg = ssaReg.SpillVReg;
                if (zombify)
                    vReg.Zombify();
                return StorageLocForVReg(vReg, forWrite, zombify);
            }
            else
            {
                switch (ssaReg.VType.ValType)
                {
                    case VType.ValTypeEnum.ValueValue:
                    case VType.ValTypeEnum.NotNullReferenceValue:
                    case VType.ValTypeEnum.NullableReferenceValue:
                    case VType.ValTypeEnum.AnchoredManagedPtr:
                    case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                    case VType.ValTypeEnum.LocalManagedPtr:
                        return "ssa" + ssaReg.SsaID;
                    case VType.ValTypeEnum.Null:
                    case VType.ValTypeEnum.ConstantValue:
                    case VType.ValTypeEnum.ConstantReference:
                        return PassiveConvertValue(ssaReg.VType, ssaReg.VType.TypeSpec, "");
                    default:
                        throw new Exception("Couldn't resolve storage location for SSA register");
                }
            }
        }

        private string PassiveConvertManagedPtr(VType sourceVType, VType targetVType, string valStr)
        {
            if (!sourceVType.TypeSpec.Equals(targetVType.TypeSpec))
                throw new Exception("PassiveConvertManagedPtr with incompatible type specs");

            if (sourceVType.ValType == targetVType.ValType)
                return valStr;

            string result = "::CLRUtil::PassiveConvert";
            switch (sourceVType.ValType)
            {
                case VType.ValTypeEnum.AnchoredManagedPtr:
                    result += "Anchored";
                    break;
                case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                    result += "MaybeAnchored";
                    break;
                case VType.ValTypeEnum.LocalManagedPtr:
                    result += "Local";
                    break;
                default:
                    throw new Exception("Strange passive conversion requested");
            }
            result += "To";
            switch (targetVType.ValType)
            {
                case VType.ValTypeEnum.AnchoredManagedPtr:
                    result += "Anchored";
                    break;
                case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                    result += "MaybeAnchored";
                    break;
                case VType.ValTypeEnum.LocalManagedPtr:
                    result += "Local";
                    break;
                default:
                    throw new Exception("Strange passive conversion requested");
            }

            result += "ManagedPtr< ";
            result += m_builder.SpecToAmbiguousStorage(sourceVType.TypeSpec);
            result += " >(";
            result += valStr;
            result += ")";
            return result;
        }

        private static void GenerateCppStringConstant(string inStr, out string outEncodedStr, out int outHash, out bool outIsPacked)
        {
            // Clarity string constant encoding isn't UTF-8, but rather, a packed encoding that converts
            // into UTF-16 code points directly.
            // "?" is escaped to avoid trigraph conversion
            List<char> resultChars = new List<char>();
            string constStrs = " !#$%&()*+,-./0123456789:l<=>@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz~";
            bool isPacked = false;

            foreach (char inChar in inStr)
            {
                if (constStrs.IndexOf(inChar) >= 0)
                    resultChars.Add(inChar);
                else
                {
                    int codePoint = ((int)inChar) & 0xffff;
                    bool first = true;

                    while (codePoint != 0 || first)
                    {
                        first = false;

                        int codedFragment = codePoint & 0x7f;
                        codePoint >>= 7;
                        resultChars.Add('\\');

                        if (codePoint != 0)
                        {
                            codedFragment |= 0x80;
                            isPacked = true;
                        }

                        for (int octalDigitIndex = 0; octalDigitIndex < 3; octalDigitIndex++)
                        {
                            int octalDigit = ((codedFragment >> ((2 - octalDigitIndex) * 3)) & 0x7);
                            resultChars.Add((char)('0' + octalDigit));
                        }
                    }
                }
            }

            string encodedStr = new string(resultChars.ToArray());
            byte[] asciiBytes = System.Text.Encoding.ASCII.GetBytes(encodedStr);
            byte[] longHash = System.Security.Cryptography.SHA256Managed.Create().ComputeHash(asciiBytes);

            int hash = 0;
            for (int i = 0; i < 4; i++)
                hash = ((hash << 8) | longHash[i]);

            outEncodedStr = encodedStr;
            outHash = hash;
            outIsPacked = isPacked;
        }

        private string PassiveConvertValueToNumeric(VType vType, string valStr)
        {
            CppClass cls = m_builder.GetCachedClass(vType.TypeSpec);
            if (cls.IsEnum)
                return PassiveConvertValue(vType, cls.GetEnumUnderlyingType(), valStr);
            return valStr;
        }

        private string PassiveConvertValue(VType sourceVType, CLR.CLRTypeSpec targetTypeSpec, string valStr)
        {
            switch (sourceVType.ValType)
            {
                case VType.ValTypeEnum.Null:
                    return "::CLRUtil::NullReference< " + m_builder.SpecToAmbiguousStorage(targetTypeSpec) + " >()";
                case VType.ValTypeEnum.ConstantReference:
                    {
                        CLR.CLRTypeSpecClass targetClassSpec = ((CLR.CLRTypeSpecClass)targetTypeSpec);
                        string targetName = targetClassSpec.TypeDef.TypeName;
                        string sourceName = ((CLR.CLRTypeSpecClass)sourceVType.TypeSpec).TypeDef.TypeName;
                        if (sourceName == "String")
                        {
                            string strConstant = (string)sourceVType.ConstantValue;
                            string encodedStr;
                            int hash;
                            bool isPacked;
                            GenerateCppStringConstant(strConstant, out encodedStr, out hash, out isPacked);

                            return "::CLRVM::StringConstant< "
                                + m_builder.SpecToAmbiguousStorage(targetClassSpec)
                                + " >(" + m_frameVarName
                                + ", " + (isPacked ? "true" : "false")
                                + ", " + strConstant.Length.ToString()
                                + ", " + hash.ToString()
                                + ", \"" + encodedStr + "\")";
                        }
                        else
                            throw new Exception("Unexpected constant reference type");
                    }
                case VType.ValTypeEnum.ConstantValue:
                    {
                        string instanceMacro;
                        CLR.CLRTypeSpecClass targetClassSpec = ((CLR.CLRTypeSpecClass)targetTypeSpec);
                        string targetName = targetClassSpec.TypeDef.TypeName;

                        if (targetName == "Int16")
                            instanceMacro = "CLARITY_INT16CONSTANT";
                        else if (targetName == "UInt16")
                            instanceMacro = "CLARITY_UINT16CONSTANT";
                        else if (targetName == "Int32")
                            instanceMacro = "CLARITY_INT32CONSTANT";
                        else if (targetName == "UInt32")
                            instanceMacro = "CLARITY_UINT32CONSTANT";
                        else if (targetName == "Int64")
                            instanceMacro = "CLARITY_UINT64CONSTANT";
                        else if (targetName == "UInt64")
                            instanceMacro = "CLARITY_UINT64CONSTANT";
                        else if (targetName == "SByte")
                            instanceMacro = "CLARITY_INT8CONSTANT";
                        else if (targetName == "Byte")
                            instanceMacro = "CLARITY_UINT8CONSTANT";
                        else if (targetName == "Boolean")
                            instanceMacro = "CLARITY_BOOLCONSTANT";
                        else if (targetName == "Single")
                            instanceMacro = "CLARITY_FLOAT32CONSTANT";
                        else if (targetName == "Double")
                            instanceMacro = "CLARITY_FLOAT64CONSTANT";
                        else if (targetName == "Char")
                            instanceMacro = "CLARITY_CHARCONSTANT";
                        else if (targetName == "IntPtr")
                            instanceMacro = "CLARITY_INTPTRCONSTANT";
                        else
                        {
                            CppClass targetClass = m_builder.GetCachedClass(targetClassSpec);
                            if (targetClass.IsEnum)
                            {
                                instanceMacro = "::CLRVM::ConstantEnum< ";
                                instanceMacro += m_builder.SpecToAmbiguousStorage(targetClassSpec);
                                instanceMacro += " >";
                            }
                            else
                                throw new Exception("Unexpected constant value type");
                        }

                        return instanceMacro + "(" + sourceVType.ConstantValue.ToString() + ")";
                    }
                default:
                    break;
            }

            if (valStr == "")
                throw new Exception("Missing value in passive conversion");

            if (targetTypeSpec.Equals(sourceVType.TypeSpec))
                return valStr;

            string result = "";
            switch (sourceVType.ValType)
            {
                case VType.ValTypeEnum.ValueValue:
                    result += "::CLRUtil::PassiveValueConverter< ";
                    break;
                case VType.ValTypeEnum.NotNullReferenceValue:
                case VType.ValTypeEnum.NullableReferenceValue:
                    result += "::CLRUtil::PassiveReferenceConverter< ";
                    break;
                default:
                    throw new Exception("Strange passive conversion requested");
            }

            result += m_builder.SpecToAmbiguousStorage(sourceVType.TypeSpec);
            result += ", ";
            result += m_builder.SpecToAmbiguousStorage(targetTypeSpec);
            result += " >::Convert(";
            result += valStr;
            result += ")";
            return result;
        }

        private void AddVTypeDependency(VType vType)
        {
            m_depSet.AddTypeSpecDependencies(vType.TypeSpec, true);
        }

        private void EmitCfgNode(int indentLevel, CfgNode cfgNode, StreamWriter writer, bool enterIsContinuous, bool exitIsContinuous)
        {
            List<SsaRegister> leakedRegs = new List<SsaRegister>();
            Console.WriteLine("Starting CFG edge");
            CppScopeStack scopeStack = new CppScopeStack(indentLevel);

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

            if (!enterIsContinuous)
            {
                writer.Write(scopeStack.Indent);
                writer.Write("bLabel_");
                writer.Write(m_regAllocator.TargetIDForCfgNode(cfgNode));
                writer.WriteLine(":;");
            }

            int debugInstrNum = -1;
            MidInstruction[] midInstrs = cfgNode.MidInstructions;
            bool regionIsTerminated = false;
            foreach (MidInstruction midInstr in midInstrs)
            {
                debugInstrNum++;
                switch (midInstr.Opcode)
                {
                    case MidInstruction.OpcodeEnum.AllocObject:
                        AddVTypeDependency(midInstr.RegArg.VType);
                        m_depSet.AddTypeSpecDependencies(midInstr.TypeSpecArg, true);
                        writer.Write(scopeStack.Indent);
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                        writer.Write(" = ::CLRVM::AllocObject< ");
                        writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.TypeSpecArg));
                        writer.Write(" >(");
                        writer.Write(m_frameVarName);
                        writer.WriteLine(");");
                        m_depSet.AddTypeSpecDependencies(midInstr.TypeSpecArg, true);
                        break;
                    case MidInstruction.OpcodeEnum.CallMethod:
                    case MidInstruction.OpcodeEnum.CallConstructor:
                        {
                            bool shouldZombifyThis = (midInstr.Opcode == MidInstruction.OpcodeEnum.CallMethod);
                            CppMethodSpec methodSpec = midInstr.MethodSpecArg;
                            CppMethod cppMethod = methodSpec.CppMethod;
                            CppClass thisClass = m_builder.GetCachedClass(cppMethod.DeclaredInClassSpec);

                            m_depSet.AddTypeSpecDependencies(cppMethod.DeclaredInClassSpec, true);

                            SsaRegister returnReg = midInstr.RegArg;
                            SsaRegister thisReg = midInstr.RegArg2;
                            SsaRegister[] paramsRegs = midInstr.RegArgs;

                            writer.Write(scopeStack.Indent);
                            if (returnReg != null)
                            {
                                writer.Write(StorageLocForSsaReg(returnReg, true, false));
                                writer.Write(" = ");
                            }

                            if (cppMethod.MethodDef.Static || thisClass.IsValueType)
                            {
                                writer.Write(m_builder.SpecToAmbiguousStorage(cppMethod.DeclaredInClassSpec));
                                writer.Write("::");
                            }
                            else
                            {
                                if (cppMethod.DeclaredInClassSpec.Equals(thisReg.VType.TypeSpec))
                                {
                                    writer.Write(StorageLocForSsaReg(thisReg, false, shouldZombifyThis));
                                    writer.Write("->");
                                }
                                else
                                {
                                    if (thisReg.VType.ValType != VType.ValTypeEnum.ConstantReference &&
                                        thisReg.VType.ValType != VType.ValTypeEnum.NotNullReferenceValue &&
                                        thisReg.VType.ValType != VType.ValTypeEnum.NullableReferenceValue)
                                        throw new Exception("Internal error: Translated call site of a non-reference");

                                    CLR.CLRTypeSpec destTypeSpec = cppMethod.DeclaredInClassSpec;
                                    CLR.CLRTypeSpec sourceTypeSpec = thisReg.VType.TypeSpec;
                                    writer.Write("(::CLRUtil::PassiveReferenceConverter< ");
                                    writer.Write(m_builder.SpecToAmbiguousStorage(sourceTypeSpec));
                                    writer.Write(", ");
                                    writer.Write(m_builder.SpecToAmbiguousStorage(destTypeSpec));
                                    writer.Write(" >::Convert(");
                                    writer.Write(StorageLocForSsaReg(thisReg, false, shouldZombifyThis));
                                    writer.Write("))->");
                                }
                            }

                            writer.Write(cppMethod.GenerateCallName());

                            if (methodSpec.GenericParameters != null)
                            {
                                writer.Write("< ");
                                for (int i = 0; i < methodSpec.GenericParameters.Length; i++)
                                {
                                    if (i != 0)
                                        writer.Write(", ");
                                    writer.Write(m_builder.SpecToAmbiguousStorage(methodSpec.GenericParameters[i]));
                                }
                                writer.Write(" >");
                            }

                            writer.Write("(");
                            writer.Write(m_frameVarName);

                            if (!cppMethod.MethodDef.Static && thisClass.IsValueType)
                            {
                                writer.Write(", ::CLRVM::PassValueThis(");
                                writer.Write(StorageLocForSsaReg(thisReg, false, shouldZombifyThis));
                                writer.Write(")");
                            }

                            CLR.CLRMethodSignatureInstance methodSig = cppMethod.MethodSignature;

                            int numParams = paramsRegs.Length;
                            if (numParams != methodSig.ParamTypes.Length)
                                throw new Exception("Internal error: CallMethod param count mismatch");

                            for (int i = 0; i < numParams; i++)
                            {
                                SsaRegister paramReg = paramsRegs[i];
                                CLR.CLRMethodSignatureInstanceParam paramSig = methodSig.ParamTypes[i];

                                writer.Write(", ");

                                if (paramSig.TypeOfType == CLR.CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                                {
                                    if (thisClass.IsDelegate && i == 1 && midInstr.Opcode == MidInstruction.OpcodeEnum.CallConstructor)
                                    {
                                        writer.Write("FIXME_DELEGATE_CONSTANT");
                                    }
                                    else
                                    {
                                        string paramValue = StorageLocForSsaReg(paramReg, false, true);
                                        writer.Write(PassiveConvertValue(paramReg.VType, paramSig.Type, paramValue));
                                    }
                                }
                                else
                                {
                                    string paramValue = StorageLocForSsaReg(paramReg, false, true);
                                    writer.Write(paramValue);
                                }
                            }
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.CallVirtualMethod:
                        {
                            CppMethodSpec methodSpec = midInstr.MethodSpecArg;
                            CppMethod cppMethod = methodSpec.CppMethod;
                            CppClass thisClass = m_builder.GetCachedClass(cppMethod.DeclaredInClassSpec);

                            m_depSet.AddTypeSpecDependencies(cppMethod.DeclaredInClassSpec, true);

                            SsaRegister returnReg = midInstr.RegArg;
                            SsaRegister thisReg = midInstr.RegArg2;
                            SsaRegister[] paramsRegs = midInstr.RegArgs;

                            writer.Write(scopeStack.Indent);
                            if (returnReg != null)
                            {
                                writer.Write(StorageLocForSsaReg(returnReg, true, false));
                                writer.Write(" = ");
                            }


                            writer.Write(StorageLocForSsaReg(thisReg, false, true));
                            writer.Write("->");

                            CppVtableSlot vtableSlot = cppMethod.CreatesSlot;
                            if (vtableSlot == null)
                                vtableSlot = cppMethod.ReplacesStandardSlot;
                            if (vtableSlot == null)
                                throw new Exception("Internal error: Couldn't resolve vtable slot for method");

                            writer.Write(vtableSlot.GenerateName());

                            if (methodSpec.GenericParameters != null)
                                throw new NotSupportedException("Virtual generics are not supported");

                            writer.Write("(");
                            writer.Write(m_frameVarName);

                            CLR.CLRMethodSignatureInstance methodSig = cppMethod.MethodSignature;

                            int numParams = paramsRegs.Length;
                            if (numParams != methodSig.ParamTypes.Length)
                                throw new Exception("Internal error: CallVirtualMethod param count mismatch");

                            for (int i = 0; i < numParams; i++)
                            {
                                SsaRegister paramReg = paramsRegs[i];
                                CLR.CLRMethodSignatureInstanceParam paramSig = methodSig.ParamTypes[i];

                                writer.Write(", ");

                                string paramValue = StorageLocForSsaReg(paramReg, false, true);
                                if (paramSig.TypeOfType == CLR.CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                                    writer.Write(PassiveConvertValue(paramReg.VType, paramSig.Type, paramValue));
                                else
                                    writer.Write(paramValue);
                            }

                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ConstrainedCallVirtualMethod:
                        writer.WriteLine("ConstrainedCallVirtualMethod " + midInstr.MethodSpecArg);
                        if (midInstr.TypeSpecArg != null)
                            writer.WriteLine("    Return SSA: " + midInstr.TypeSpecArg);
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
                    case MidInstruction.OpcodeEnum.ConstrainedCallMethod:
                        writer.WriteLine("ConstrainedCallMethod " + midInstr.MethodSpecArg);
                        if (midInstr.TypeSpecArg != null)
                            writer.WriteLine("    Return SSA: " + midInstr.TypeSpecArg);
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
                            VReg vReg = midInstr.RegArg.SpillVReg;

                            if (vReg.Traceability != CppTraceabilityEnum.NotTraced && !vReg.IsZombie)
                            {
                                writer.Write(scopeStack.Indent);
                                writer.Write("::CLRVM::KillVReg(");
                                writer.Write(vReg.SlotName);
                                writer.WriteLine(");");
                            }
                            midInstr.RegArg.SpillVReg.Kill();
                        }
                        else
                            scopeStack.KillReg(midInstr.RegArg, writer);
                        break;
                    case MidInstruction.OpcodeEnum.LivenReg:
                        {
                            SsaRegister reg = midInstr.RegArg;
                            if (reg.VType.TypeSpec != null)
                                m_depSet.AddTypeSpecDependencies(reg.VType.TypeSpec, true);
                            reg.SsaID = m_regAllocator.NewSsaID();

                            if (reg.IsSpilled)
                                reg.SpillVReg = m_regAllocator.AllocReg(reg.VType);
                            else
                                scopeStack.LivenReg(reg, m_builder, writer);
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Return:
                        writer.Write(scopeStack.Indent);
                        writer.WriteLine("return;");
                        regionIsTerminated = true;
                        break;
                    case MidInstruction.OpcodeEnum.ReturnValue:
                        writer.Write(scopeStack.Indent);
                        writer.Write("return ");
                        writer.Write(PassiveConvertValue(midInstr.RegArg.VType, midInstr.TypeSpecArg, StorageLocForSsaReg(midInstr.RegArg, false, true)));
                        writer.WriteLine(";");
                        regionIsTerminated = true;
                        break;
                    case MidInstruction.OpcodeEnum.LoadReg_ManagedPtr:
                        writer.Write(scopeStack.Indent);
                        writer.WriteLine(StorageLocForSsaReg(midInstr.RegArg, true, false) + " = " + StorageLocForVReg(midInstr.VRegArg, false, false) + ";");
                        break;
                    case MidInstruction.OpcodeEnum.LoadReg_Value:
                        writer.Write(scopeStack.Indent);
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                        writer.Write(" = ");
                        writer.Write(PassiveConvertValue(midInstr.RegArg.VType, midInstr.VRegArg.VType.TypeSpec, StorageLocForVReg(midInstr.VRegArg, false, false)));
                        writer.WriteLine(";");
                        break;
                    case MidInstruction.OpcodeEnum.StoreReg_ManagedPtr:
                        writer.Write(scopeStack.Indent);
                        writer.Write(StorageLocForVReg(midInstr.VRegArg, false, false));
                        writer.Write(" = ");
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                        writer.WriteLine(";");
                        break;
                    case MidInstruction.OpcodeEnum.StoreReg_Value:
                        writer.Write(scopeStack.Indent);
                        writer.Write(StorageLocForVReg(midInstr.VRegArg, false, false));
                        writer.Write(" = ");
                        writer.Write(PassiveConvertValue(midInstr.RegArg.VType, midInstr.VRegArg.VType.TypeSpec, StorageLocForSsaReg(midInstr.RegArg, false, false)));
                        writer.WriteLine(";");
                        break;
                    case MidInstruction.OpcodeEnum.beq_ref:
                        // UNIMPLEMENTED
                        writer.WriteLine("beq_ref Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.beq_val:
                        // UNIMPLEMENTED
                        writer.WriteLine("beq_val Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bne_ref:
                        // UNIMPLEMENTED
                        writer.WriteLine("bne_ref Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bne_val:
                        // UNIMPLEMENTED
                        writer.WriteLine("bne_val Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bge:
                        // UNIMPLEMENTED
                        writer.WriteLine("bge Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.bgt:
                        // UNIMPLEMENTED
                        writer.WriteLine("bgt Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.ble:
                        // UNIMPLEMENTED
                        writer.WriteLine("ble Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.blt:
                        // UNIMPLEMENTED
                        writer.WriteLine("blt Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Target CFG " + m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.clt:
                    case MidInstruction.OpcodeEnum.cgt:
                    case MidInstruction.OpcodeEnum.ceq_numeric:
                        {
                            bool isUnsigned = midInstr.FlagArg;
                            CLR.CLRTypeSpec comparisonSpec = this.TypeSpecForNumericBinaryOp(midInstr.RegArg2, midInstr.RegArg3, isUnsigned);

                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                            writer.Write(" = ((");

                            writer.Write(PassiveConvertValue(midInstr.RegArg2.VType, comparisonSpec, StorageLocForSsaReg(midInstr.RegArg2, false, false)));
                            writer.Write(") ");

                            switch (midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.clt:
                                    writer.Write("<");
                                    break;
                                case MidInstruction.OpcodeEnum.cgt:
                                    writer.Write("<");
                                    break;
                                case MidInstruction.OpcodeEnum.ceq_numeric:
                                    writer.Write("==");
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            writer.Write(" (");
                            writer.Write(PassiveConvertValue(midInstr.RegArg3.VType, comparisonSpec, StorageLocForSsaReg(midInstr.RegArg3, false, false)));
                            writer.WriteLine("));");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.cne_ref:
                    case MidInstruction.OpcodeEnum.ceq_ref:
                        writer.Write(scopeStack.Indent);
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                        writer.Write(" = ");
                        if (midInstr.Opcode == MidInstruction.OpcodeEnum.ceq_ref)
                            writer.Write("(::CLRVM::ReferenceEqualityComparer< ");
                        else if (midInstr.Opcode == MidInstruction.OpcodeEnum.cne_ref)
                            writer.Write("(!::CLRVM::ReferenceEqualityComparer< ");
                        else
                            throw new ArgumentException();
                        writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg2.VType.TypeSpec));
                        writer.Write(", ");
                        writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg3.VType.TypeSpec));
                        writer.Write(" >::AreEqual(");
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg2, false, false));
                        writer.Write(", ");
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg3, false, false));
                        writer.WriteLine("));");
                        break;
                    case MidInstruction.OpcodeEnum.LoadArgA_Value:
                        writer.WriteLine("LoadArgA_Value SSA: " + midInstr.RegArg.SsaID + "  VReg: " + midInstr.VRegArg.SlotName);
                        break;
                    case MidInstruction.OpcodeEnum.brzero:
                    case MidInstruction.OpcodeEnum.brnotzero:
                        writer.Write(scopeStack.Indent);
                        writer.Write("if (");

                        if (midInstr.Opcode == MidInstruction.OpcodeEnum.brnotzero)
                            writer.Write("!");
                        else if (midInstr.Opcode != MidInstruction.OpcodeEnum.brzero)
                            throw new ArgumentException();

                        switch(midInstr.RegArg.VType.ValType)
                        {
                            case VType.ValTypeEnum.ConstantValue:
                            case VType.ValTypeEnum.ValueValue:
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        writer.Write("::CLRVM::IsNumberZero(::CLRTypes::DetagNumber(");
                        writer.Write(PassiveConvertValueToNumeric(midInstr.RegArg.VType, StorageLocForSsaReg(midInstr.RegArg, false, false)));
                        writer.WriteLine(")))");
                        writer.Write(scopeStack.Indent);
                        writer.WriteLine("{");
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        writer.Write(scopeStack.Indent);
                        writer.Write("\tgoto bLabel_");
                        writer.Write(m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        writer.WriteLine(";");
                        writer.Write(scopeStack.Indent);
                        writer.WriteLine("}");
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.brnull:
                    case MidInstruction.OpcodeEnum.brnotnull:
                        writer.Write(scopeStack.Indent);
                        writer.Write("if (");

                        if (midInstr.Opcode == MidInstruction.OpcodeEnum.brnotnull)
                            writer.Write("!");
                        else if (midInstr.Opcode != MidInstruction.OpcodeEnum.brnull)
                            throw new ArgumentException();

                        switch (midInstr.RegArg.VType.ValType)
                        {
                            case VType.ValTypeEnum.NotNullReferenceValue:
                            case VType.ValTypeEnum.NullableReferenceValue:
                            case VType.ValTypeEnum.Null:
                            case VType.ValTypeEnum.ConstantReference:
                                writer.Write("::CLRVM::IsNull");
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        writer.Write("< ");
                        writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg.VType.TypeSpec));
                        writer.Write(" >(");
                        writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, true));
                        writer.WriteLine("))");
                        writer.Write(scopeStack.Indent);
                        writer.WriteLine("{");
                        SpillCfgEdge(scopeStack.Indent, cfgNode, midInstr.CfgEdgeArg, writer);
                        writer.Write(scopeStack.Indent);
                        writer.Write("\tgoto bLabel_");
                        writer.Write(m_regAllocator.TargetIDForCfgNode(midInstr.CfgEdgeArg.SuccessorNode));
                        writer.WriteLine(";");
                        writer.Write(scopeStack.Indent);
                        writer.WriteLine("}");
                        AddNode(midInstr.CfgEdgeArg.SuccessorNode);
                        break;
                    case MidInstruction.OpcodeEnum.LeakReg:
                        {
                            leakedRegs.Add(midInstr.RegArg);
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
                                }
                            }
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Throw:
                        {
                            CLR.CLRTypeSpec objSpec = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(CLR.CLRSigType.ElementType.OBJECT));

                            writer.Write(scopeStack.Indent);
                            writer.Write("::CLRVM::Throw(");
                            writer.Write(m_frameVarName);
                            writer.Write(", ");
                            writer.Write(PassiveConvertValue(midInstr.RegArg.VType, objSpec, StorageLocForSsaReg(midInstr.RegArg, false, false)));
                            writer.WriteLine(");");

                            regionIsTerminated = true;
                        }
                        break;
                    case MidInstruction.OpcodeEnum.NewSZArray:
                        {
                            NumericStackType indexNst = StackTypeForTypeSpec(midInstr.RegArg2.VType.TypeSpec);
                            CLR.CLRSigType.ElementType indexElementType;

                            if (indexNst == NumericStackType.Int32)
                                indexElementType = CLR.CLRSigType.ElementType.I4;
                            else if (indexNst == NumericStackType.NativeInt)
                                indexElementType = CLR.CLRSigType.ElementType.I;    // CLARITYTODO: Test UIntPtr
                            else
                                throw new ArgumentException("Unusual array subscript type");

                            CLR.CLRTypeSpec indexSpec = m_builder.Assemblies.InternVagueType(new CLR.CLRSigTypeSimple(indexElementType));

                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                            writer.Write(" = CLRVM::ArrayCreator< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg.VType.TypeSpec));
                            writer.Write(" >::Create(");
                            writer.Write(m_frameVarName);
                            writer.Write(", ");
                            writer.Write(PassiveConvertValue(midInstr.RegArg2.VType, indexSpec, StorageLocForSsaReg(midInstr.RegArg2, false, false)));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_ManagedPtr:
                        // UNIMPLEMENTED
                        writer.WriteLine("LoadField_ManagedPtr Object SSA: " + midInstr.RegArg.SsaID + "  Value: " + midInstr.RegArg2.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldA_ManagedPtr:
                        // UNIMPLEMENTED
                        writer.WriteLine("LoadFieldA_ManagedPtr Object SSA: " + midInstr.RegArg.SsaID + "  Value: " + midInstr.RegArg2.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_Object:
                        // CLARITYTODO: Null ref check
                        {
                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg2, true, false));
                            writer.Write(" = ");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.Write("->f");
                            writer.Write(CppBuilder.LegalizeName(midInstr.StrArg, true));
                            writer.WriteLine(".Value();");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadFieldA_Object:
                        // UNIMPLEMENTED
                        writer.WriteLine("LoadFieldA_Object Object SSA: " + midInstr.RegArg.SsaID + "  Value: " + midInstr.RegArg2.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadField_Value:
                        // UNIMPLEMENTED
                        writer.WriteLine("LoadField_Value Object SSA: " + midInstr.RegArg.SsaID + "  Value: " + midInstr.RegArg2.SsaID + "  Field: " + midInstr.StrArg);
                        break;
                    case MidInstruction.OpcodeEnum.LoadRegA:
                        {
                            VType.ValTypeEnum baseValType = midInstr.VRegArg.VType.ValType;
                            if (baseValType != VType.ValTypeEnum.ValueValue
                                && baseValType != VType.ValTypeEnum.NullableReferenceValue)
                                throw new Exception("Bad vreg type for LoadRegA");

                            CLR.CLRTypeSpec boxType = midInstr.RegArg.VType.TypeSpec;
                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                            writer.Write(" = ::CLRVM::CreateLocalManagedPtr< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.VRegArg.VType.TypeSpec));
                            writer.Write(" >(");
                            writer.Write(StorageLocForVReg(midInstr.VRegArg, false, false));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayElem:
                    case MidInstruction.OpcodeEnum.LoadArrayElemAddr:
                        {
                            CLR.CLRTypeSpec szArraySpec = midInstr.RegArg.VType.TypeSpec;
                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg3, true, false));
                            writer.Write(" = ");

                            switch (midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.LoadArrayElem:
                                    writer.Write("::CLRVM::SZArrayLoader< ");
                                    break;
                                case MidInstruction.OpcodeEnum.LoadArrayElemAddr:
                                    writer.Write("::CLRVM::SZArrayAddrLoader< ");
                                    break;
                                default:
                                    throw new ArgumentException();
                            }
                            writer.Write(m_builder.SpecToAmbiguousStorage(szArraySpec));
                            writer.Write(" >::Load(");
                            writer.Write(m_frameVarName);
                            writer.Write(", ");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.Write(", ");

                            CLR.CLRTypeSpec indexSpec = TypeSpecForArrayIndex(midInstr.RegArg2.VType.TypeSpec);

                            writer.Write(PassiveConvertValue(midInstr.RegArg2.VType, indexSpec, StorageLocForSsaReg(midInstr.RegArg2, false, false)));
                            writer.WriteLine(");");
                        }
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
                        {
                            bool isUnsigned = (midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Un) != 0;
                            bool isOvf = (midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Ovf) != 0;
                            bool divsMayThrow;
                            NumericStackType nst = NumericStackTypeForNumericBinaryOp(midInstr.RegArg, midInstr.RegArg2);

                            switch(nst)
                            {
                                case NumericStackType.Float32:
                                case NumericStackType.Float64:
                                    divsMayThrow = false;
                                    break;
                                case NumericStackType.Int32:
                                case NumericStackType.Int64:
                                case NumericStackType.NativeInt:
                                    divsMayThrow = true;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            CLR.CLRTypeSpec opSpec = TypeSpecForNumericStackType(nst, isUnsigned);

                            string arithOp;
                            bool canThrow = isOvf;
                            switch (midInstr.Opcode)
                            {
                                case MidInstruction.OpcodeEnum.add:
                                    arithOp = "Add";
                                    break;
                                case MidInstruction.OpcodeEnum.sub:
                                    arithOp = "Subtract";
                                    break;
                                case MidInstruction.OpcodeEnum.mul:
                                    arithOp = "Multiply";
                                    break;
                                case MidInstruction.OpcodeEnum.div:
                                    arithOp = "Divide";
                                    if (divsMayThrow)
                                        canThrow = true;
                                    break;
                                case MidInstruction.OpcodeEnum.rem:
                                    arithOp = "Modulo";
                                    if (divsMayThrow)
                                        canThrow = true;
                                    break;
                                case MidInstruction.OpcodeEnum.and:
                                    arithOp = "BitwiseAnd";
                                    break;
                                case MidInstruction.OpcodeEnum.or:
                                    arithOp = "BitwiseOr";
                                    break;
                                case MidInstruction.OpcodeEnum.xor:
                                    arithOp = "BitwiseXor";
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg3, true, false));
                            writer.Write(" = CLRVM::ArithOps< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(opSpec));
                            writer.Write(" >::");
                            writer.Write(arithOp);
                            if (isOvf)
                                writer.Write("Ovf");

                            writer.Write("(");
                            if (canThrow)
                            {
                                writer.Write(m_frameVarName);
                                writer.Write(", ");
                            }

                            writer.Write(PassiveConvertValue(midInstr.RegArg.VType, opSpec, StorageLocForSsaReg(midInstr.RegArg, false, false)));
                            writer.Write(", ");
                            writer.Write(PassiveConvertValue(midInstr.RegArg2.VType, opSpec, StorageLocForSsaReg(midInstr.RegArg2, false, false)));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.shl:
                    case MidInstruction.OpcodeEnum.shr: // [.un]
                        writer.WriteLine("ShiftOp " + midInstr.Opcode.ToString() + "  Value 1 SSA: " + midInstr.RegArg.SsaID + "  Value 2 SSA: " + midInstr.RegArg2.SsaID + "  Result SSA: " + midInstr.RegArg3.SsaID + "  Arith mode " + (int)midInstr.ArithArg);
                        break;
                    case MidInstruction.OpcodeEnum.neg:
                    case MidInstruction.OpcodeEnum.not:
                        writer.WriteLine("UnaryArith " + midInstr.Opcode.ToString() + "  Value SSA: " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID + "  Arith mode " + (int)midInstr.ArithArg);
                        break;
                    case MidInstruction.OpcodeEnum.TryConvertObj:
                        {
                            CLR.CLRTypeSpec sourceSpec = midInstr.RegArg.VType.TypeSpec;
                            CLR.CLRTypeSpec destSpec = midInstr.RegArg2.VType.TypeSpec;

                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg2, true, false));
                            writer.Write(" = ::CLRVM::DynamicCaster< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(sourceSpec));
                            writer.Write(", ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(destSpec));
                            writer.Write(" >::Cast(");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Leave:
                        writer.WriteLine("Leave Esc " + midInstr.UIntArg);
                        break;
                    case MidInstruction.OpcodeEnum.DuplicateReg:
                        writer.WriteLine("DuplicateReg SSA: " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.StoreStaticField:

                        {
                            int staticInitID = m_regAllocator.AllocStaticToken(midInstr.TypeSpecArg);
                            writer.Write(scopeStack.Indent);
                            writer.Write("if (!bStaticInit");
                            writer.Write(staticInitID);
                            writer.WriteLine(")");

                            writer.Write(scopeStack.Indent);
                            writer.WriteLine("{");

                            writer.Write(scopeStack.Indent);
                            writer.Write("\t::CLRVM::InitStaticToken< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.TypeSpecArg));
                            writer.Write(" >(");
                            writer.Write(m_frameVarName);
                            writer.Write(", bTracedLocals.bStatic");
                            writer.Write(staticInitID);
                            writer.WriteLine(".m_value);");

                            writer.Write(scopeStack.Indent);
                            writer.Write("\tbStaticInit");
                            writer.Write(staticInitID);
                            writer.WriteLine(" = true;");

                            writer.Write(scopeStack.Indent);
                            writer.WriteLine("}");

                            writer.Write(scopeStack.Indent);
                            writer.Write("bTracedLocals.bStatic");
                            writer.Write(staticInitID);
                            writer.Write(".m_value->f");
                            writer.Write(CppBuilder.LegalizeName(midInstr.StrArg, true));
                            writer.Write(".Set(");

                            writer.Write(PassiveConvertValue(midInstr.RegArg.VType, midInstr.TypeSpecArg2, StorageLocForSsaReg(midInstr.RegArg, true, false)));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadIndirect:
                        {
                            writer.Write(scopeStack.Indent);

                            switch (midInstr.RegArg.VType.ValType)
                            {
                                case VType.ValTypeEnum.AnchoredManagedPtr:
                                    writer.Write("::CLRVM::LoadAnchoredManagedPtr");
                                    break;
                                case VType.ValTypeEnum.LocalManagedPtr:
                                    writer.Write("::CLRVM::LoadAnchoredManagedPtr");
                                    break;
                                case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                                    writer.Write("::CLRVM::LoadMaybeAnchoredManagedPtr");
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            writer.Write("< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg.VType.TypeSpec));
                            writer.Write(" >(");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg2, true, false));
                            writer.Write(", ");
                            writer.Write(PassiveConvertValue(midInstr.RegArg.VType, midInstr.RegArg2.VType.TypeSpec, StorageLocForSsaReg(midInstr.RegArg, false, false)));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadStaticField:
                        {
                            int staticInitID = m_regAllocator.AllocStaticToken(midInstr.TypeSpecArg);
                            writer.Write(scopeStack.Indent);
                            writer.Write("if (!bStaticInit");
                            writer.Write(staticInitID);
                            writer.WriteLine(")");

                            writer.Write(scopeStack.Indent);
                            writer.WriteLine("{");

                            writer.Write(scopeStack.Indent);
                            writer.Write("\t::CLRVM::InitStaticToken< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.TypeSpecArg));
                            writer.Write(" >(");
                            writer.Write(m_frameVarName);
                            writer.Write(", bTracedLocals.bStatic");
                            writer.Write(staticInitID);
                            writer.WriteLine(".m_value);");
                            
                            writer.Write(scopeStack.Indent);
                            writer.Write("\tbStaticInit");
                            writer.Write(staticInitID);
                            writer.WriteLine(" = true;");

                            writer.Write(scopeStack.Indent);
                            writer.WriteLine("}");

                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, true, false));
                            writer.Write(" = ");
                            writer.Write("bTracedLocals.bStatic");
                            writer.Write(staticInitID);
                            writer.Write(".m_value->f");
                            writer.Write(CppBuilder.LegalizeName(midInstr.StrArg, true));
                            writer.WriteLine(".Value();");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Box:
                        {
                            CLR.CLRTypeSpec boxType = midInstr.RegArg.VType.TypeSpec;
                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg2, true, false));
                            writer.Write(" = ::CLRVM::Box< ");

                            if (boxType.UsesAnyGenericParams)
                                writer.Write("typename ");

                            writer.Write(m_builder.SpecToAmbiguousStorage(boxType));
                            writer.Write(" >(");
                            writer.Write(m_frameVarName);
                            writer.Write(", ");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.ConvertNumber:
                        {
                            bool isOvf = ((midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Ovf) != 0);
                            bool isUn = ((midInstr.ArithArg & MidInstruction.ArithEnum.Flags_Un) != 0);

                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg2, true, false));
                            writer.Write(" = ::CLRVM::ClrSemanticNumberConverter< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg.VType.TypeSpec));
                            writer.Write(", ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg2.VType.TypeSpec));

                            writer.Write(isOvf ? ", 1" : ", 0");
                            writer.Write(isUn ? ", 1" : ", 0");

                            if (isOvf)
                            {
                                writer.Write(" >::CheckedConvert(");
                                writer.Write(m_frameVarName);
                                writer.Write(", ");
                            }
                            else
                                writer.Write(" >::Convert(");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadArrayLength:
                        {
                            writer.Write(scopeStack.Indent);
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg2, true, false));
                            writer.Write(" = ::CLRVM::ArrayLengthReader< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(midInstr.RegArg.VType.TypeSpec));
                            writer.Write(" >::Read(");
                            writer.Write(m_frameVarName);
                            writer.Write(", ");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.LoadTypeInfoHandle:
                        // UNIMPLEMENTED
                        writer.WriteLine("LoadTypeInfoHandle SSA " + midInstr.RegArg.SsaID + "  Type: " + midInstr.TypeSpecArg);
                        break;
                    case MidInstruction.OpcodeEnum.ConvertObj:
                        // UNIMPLEMENTED
                        writer.WriteLine("ConvertObj SSA " + midInstr.RegArg.SsaID + "  Result SSA: " + midInstr.RegArg2.SsaID);
                        break;
                    case MidInstruction.OpcodeEnum.StoreArrayElem:
                        {
                            CLR.CLRTypeSpec szArraySpec = midInstr.RegArg.VType.TypeSpec;
                            CLR.CLRTypeSpec subscriptType = ((CLR.CLRTypeSpecSZArray)szArraySpec).SubType;
                            writer.Write(scopeStack.Indent);

                            writer.Write("::CLRVM::SZArrayStorer< ");
                            writer.Write(m_builder.SpecToAmbiguousStorage(szArraySpec));
                            writer.Write(" >::Store(");
                            writer.Write(m_frameVarName);
                            writer.Write(", ");
                            writer.Write(StorageLocForSsaReg(midInstr.RegArg, false, false));
                            writer.Write(", ");

                            CLR.CLRTypeSpec indexSpec = TypeSpecForArrayIndex(midInstr.RegArg2.VType.TypeSpec);

                            writer.Write(PassiveConvertValue(midInstr.RegArg2.VType, indexSpec, StorageLocForSsaReg(midInstr.RegArg2, false, false)));
                            writer.Write(", ");
                            writer.Write(PassiveConvertValue(midInstr.RegArg3.VType, subscriptType, StorageLocForSsaReg(midInstr.RegArg3, false, false)));

                            writer.WriteLine(");");
                        }
                        break;
                    case MidInstruction.OpcodeEnum.Switch:
                        {
                            writer.WriteLine("Switch Value SSA " + midInstr.RegArg.SsaID);
                            int numCases = midInstr.CfgEdgesArg.Length;
                            for (int i = 0; i < numCases; i++)
                            {
                                CfgOutboundEdge edge = midInstr.CfgEdgesArg[numCases - 1 - i];
                                CfgNode caseNode = edge.SuccessorNode;
                                SpillCfgEdge(scopeStack.Indent, cfgNode, edge, writer);
                                AddNode(caseNode);
                            }
                            foreach (CfgOutboundEdge outboundEdge in midInstr.CfgEdgesArg)
                                writer.WriteLine("    Case target: " + m_regAllocator.TargetIDForCfgNode(outboundEdge.SuccessorNode));
                        }
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
                                CppRegionEmitter tryEmitter = new CppRegionEmitter(m_depSet, m_baseIndentLevel + 1, m_builder, cluster.TryRegion, m_regAllocator, m_frameVarName);
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
                                CppRegionEmitter hdlEmitter = new CppRegionEmitter(m_depSet, m_baseIndentLevel + 1, m_builder, handlerRegion, m_regAllocator, m_frameVarName);
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
                if (!exitIsContinuous)
                {
                    SpillCfgEdge(scopeStack.Indent, cfgNode, cfgNode.FallThroughEdge, writer);
                    writer.Write(scopeStack.Indent);
                    writer.Write("goto bLabel_");
                    writer.Write(m_regAllocator.TargetIDForCfgNode(cfgNode.FallThroughEdge.SuccessorNode));
                    writer.WriteLine(";  // Non-continuous exit");

                    foreach (SsaRegister leakedReg in leakedRegs)
                        scopeStack.KillReg(leakedReg, writer);
                }

                AddNode(cfgNode.FallThroughEdge.SuccessorNode);
            }
        }
    }
}
