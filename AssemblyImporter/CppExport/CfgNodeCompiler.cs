using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using Clarity.Rpa;

namespace AssemblyImporter.CppExport
{
    public class CfgNodeCompiler
    {
        public class CommonTypeLookup
        {
            public CLRTypeSpec I { get; private set; }
            public CLRTypeSpec I8 { get; private set; }
            public CLRTypeSpec I16 { get; private set; }
            public CLRTypeSpec I32 { get; private set; }
            public CLRTypeSpec I64 { get; private set; }
            public CLRTypeSpec U { get; private set; }
            public CLRTypeSpec U8 { get; private set; }
            public CLRTypeSpec U16 { get; private set; }
            public CLRTypeSpec U32 { get; private set; }
            public CLRTypeSpec U64 { get; private set; }
            public CLRTypeSpec F32 { get; private set; }
            public CLRTypeSpec F64 { get; private set; }
            public CLRTypeSpec Object { get; private set; }
            public CLRTypeSpec String { get; private set; }
            public CLRTypeSpec Char { get; private set; }
            public CLRTypeSpec Boolean { get; private set; }
            public CLRTypeSpec Array { get; private set; }
            public CLRTypeSpec ValueType { get; private set; }

            public CommonTypeLookup(CppBuilder builder)
            {
                CLRAssemblyCollection assm = builder.Assemblies;

                I = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.I));
                I8 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.I1));
                I16 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.I2));
                I32 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.I4));
                I64 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.I8));
                U = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.U));
                U8 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.U1));
                U16 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.U2));
                U32 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.U4));
                U64 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.U8));
                F32 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.R4));
                F64 = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.R8));
                Object = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.OBJECT));
                String = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.STRING));
                Char = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.CHAR));
                Boolean = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.BOOLEAN));
                Array = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.ARRAY));
                ValueType = assm.InternVagueType(new CLRSigTypeSimple(CLRSigType.ElementType.VALUETYPE));
            }
        }

        public MidInstruction[] OutputInstructions { get; private set; }
        public CfgOutboundEdge OutputFallThroughEdge { get; private set; }
        public CppBuilder CppBuilder { get { return m_cppBuilder; } }
        public CommonTypeLookup CommonTypeLookupInst { get { return m_commonTypeLookup; } }

        private CfgNode m_cfgNode;
        private CommonTypeLookup m_commonTypeLookup;
        private CppBuilder m_cppBuilder;
        private ExceptionHandlingRegion m_ehRegion;
        private SortedSet<uint> m_escapePaths;
        private IList<VReg> m_temporaries;

        public CfgNodeCompiler(CfgNode cfgNode)
        {
            m_cppBuilder = cfgNode.CfgBuilder.CppBuilder;
            m_commonTypeLookup = new CommonTypeLookup(m_cppBuilder);
            m_cfgNode = cfgNode;
            m_ehRegion = cfgNode.CfgBuilder.Region;
            m_escapePaths = new SortedSet<uint>();
            m_temporaries = cfgNode.CfgBuilder.Temporaries;
        }

        private VReg GetTemporary(VType vType)
        {
            foreach (VReg vReg in m_temporaries)
                if (vReg.VType.Equals(vType))
                    return vReg;

            int slot = m_temporaries.Count + m_cfgNode.CfgBuilder.Locals.Length + m_cfgNode.CfgBuilder.Args.Length;
            VReg newVReg = new VReg(m_cppBuilder, "temp_", vType, slot, VReg.UsageEnum.Temporary);

            m_temporaries.Add(newVReg);
            return newVReg;
        }

        private CppField ResolveField(CLRTableRow tableRow)
        {
            if (tableRow is CLRFieldRow)
                return new CppField(m_cppBuilder.Assemblies, (CLRFieldRow)tableRow);
            if (tableRow is CLRMemberRefRow)
            {
                CLRMemberRefRow memberRef = (CLRMemberRefRow)tableRow;
                CLRTypeSpec declaredIn = m_cppBuilder.ResolveTypeDefOrRefOrSpec(memberRef.Class);
                CLRTypeSpec fieldType = m_cppBuilder.Assemblies.InternVagueType(memberRef.FieldSig.Type);
                CppClass cachedClass = m_cppBuilder.GetCachedClass(declaredIn);

                foreach (CppField field in cachedClass.Fields)
                {
                    if (field.Name == memberRef.Name && field.Type.Equals(fieldType))
                        return field;
                }
                throw new ParseFailedException("Unresolved field reference");
            }
            throw new ArgumentException();
        }

        public CLRTypeSpec ArithConvergeValues(CLRTypeSpec ts1, CLRTypeSpec ts2)
        {
            // Both are value types, so these must converge
            CLRTypeSpec promoted1 = ArithPromoteValue(ts1);
            CLRTypeSpec promoted2 = ArithPromoteValue(ts2);

            // If both promote to the same thing, use that
            if (promoted1.Equals(promoted2))
                return promoted1;

            if (promoted1.Equals(m_commonTypeLookup.I32) && promoted2.Equals(m_commonTypeLookup.I64))
                return m_commonTypeLookup.I64;
            if (promoted1.Equals(m_commonTypeLookup.I64) && promoted2.Equals(m_commonTypeLookup.I32))
                return m_commonTypeLookup.I64;
            if (promoted1.Equals(m_commonTypeLookup.F32) && promoted2.Equals(m_commonTypeLookup.F64))
                return m_commonTypeLookup.F64;
            if (promoted1.Equals(m_commonTypeLookup.F64) && promoted2.Equals(m_commonTypeLookup.F32))
                return m_commonTypeLookup.F64;

            // Unmergable
            throw new ArgumentException();
        }

        public CLRTypeSpec ResolveEnumArithType(CLRTypeSpec typeSpec)
        {
            if (!(typeSpec is CLRTypeSpecClass))
                return null;

            CLRTypeDefRow typeDef = ((CLRTypeSpecClass)typeSpec).TypeDef;
            if (typeDef.Extends == null)
                return null;

            CLRTypeSpec extendsSpec = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(typeDef.Extends);
            if (!(extendsSpec is CLRTypeSpecClass))
                return null;

            CLRTypeDefRow extendsDef = ((CLRTypeSpecClass)extendsSpec).TypeDef;
            if (extendsDef.ContainerClass != null || extendsDef.TypeNamespace != "System" || extendsDef.TypeName != "Enum")
                return null;

            foreach (CLRFieldRow field in typeDef.Fields)
            {
                if (!field.Static)
                    return m_cppBuilder.Assemblies.InternVagueType(field.Signature.Type);
            }
            return null;
        }

        public CLRTypeSpec ArithPromoteValue(CLRTypeSpec typeSpec)
        {
            if (typeSpec.Equals(m_commonTypeLookup.I8)
                || typeSpec.Equals(m_commonTypeLookup.I16)
                || typeSpec.Equals(m_commonTypeLookup.I32)
                || typeSpec.Equals(m_commonTypeLookup.U8)
                || typeSpec.Equals(m_commonTypeLookup.U16)
                || typeSpec.Equals(m_commonTypeLookup.U32)
                || typeSpec.Equals(m_commonTypeLookup.Char)
                || typeSpec.Equals(m_commonTypeLookup.Boolean)
                )
                return m_commonTypeLookup.I32;
            if (typeSpec.Equals(m_commonTypeLookup.I64) || typeSpec.Equals(m_commonTypeLookup.U64))
                return m_commonTypeLookup.I64;
            if (typeSpec.Equals(m_commonTypeLookup.U) || typeSpec.Equals(m_commonTypeLookup.U))
                return m_commonTypeLookup.I;
            if (typeSpec.Equals(m_commonTypeLookup.F32))
                return m_commonTypeLookup.F32;
            if (typeSpec.Equals(m_commonTypeLookup.F64))
                return m_commonTypeLookup.F64;

            {
                CLRTypeSpec enumArithType = ResolveEnumArithType(typeSpec);
                if (enumArithType != null)
                    return ArithPromoteValue(enumArithType);
            }

            throw new ArgumentException();
        }

        public CLRTypeSpec TypeSpecForArithModeResult(MidInstruction.ArithEnum arithMode)
        {
            bool isUnsigned = ((arithMode & MidInstruction.ArithEnum.Flags_Un) != 0);

            switch (arithMode & MidInstruction.ArithEnum.ArithType_Mask)
            {
                case MidInstruction.ArithEnum.ArithType_Float32:
                    return m_commonTypeLookup.F32;
                case MidInstruction.ArithEnum.ArithType_Float64:
                    return m_commonTypeLookup.F64;
                case MidInstruction.ArithEnum.ArithType_Int32:
                    return isUnsigned ? m_commonTypeLookup.U32 : m_commonTypeLookup.I32;
                case MidInstruction.ArithEnum.ArithType_Int64:
                    return isUnsigned ? m_commonTypeLookup.U64 : m_commonTypeLookup.I64;
                case MidInstruction.ArithEnum.ArithType_NativeInt:
                    return isUnsigned ? m_commonTypeLookup.U : m_commonTypeLookup.I;
            }
            throw new ArgumentException();
        }

        private static MidInstruction.ArithEnum ArithModeForBinaryNumericOp(CommonTypeLookup lookup, CLRTypeSpec ls, CLRTypeSpec rs)
        {
            // III.5
            if (ls.Equals(lookup.I32))
            {
                if (rs.Equals(lookup.I32))
                    return MidInstruction.ArithEnum.ArithType_Int32;
                if (rs.Equals(lookup.I))
                    return MidInstruction.ArithEnum.ArithType_NativeInt;
                throw new ArgumentException();
            }
            if (ls.Equals(lookup.I64))
            {
                if (rs.Equals(lookup.I64))
                    return MidInstruction.ArithEnum.ArithType_Int64;
                throw new ArgumentException();
            }
            if (ls.Equals(lookup.I))
            {
                if (rs.Equals(lookup.I32))
                    return MidInstruction.ArithEnum.ArithType_NativeInt;
                if (rs.Equals(lookup.I))
                    return MidInstruction.ArithEnum.ArithType_NativeInt;
                throw new ArgumentException();
            }
            if (ls.Equals(lookup.F32))
            {
                if (rs.Equals(lookup.F32))
                    return MidInstruction.ArithEnum.ArithType_Float32;
                if (rs.Equals(lookup.F64))
                    return MidInstruction.ArithEnum.ArithType_Float64;
                throw new ArgumentException();
            }
            if (ls.Equals(lookup.F64))
            {
                if (rs.Equals(lookup.F32))
                    return MidInstruction.ArithEnum.ArithType_Float64;
                if (rs.Equals(lookup.F64))
                    return MidInstruction.ArithEnum.ArithType_Float64;
                throw new ArgumentException();
            }
            throw new ArgumentException();
        }

        private static MidInstruction.ArithEnum ArithModeForShiftOp(CommonTypeLookup lookup, CLRTypeSpec ls, CLRTypeSpec rs)
        {
            // III.6
            if (ls.Equals(lookup.I32))
            {
                if (rs.Equals(lookup.I32))
                    return MidInstruction.ArithEnum.ArithType_Int32;
                throw new ArgumentException();
            }
            if (ls.Equals(lookup.I64))
            {
                if (rs.Equals(lookup.I64))
                    return MidInstruction.ArithEnum.ArithType_Int64;
                if (rs.Equals(lookup.I))
                    return MidInstruction.ArithEnum.ArithType_Int64;
                throw new ArgumentException();
            }
            if (ls.Equals(lookup.I))
            {
                if (rs.Equals(lookup.I64))
                    return MidInstruction.ArithEnum.ArithType_NativeInt;
                if (rs.Equals(lookup.I))
                    return MidInstruction.ArithEnum.ArithType_NativeInt;
                throw new ArgumentException();
            }
            throw new ArgumentException();
        }

        private MidInstruction.ArithEnum ArithModeForUnaryOp(CLRTypeSpec ts)
        {
            // III.6
            if (ts.Equals(m_commonTypeLookup.I32))
                return MidInstruction.ArithEnum.ArithType_Int32;
            if (ts.Equals(m_commonTypeLookup.I64))
                return MidInstruction.ArithEnum.ArithType_Int64;
            if (ts.Equals(m_commonTypeLookup.F32))
                return MidInstruction.ArithEnum.ArithType_Float32;
            if (ts.Equals(m_commonTypeLookup.F64))
                return MidInstruction.ArithEnum.ArithType_Float64;
            if (ts.Equals(m_commonTypeLookup.I))
                return MidInstruction.ArithEnum.ArithType_NativeInt;
            throw new ArgumentException();
        }

        private static MidInstruction.OpcodeEnum SimpleTranslateInstr(CLR.CIL.HLOpcode opcode)
        {
            switch (opcode)
            {
                case CLR.CIL.HLOpcode.bge:
                    return MidInstruction.OpcodeEnum.bge;
                case CLR.CIL.HLOpcode.bgt:
                    return MidInstruction.OpcodeEnum.bgt;
                case CLR.CIL.HLOpcode.ble:
                    return MidInstruction.OpcodeEnum.ble;
                case CLR.CIL.HLOpcode.blt:
                    return MidInstruction.OpcodeEnum.blt;
                case CLR.CIL.HLOpcode.clt:
                    return MidInstruction.OpcodeEnum.clt;
                case CLR.CIL.HLOpcode.add:
                    return MidInstruction.OpcodeEnum.add;
                case CLR.CIL.HLOpcode.sub:
                    return MidInstruction.OpcodeEnum.sub;
                case CLR.CIL.HLOpcode.mul:
                    return MidInstruction.OpcodeEnum.mul;
                case CLR.CIL.HLOpcode.div:
                    return MidInstruction.OpcodeEnum.div;
                case CLR.CIL.HLOpcode.rem:
                    return MidInstruction.OpcodeEnum.rem;
                case CLR.CIL.HLOpcode.and:
                    return MidInstruction.OpcodeEnum.and;
                case CLR.CIL.HLOpcode.or:
                    return MidInstruction.OpcodeEnum.or;
                case CLR.CIL.HLOpcode.xor:
                    return MidInstruction.OpcodeEnum.xor;
                case CLR.CIL.HLOpcode.shl:
                    return MidInstruction.OpcodeEnum.shl;
                case CLR.CIL.HLOpcode.shr:
                    return MidInstruction.OpcodeEnum.shr;
                case CLR.CIL.HLOpcode.not:
                    return MidInstruction.OpcodeEnum.not;
                case CLR.CIL.HLOpcode.neg:
                    return MidInstruction.OpcodeEnum.neg;
                default:
                    throw new ArgumentException();
            }
        }

        private static bool IsComparisonValueReference(VType vType)
        {
            switch (vType.ValType)
            {
                case VType.ValTypeEnum.ReferenceValue:
                case VType.ValTypeEnum.ConstantReference:
                case VType.ValTypeEnum.Null:
                    return true;
                case VType.ValTypeEnum.ValueValue:
                case VType.ValTypeEnum.ConstantValue:
                    return false;
                default:
                    throw new Exception("Invalid value type for comparison");
            }
        }

        private static bool IsComparisonReference(VType vType1, VType vType2)
        {
            bool isRef = IsComparisonValueReference(vType1);
            if (isRef != IsComparisonValueReference(vType2))
                throw new Exception("Couldn't resolve comparison reference status");
            return isRef;
        }

        public void Compile()
        {
            CppMethod method = m_cfgNode.CfgBuilder.CppMethod;
            VReg[] locals = m_cfgNode.CfgBuilder.Locals;
            VReg[] args = m_cfgNode.CfgBuilder.Args;
            CfgBuilder cfgBuilder = m_cfgNode.CfgBuilder;

            CLR.CIL.Method cilMethod = method.MethodDef.Method;
            List<MidInstruction> midInstrs = new List<MidInstruction>();
            bool[] everTargeted = new bool[cilMethod.Instructions.Length];

            EvalStackTracker stackTracker = new EvalStackTracker();

            Clarity.Rpa.CodeLocationTag codeLocation = new Clarity.Rpa.CodeLocationTag(method.VtableSlotTag, cilMethod.OffsetForInstruction(m_cfgNode.StartInstr));

            foreach (VType entryType in m_cfgNode.EntryTypes)
            {
                SsaRegister reg = stackTracker.NewReg(entryType);
                reg.TrySpill();
                stackTracker.Push(reg);

                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.EntryReg, codeLocation, reg));
            }

            int firstInstr = m_cfgNode.StartInstr;
            int nextInstr = firstInstr;


            bool isTerminalEdge = false;
            while (!isTerminalEdge)
            {
                int instrNum = nextInstr++;
                codeLocation = new Clarity.Rpa.CodeLocationTag(method.VtableSlotTag, cilMethod.OffsetForInstruction(instrNum));

                if (instrNum != firstInstr && cfgBuilder.InstrIsJumpTarget(instrNum))
                {
                    CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();
                    OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, cfgBuilder.AddCfgTarget(this, instrNum, edgeProto.OutboundTypes), edgeProto);
                    break;
                }

                if (cfgBuilder.EhClusters.ContainsKey((uint)instrNum))
                {
                    if (stackTracker.Depth != 0)
                        throw new ParseFailedException("Stack not empty at protected block entry point");
                    ExceptionHandlingCluster cluster = cfgBuilder.EhClusters[(uint)instrNum];
                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.EnterProtectedBlock, codeLocation, cluster));

                    VType[] emptyTypesList = new VType[0];

                    foreach (uint escapePath in cluster.EscapePaths)
                    {
                        if (escapePath >= m_ehRegion.StartInstr && escapePath <= m_ehRegion.EndInstr)
                        {
                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)escapePath, emptyTypesList);
                            m_ehRegion.AddLeaveTarget(escapePath, targetNode);
                        }
                        else
                            m_ehRegion.AddEscapePath(escapePath);
                    }

                    break;
                }

                CLR.CIL.HLInstruction instr = cilMethod.Instructions[instrNum];

                switch (instr.Opcode)
                {
                    case CLR.CIL.HLOpcode.nop:
                        break;
                    case CLR.CIL.HLOpcode.newobj:
                        {
                            CppMethodSpec ctorMethodSpec = CppBuilder.ResolveMethodDefOrRef((CLRTableRow)instr.Arguments.ObjValue);

                            if (ctorMethodSpec.GenericParameters != null)
                                throw new ArgumentException();

                            CppMethod ctorMethod = ctorMethodSpec.CppMethod;

                            int numParams = ctorMethod.MethodSignature.ParamTypes.Length;

                            CLRTypeSpec instanceSpec = ctorMethod.DeclaredInClassSpec;

                            VType.ValTypeEnum resultValType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, instanceSpec);
                            SsaRegister instanceReg = stackTracker.NewReg(new VType(resultValType, instanceSpec));

                            // Allocate the instance into an SSA reg
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, instanceReg));

                            stackTracker.SpillStack();

                            bool isDelegate = false;

                            if (numParams == 2)
                            {
                                SsaRegister param2 = stackTracker.GetFromTop(0);
                                if (param2.VType.ValType == VType.ValTypeEnum.DelegateSimpleMethod ||
                                    param2.VType.ValType == VType.ValTypeEnum.DelegateVirtualMethod)
                                    isDelegate = true;
                            }

                            // Determine the parameter set.
                            // Unlike when making calls, we don't spill here because we already spilled during the AllocObject call
                            SsaRegister[] passedParams = new SsaRegister[numParams];
                            for (int p = 0; p < numParams; p++)
                            {
                                SsaRegister paramReg = stackTracker.GetFromTop(numParams - 1 - p);
                                passedParams[p] = paramReg;
                            }

                            CppClass cls = CppBuilder.GetCachedClass(instanceSpec);
                            SsaRegister valueOutRegister = null;
                            VReg tempLocal = null;
                            if (cls.IsValueType)
                            {
                                valueOutRegister = instanceReg;

                                tempLocal = GetTemporary(new VType(VType.ValTypeEnum.ValueValue, instanceSpec));

                                SsaRegister tempClearAddr = new SsaRegister(new VType(VType.ValTypeEnum.ManagedPtr, instanceSpec));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, tempClearAddr));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadRegA, codeLocation, tempLocal, tempClearAddr));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ZeroFillPtr, codeLocation, tempClearAddr));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, tempClearAddr));

                                SsaRegister tempUseAddr = new SsaRegister(new VType(VType.ValTypeEnum.ManagedPtr, instanceSpec));

                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, tempUseAddr));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadRegA, codeLocation, tempLocal, tempUseAddr));

                                instanceReg = tempUseAddr;
                            }

                            if (isDelegate)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.BindDelegate, codeLocation, passedParams[0], passedParams[1], instanceReg));
                            else
                            {
                                // Allocate object
                                if (tempLocal == null)
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.AllocObject, codeLocation, instanceReg, instanceSpec));
                                // Make the actual ctor call
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.CallConstructor, codeLocation, ctorMethodSpec, null, null, instanceReg, passedParams));
                            }

                            // Kill all of the parameter registers
                            for (int p = 0; p < numParams; p++)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, stackTracker.Pop()));

                            if (valueOutRegister != null)
                            {
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_Value, codeLocation, tempLocal, valueOutRegister));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, instanceReg));

                                instanceReg = valueOutRegister;
                            }

                            // Push the return value on to the stack
                            stackTracker.Push(instanceReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ret:
                        if (stackTracker.Depth == 1)
                        {
                            SsaRegister returnValue = stackTracker.Pop();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ReturnValue, codeLocation, returnValue, method.MethodSignature.RetType));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, returnValue));
                        }
                        else if (stackTracker.Depth == 0)
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Return, codeLocation));
                        else
                            throw new ArgumentException();

                        isTerminalEdge = true;
                        break;
                    case CLR.CIL.HLOpcode.ldarg:
                        {
                            VReg argReg = args[instr.Arguments.U32Value];

                            SsaRegister evalReg = stackTracker.NewReg(argReg.VType);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, evalReg));

                            string loadSrc = argReg.SlotName;
                            switch (argReg.VType.ValType)
                            {
                                case VType.ValTypeEnum.ManagedPtr:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_ManagedPtr, codeLocation, argReg, evalReg));
                                    break;
                                case VType.ValTypeEnum.ReferenceValue:
                                case VType.ValTypeEnum.ValueValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_Value, codeLocation, argReg, evalReg));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            stackTracker.Push(evalReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldarga:
                        {
                            VReg argReg = args[instr.Arguments.U32Value];

                            VType outEvalType;
                            MidInstruction.OpcodeEnum opcode;

                            switch (argReg.VType.ValType)
                            {
                                case VType.ValTypeEnum.ReferenceValue:
                                case VType.ValTypeEnum.ValueValue:
                                    opcode = MidInstruction.OpcodeEnum.LoadArgA_Value;
                                    outEvalType = new VType(VType.ValTypeEnum.ManagedPtr, argReg.VType.TypeSpec);
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            SsaRegister evalReg = stackTracker.NewReg(outEvalType);
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, evalReg));
                            midInstrs.Add(new MidInstruction(opcode, codeLocation, argReg, evalReg));

                            stackTracker.Push(evalReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.starg:
                        {
                            VReg argReg = args[instr.Arguments.U32Value];
                            SsaRegister valueReg = stackTracker.Pop();

                            switch (valueReg.VType.ValType)
                            {
                                case VType.ValTypeEnum.ConstantReference:
                                case VType.ValTypeEnum.ReferenceValue:
                                case VType.ValTypeEnum.Null:
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.ConstantValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreReg_Value, codeLocation, argReg, valueReg));
                                    break;
                                case VType.ValTypeEnum.ManagedPtr:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreReg_ManagedPtr, codeLocation, argReg, valueReg));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, valueReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.call:
                    case CLR.CIL.HLOpcode.callvirt:
                        {
                            bool devirtualize = false;

                            CLRTypeSpec constraintType = null;
                            if (instrNum != 0)
                            {
                                CLR.CIL.HLInstruction prevInstr = cilMethod.Instructions[instrNum - 1];
                                if (prevInstr.Opcode == CLR.CIL.HLOpcode.constrained_pfx)
                                    constraintType = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)prevInstr.Arguments.ObjValue);
                            }

                            CppMethodSpec calledMethodSpec = CppBuilder.ResolveMethodDefOrRef((CLRTableRow)instr.Arguments.ObjValue);
                            CppMethod calledMethod = calledMethodSpec.CppMethod;

                            if (!calledMethod.Virtual)
                                devirtualize = true;

                            int numParams = calledMethod.MethodSignature.ParamTypes.Length;

                            SsaRegister returnReg = null;
                            SsaRegister thisReg = null;

                            if (!CppBuilder.TypeSpecIsVoid(calledMethod.MethodSignature.RetType))
                            {
                                CLRTypeSpec retType = calledMethod.MethodSignature.RetType;
                                returnReg = stackTracker.NewReg(new VType(CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, retType), retType));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, returnReg));
                            }

                            if (!calledMethod.Static)
                                thisReg = stackTracker.GetFromTop(numParams);

                            MidInstruction.OpcodeEnum midOpcode;
                            if (instr.Opcode == CLR.CIL.HLOpcode.call || devirtualize)
                            {
                                if (constraintType != null)
                                    midOpcode = MidInstruction.OpcodeEnum.ConstrainedCallMethod;
                                else
                                    midOpcode = MidInstruction.OpcodeEnum.CallMethod;
                            }
                            else if (instr.Opcode == CLR.CIL.HLOpcode.callvirt)
                            {
                                if (constraintType != null)
                                    midOpcode = MidInstruction.OpcodeEnum.ConstrainedCallVirtualMethod;
                                else
                                {
                                    midOpcode = MidInstruction.OpcodeEnum.CallVirtualMethod;
                                    if (calledMethod.NumGenericParameters > 0)
                                    {
                                        if (calledMethod.DeclaredInClass.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                                            throw new NotSupportedException(method.DeclaredInClassSpec.ToString() + "." + method.Name + " contains an unconstrained call to an interface virtual method, which is not supported.");
                                        else if (calledMethod.DeclaredInClass.Semantics == CLRTypeDefRow.TypeSemantics.Class)
                                            midOpcode = MidInstruction.OpcodeEnum.CallMethod;
                                        else
                                            throw new ArgumentException();
                                    }
                                }
                            }
                            else
                                throw new ArgumentException();

                            SsaRegister[] passedParams = new SsaRegister[numParams];
                            for (int p = 0; p < numParams; p++)
                            {
                                SsaRegister paramReg = stackTracker.GetFromTop(numParams - 1 - p);
                                passedParams[p] = paramReg;
                            }

                            // Pop the parameters first so that we don't have to spill them into the caller frame.
                            // They won't actually be deadened until after the call.
                            stackTracker.Pop(numParams);

                            stackTracker.SpillStack();

                            // Emit the actual call
                            midInstrs.Add(new MidInstruction(midOpcode, codeLocation, calledMethodSpec, constraintType, returnReg, thisReg, passedParams));

                            // Emit parameter deadens (in stack order)
                            for (int p = 0; p < numParams; p++)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, passedParams[numParams - 1 - p]));

                            // Emit "this" deaden and remove it from the stack
                            if (thisReg != null)
                            {
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, thisReg));
                                if (stackTracker.Pop() != thisReg)
                                    throw new ArgumentException();
                            }

                            // Push the return value
                            if (returnReg != null)
                                stackTracker.Push(returnReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.stloc:
                        {
                            SsaRegister evalVar = stackTracker.Pop();
                            VReg localVar = locals[instr.Arguments.U32Value];
                            switch (localVar.VType.ValType)
                            {
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.ReferenceValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreReg_Value, codeLocation, localVar, evalVar));
                                    break;
                                case VType.ValTypeEnum.ManagedPtr:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreReg_ManagedPtr, codeLocation, localVar, evalVar));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, evalVar));
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldloc:
                        {
                            VReg localVar = locals[instr.Arguments.U32Value];
                            SsaRegister evalReg = stackTracker.NewReg(localVar.VType);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, evalReg));

                            switch (localVar.VType.ValType)
                            {
                                case VType.ValTypeEnum.ManagedPtr:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_ManagedPtr, codeLocation, localVar, evalReg));
                                    break;
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.ReferenceValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_Value, codeLocation, localVar, evalReg));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            stackTracker.Push(evalReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldloca:
                        {
                            VReg localVar = locals[instr.Arguments.U32Value];
                            SsaRegister evalReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ManagedPtr, localVar.VType.TypeSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, evalReg));

                            switch (localVar.VType.ValType)
                            {
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.ReferenceValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadRegA, codeLocation, localVar, evalReg));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            stackTracker.Push(evalReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.bne:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();

                            bool isRefComparison = IsComparisonReference(value1.VType, value2.VType);

                            CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();

                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)instr.Arguments.U32Value, edgeProto.OutboundTypes);
                            CfgNode fallThroughNode = cfgBuilder.AddCfgTarget(this, nextInstr, edgeProto.OutboundTypes);

                            midInstrs.Add(new MidInstruction(isRefComparison ? MidInstruction.OpcodeEnum.bne_ref : MidInstruction.OpcodeEnum.bne_val, codeLocation, value1, value2, new CfgOutboundEdge(codeLocation, targetNode, edgeProto), (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, fallThroughNode, edgeProto);

                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.beq:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();

                            CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();

                            bool isRefComparison = IsComparisonReference(value1.VType, value2.VType);

                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)instr.Arguments.U32Value, edgeProto.OutboundTypes);
                            CfgNode fallThroughNode = cfgBuilder.AddCfgTarget(this, nextInstr, edgeProto.OutboundTypes);

                            midInstrs.Add(new MidInstruction(isRefComparison ? MidInstruction.OpcodeEnum.beq_ref : MidInstruction.OpcodeEnum.beq_val, codeLocation, value1, value2, new CfgOutboundEdge(codeLocation, targetNode, edgeProto), (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, fallThroughNode, edgeProto);

                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.bge:    // [.un]
                    case CLR.CIL.HLOpcode.bgt:    // [.un]
                    case CLR.CIL.HLOpcode.ble:    // [.un]
                    case CLR.CIL.HLOpcode.blt:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();

                            CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();

                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)instr.Arguments.U32Value, edgeProto.OutboundTypes);
                            CfgNode fallThroughNode = cfgBuilder.AddCfgTarget(this, nextInstr, edgeProto.OutboundTypes);

                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), codeLocation, value1, value2, new CfgOutboundEdge(codeLocation, targetNode, edgeProto), (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, fallThroughNode, edgeProto);

                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.br:
                        {
                            CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();
                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)instr.Arguments.U32Value, edgeProto.OutboundTypes);
                            OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, targetNode, edgeProto);
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.leave:
                        {
                            uint escapePath = instr.Arguments.U32Value;
                            m_ehRegion.AddEscapePath(instr.Arguments.U32Value);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Leave, codeLocation, escapePath));

                            while (stackTracker.Depth > 0)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, stackTracker.Pop()));
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.ceq:
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();
                            SsaRegister returnValue = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, m_commonTypeLookup.Boolean));

                            bool isRefComparison = IsComparisonReference(value1.VType, value2.VType);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, returnValue));
                            midInstrs.Add(new MidInstruction(isRefComparison ? MidInstruction.OpcodeEnum.ceq_ref : MidInstruction.OpcodeEnum.ceq_numeric, codeLocation, returnValue, value1, value2, false));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            stackTracker.Push(returnValue);
                        }
                        break;
                    case CLR.CIL.HLOpcode.cgt:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();
                            SsaRegister returnValue = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, m_commonTypeLookup.Boolean));

                            // Per III.4, cgt.un is used for reference non-equality checks.
                            // For some reason there isn't a cne instruction...
                            bool isRefComparison = IsComparisonReference(value1.VType, value2.VType);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, returnValue));
                            midInstrs.Add(new MidInstruction(isRefComparison ? MidInstruction.OpcodeEnum.cne_ref : MidInstruction.OpcodeEnum.cgt, codeLocation, returnValue, value1, value2, (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            stackTracker.Push(returnValue);
                        }
                        break;
                    case CLR.CIL.HLOpcode.clt:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();
                            SsaRegister returnValue = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, m_commonTypeLookup.Boolean));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, returnValue));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), codeLocation, returnValue, value1, value2, (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            stackTracker.Push(returnValue);
                        }
                        break;

                    case CLR.CIL.HLOpcode.ldc:
                        {
                            SsaRegister resultReg;

                            switch (instr.Arguments.ArgsType)
                            {
                                case CLR.CIL.HLArguments.ArgsTypeEnum.I32:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.I32, instr.Arguments.S32Value));
                                    break;
                                case CLR.CIL.HLArguments.ArgsTypeEnum.I64:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.I64, instr.Arguments.S64Value));
                                    break;
                                case CLR.CIL.HLArguments.ArgsTypeEnum.F32:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.F32, instr.Arguments.F32Value));
                                    break;
                                case CLR.CIL.HLArguments.ArgsTypeEnum.F64:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.F64, instr.Arguments.F64Value));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.brfalse:
                    case CLR.CIL.HLOpcode.brtrue:
                        {
                            SsaRegister v = stackTracker.Pop();

                            CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();
                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)instr.Arguments.U32Value, edgeProto.OutboundTypes);
                            CfgNode fallThroughNode = cfgBuilder.AddCfgTarget(this, nextInstr, edgeProto.OutboundTypes);

                            MidInstruction.OpcodeEnum opcode;
                            switch (v.VType.ValType)
                            {
                                case VType.ValTypeEnum.ConstantValue:
                                case VType.ValTypeEnum.ValueValue:
                                    if (instr.Opcode == CLR.CIL.HLOpcode.brtrue)
                                        opcode = MidInstruction.OpcodeEnum.brnotzero;
                                    else
                                        opcode = MidInstruction.OpcodeEnum.brzero;
                                    break;
                                case VType.ValTypeEnum.ConstantReference:
                                case VType.ValTypeEnum.Null:
                                case VType.ValTypeEnum.ReferenceValue:
                                    if (instr.Opcode == CLR.CIL.HLOpcode.brtrue)
                                        opcode = MidInstruction.OpcodeEnum.brnotnull;
                                    else
                                        opcode = MidInstruction.OpcodeEnum.brnull;
                                    break;
                                default:
                                    throw new Exception("Unsupported stack op type passed to brtrue or brfalse");
                            }
                            midInstrs.Add(new MidInstruction(opcode, codeLocation, v, new CfgOutboundEdge(codeLocation, targetNode, edgeProto)));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, v));

                            OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, fallThroughNode, edgeProto);

                            isTerminalEdge = true;
                        }
                        break;

                    case CLR.CIL.HLOpcode.ldnull:
                        {
                            SsaRegister constReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.Null, m_commonTypeLookup.Object));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, constReg));
                            stackTracker.Push(constReg);
                        }
                        break;

                    case CLR.CIL.HLOpcode.@throw:
                        {
                            SsaRegister ex = stackTracker.Pop();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Throw, codeLocation, ex));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, ex));
                            isTerminalEdge = true;
                        }
                        break;

                    case CLR.CIL.HLOpcode.newarr:
                        {
                            CLRTableRow contentsType = (CLRTableRow)instr.Arguments.ObjValue;
                            CLRTypeSpec contentsTypeSpec = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(contentsType);

                            CLRTypeSpecSZArray arrayTS = new CLRTypeSpecSZArray(contentsTypeSpec);

                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ReferenceValue, arrayTS));

                            // It's OK to pop num elems here since, as an integer, we don't care if it doesn't spill
                            SsaRegister numElemsReg = stackTracker.Pop();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            stackTracker.SpillStack();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.NewSZArray, codeLocation, resultReg, numElemsReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, numElemsReg));
                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldfld:
                        {
                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, fieldValueSpec);

                            SsaRegister valueReg = stackTracker.NewReg(new VType(valType, fieldValueSpec));
                            SsaRegister objReg = stackTracker.Pop();

                            VType.ValTypeEnum objValType = objReg.VType.ValType;

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, valueReg));

                            MidInstruction.OpcodeEnum opcode;
                            if (objValType == VType.ValTypeEnum.ManagedPtr)
                                opcode = MidInstruction.OpcodeEnum.LoadField_ManagedPtr;
                            else if (objValType == VType.ValTypeEnum.ReferenceValue)
                                opcode = MidInstruction.OpcodeEnum.LoadField_Object;
                            else if (objValType == VType.ValTypeEnum.ValueValue)
                                opcode = MidInstruction.OpcodeEnum.LoadField_Value;
                            else
                                throw new ArgumentException();

                            midInstrs.Add(new MidInstruction(opcode, codeLocation, objReg, valueReg, field.Name, field.DeclaredInClassSpec));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldflda:
                        {
                            SsaRegister objReg = stackTracker.Pop();

                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = VType.ValTypeEnum.ManagedPtr;

                            bool isManagedPtr = (objReg.VType.ValType == VType.ValTypeEnum.ManagedPtr);

                            if (isManagedPtr)
                                valType = objReg.VType.ValType;

                            SsaRegister valueReg = stackTracker.NewReg(new VType(valType, fieldValueSpec));

                            VType.ValTypeEnum objValType = objReg.VType.ValType;

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, valueReg));

                            MidInstruction.OpcodeEnum opcode;
                            if (isManagedPtr)
                                opcode = MidInstruction.OpcodeEnum.LoadFieldA_ManagedPtr;
                            else if (objValType == VType.ValTypeEnum.ReferenceValue)
                                opcode = MidInstruction.OpcodeEnum.LoadFieldA_Object;
                            else
                                throw new ArgumentException();

                            midInstrs.Add(new MidInstruction(opcode, codeLocation, objReg, valueReg, field.Name, field.DeclaredInClassSpec));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldsfld:
                        {
                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, fieldValueSpec);

                            SsaRegister valueReg = stackTracker.NewReg(new VType(valType, fieldValueSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadStaticField, codeLocation, valueReg, field.DeclaredInClassSpec, field.Name));

                            stackTracker.Push(valueReg);
                        }
                        break;

                    case CLR.CIL.HLOpcode.ldsflda:
                        {
                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;

                            SsaRegister valueReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ManagedPtr, fieldValueSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadStaticFieldAddr, codeLocation, valueReg, field.DeclaredInClassSpec, field.Name));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldelem:     // [.type]
                    case CLR.CIL.HLOpcode.ldelema:
                        {
                            // We just ignore the type suffix and get it from the array instead
                            SsaRegister indexReg = stackTracker.Pop();
                            SsaRegister arrayReg = stackTracker.Pop();

                            CLRTypeSpecSZArray arraySpec = (CLRTypeSpecSZArray)arrayReg.VType.TypeSpec;
                            CLRTypeSpec contentsSpec = arraySpec.SubType;

                            VType.ValTypeEnum valType;
                            MidInstruction.OpcodeEnum op;
                            if (instr.Opcode == CLR.CIL.HLOpcode.ldelem)
                            {
                                valType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, contentsSpec);
                                op = MidInstruction.OpcodeEnum.LoadArrayElem;
                            }
                            else if (instr.Opcode == CLR.CIL.HLOpcode.ldelema)
                            {
                                valType = VType.ValTypeEnum.ManagedPtr;
                                op = MidInstruction.OpcodeEnum.LoadArrayElemAddr;
                            }
                            else
                                throw new ArgumentException();

                            SsaRegister contentsReg = stackTracker.NewReg(new VType(valType, contentsSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, contentsReg));
                            midInstrs.Add(new MidInstruction(op, codeLocation, arrayReg, indexReg, contentsReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, indexReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, arrayReg));

                            stackTracker.Push(contentsReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.stelem:     // [.type]
                        {
                            // We just ignore the type suffix and get it from the array instead
                            SsaRegister valueReg = stackTracker.Pop();
                            SsaRegister indexReg = stackTracker.Pop();
                            SsaRegister arrayReg = stackTracker.Pop();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreArrayElem, codeLocation, arrayReg, indexReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, indexReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, arrayReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.stfld:
                        {
                            SsaRegister valueReg = stackTracker.Pop();
                            SsaRegister objReg = stackTracker.Pop();

                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, fieldValueSpec);

                            VType.ValTypeEnum objValType = objReg.VType.ValType;

                            if (objValType == VType.ValTypeEnum.ManagedPtr)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreField_ManagedPtr, codeLocation, objReg, valueReg, field.Name, field.DeclaredInClassSpec, fieldValueSpec));
                            else if (objValType == VType.ValTypeEnum.ReferenceValue)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreField_Object, codeLocation, objReg, valueReg, field.Name, field.DeclaredInClassSpec, fieldValueSpec));
                            else
                                throw new ArgumentException();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, objReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.stsfld:
                        {
                            SsaRegister valueReg = stackTracker.Pop();

                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreStaticField, codeLocation, valueReg, field.DeclaredInClassSpec, fieldValueSpec, field.Name));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, valueReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldstr:
                        {
                            SsaRegister constReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantReference, m_commonTypeLookup.String, instr.Arguments.ObjValue));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, constReg));
                            stackTracker.Push(constReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.add:    // [.ovf][.un]
                    case CLR.CIL.HLOpcode.sub:    // [.ovf][.un]
                    case CLR.CIL.HLOpcode.mul:    // [.ovf][.un]
                    case CLR.CIL.HLOpcode.div:    // [.ovf][.un]
                    case CLR.CIL.HLOpcode.rem:    // [.ovf][.un]
                    case CLR.CIL.HLOpcode.and:
                    case CLR.CIL.HLOpcode.or:
                    case CLR.CIL.HLOpcode.xor:
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();

                            CLRTypeSpec promoted1 = ArithPromoteValue(value1.VType.TypeSpec);
                            CLRTypeSpec promoted2 = ArithPromoteValue(value2.VType.TypeSpec);

                            MidInstruction.ArithEnum arithMode = ArithModeForBinaryNumericOp(m_commonTypeLookup, promoted1, promoted2);
                            if ((instr.Flags & CLR.CIL.HLOpFlags.Ovf) != 0)
                                arithMode |= MidInstruction.ArithEnum.Flags_Ovf;
                            if ((instr.Flags & CLR.CIL.HLOpFlags.Un) != 0)
                                arithMode |= MidInstruction.ArithEnum.Flags_Un;

                            CLRTypeSpec resultType = TypeSpecForArithModeResult(arithMode);

                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, resultType));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), codeLocation, value1, value2, resultReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.shl:
                    case CLR.CIL.HLOpcode.shr:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();

                            CLRTypeSpec promoted1 = ArithPromoteValue(value1.VType.TypeSpec);
                            CLRTypeSpec promoted2 = ArithPromoteValue(value2.VType.TypeSpec);

                            MidInstruction.ArithEnum arithMode = ArithModeForShiftOp(m_commonTypeLookup, promoted1, promoted2);
                            if ((instr.Flags & CLR.CIL.HLOpFlags.Un) != 0)
                                arithMode |= MidInstruction.ArithEnum.Flags_Un;

                            CLRTypeSpec resultType = TypeSpecForArithModeResult(arithMode);

                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, resultType));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), codeLocation, value1, value2, resultReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, value1));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.neg:
                    case CLR.CIL.HLOpcode.not:
                        {
                            SsaRegister v = stackTracker.Pop();

                            CLRTypeSpec promoted = ArithPromoteValue(v.VType.TypeSpec);

                            MidInstruction.ArithEnum arithMode = ArithModeForUnaryOp(promoted);

                            CLRTypeSpec resultType = TypeSpecForArithModeResult(arithMode);

                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, resultType));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), codeLocation, v, resultReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, v));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.isinst:
                        {
                            CLRTypeSpec type = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister inputReg = stackTracker.Pop();
                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ReferenceValue, type));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.TryConvertObj, codeLocation, inputReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, inputReg));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.castclass:
                        {
                            CLRTypeSpec type = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister inputReg = stackTracker.Pop();
                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ReferenceValue, type));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ConvertObj, codeLocation, inputReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, inputReg));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.dup:
                        {
                            SsaRegister top = stackTracker.GetFromTop(0);
                            SsaRegister duplicate = stackTracker.NewReg(top.VType);

                            // WARNING: If you update this sequence, you must update ldvirtftn too!
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, duplicate));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.DuplicateReg, codeLocation, top, duplicate));

                            stackTracker.Push(duplicate);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldind:  // (.type)
                    case CLR.CIL.HLOpcode.ldobj:
                        {
                            SsaRegister addr = stackTracker.Pop();
                            if (addr.VType.ValType != VType.ValTypeEnum.ManagedPtr)
                                throw new ArgumentException();

                            SsaRegister val = stackTracker.NewReg(new VType(CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, addr.VType.TypeSpec), addr.VType.TypeSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadIndirect, codeLocation, addr, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, addr));

                            stackTracker.Push(val);
                        }
                        break;
                    case CLR.CIL.HLOpcode.stind:  // (.type)
                    case CLR.CIL.HLOpcode.stobj:
                        {
                            SsaRegister val = stackTracker.Pop();
                            SsaRegister addr = stackTracker.Pop();
                            if (addr.VType.ValType != VType.ValTypeEnum.ManagedPtr)
                                throw new ArgumentException();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreIndirect, codeLocation, addr, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, addr));
                        }
                        break;
                    case CLR.CIL.HLOpcode.pop:
                        {
                            SsaRegister val = stackTracker.Pop();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, val));
                        }
                        break;
                    case CLR.CIL.HLOpcode.box:
                        {
                            CLRTableRow typeTok = (CLRTableRow)instr.Arguments.ObjValue;

                            stackTracker.SpillStack();
                            SsaRegister val = stackTracker.Pop();

                            if (val.VType.ValType != VType.ValTypeEnum.ConstantValue && val.VType.ValType != VType.ValTypeEnum.ValueValue)
                                throw new ArgumentException();

                            CLRTypeSpec valueTypeSpec = val.VType.TypeSpec;
                            if (valueTypeSpec is CLRTypeSpecGenericInstantiation)
                            {
                                CLRTypeSpecGenericInstantiation valueGI = (CLRTypeSpecGenericInstantiation)valueTypeSpec;
                                CLRTypeSpecClass valueGIClass = valueGI.GenericType;
                                CLRTypeDefRow typeDef = valueGIClass.TypeDef;
                                if (typeDef.ContainerClass == null && typeDef.TypeNamespace == "System" && typeDef.TypeName == "Nullable`1")
                                    valueTypeSpec = valueGI.ArgTypes[0];
                            }

                            SsaRegister boxed = stackTracker.NewReg(new VType(VType.ValTypeEnum.ReferenceValue, val.VType.TypeSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, boxed));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Box, codeLocation, val, boxed));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, val));

                            stackTracker.Push(boxed);
                        }
                        break;
                    case CLR.CIL.HLOpcode.conv:   // [.ovf](.type)[.un]
                        {
                            CLRTypeSpec destType;
                            MidInstruction.ArithEnum arithMode;
                            switch (instr.TypeToken)
                            {
                                case CLR.CIL.HLOpType.I1:
                                    destType = m_commonTypeLookup.I8;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int32;
                                    break;
                                case CLR.CIL.HLOpType.I2:
                                    destType = m_commonTypeLookup.I16;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int32;
                                    break;
                                case CLR.CIL.HLOpType.I4:
                                    destType = m_commonTypeLookup.I32;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int32;
                                    break;
                                case CLR.CIL.HLOpType.I8:
                                    destType = m_commonTypeLookup.I64;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int64;
                                    break;
                                case CLR.CIL.HLOpType.U1:
                                    destType = m_commonTypeLookup.U8;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int32;
                                    break;
                                case CLR.CIL.HLOpType.U2:
                                    destType = m_commonTypeLookup.U16;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int32;
                                    break;
                                case CLR.CIL.HLOpType.U4:
                                    destType = m_commonTypeLookup.U32;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int32;
                                    break;
                                case CLR.CIL.HLOpType.U8:
                                    destType = m_commonTypeLookup.U64;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Int64;
                                    break;
                                case CLR.CIL.HLOpType.R4:
                                    destType = m_commonTypeLookup.F32;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Float32;
                                    break;
                                case CLR.CIL.HLOpType.R:    // Used by conv.r.un
                                case CLR.CIL.HLOpType.R8:
                                    destType = m_commonTypeLookup.F64;
                                    arithMode = MidInstruction.ArithEnum.ArithType_Float64;
                                    break;
                                case CLR.CIL.HLOpType.I:
                                    destType = m_commonTypeLookup.I;
                                    arithMode = MidInstruction.ArithEnum.ArithType_NativeInt;
                                    break;
                                case CLR.CIL.HLOpType.U:
                                    destType = m_commonTypeLookup.U;
                                    arithMode = MidInstruction.ArithEnum.ArithType_NativeInt;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            if ((instr.Flags & CLR.CIL.HLOpFlags.Ovf) != 0)
                                arithMode |= MidInstruction.ArithEnum.Flags_Ovf;
                            if ((instr.Flags & CLR.CIL.HLOpFlags.Un) != 0)
                                arithMode |= MidInstruction.ArithEnum.Flags_Un;

                            SsaRegister srcReg = stackTracker.Pop();
                            SsaRegister destReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, destType));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, destReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ConvertNumber, codeLocation, srcReg, destReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, srcReg));

                            stackTracker.Push(destReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldlen:
                        {
                            SsaRegister arrayReg = stackTracker.Pop();
                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, m_commonTypeLookup.U));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadArrayLength, codeLocation, arrayReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, arrayReg));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldtoken:
                        {
                            CLRTableRow tokenRow = (CLRTableRow)instr.Arguments.ObjValue;
                            CLRTypeSpec typeResolution = null;
                            CppMethodSpec methodResolution = null;
                            CppField fieldResolution = null;

                            if (tokenRow is CLRTypeRefRow || tokenRow is CLRTypeDefRow || tokenRow is CLRTypeSpecRow)
                                typeResolution = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(tokenRow);
                            else if (tokenRow is CLRMemberRefRow)
                            {
                                CLRMemberRefRow memberRef = (CLRMemberRefRow)tokenRow;
                                if (memberRef.FieldSig != null)
                                    fieldResolution = ResolveField(memberRef);
                                else if (memberRef.MethodSig != null)
                                    methodResolution = CppBuilder.ResolveMethodDefOrRef(memberRef);
                                else
                                    throw new ArgumentException();
                            }
                            else if (tokenRow is CLRMethodDefRow)
                                methodResolution = CppBuilder.ResolveMethodDefOrRef(tokenRow);
                            else if (tokenRow is CLRFieldRow)
                                fieldResolution = ResolveField(tokenRow);
                            else
                                throw new ArgumentException();

                            if (typeResolution != null)
                            {
                                CLRTypeSpec rtDefSpec = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(m_cppBuilder.Assemblies.RuntimeTypeHandleDef);
                                SsaRegister reg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, rtDefSpec));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, reg));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadTypeInfoHandle, codeLocation, reg, typeResolution));
                                stackTracker.Push(reg);
                            }
                            if (methodResolution != null)
                            {
                                // TODO: nameof support
                                throw new NotImplementedException();
                            }
                            if (fieldResolution != null)
                            {
                                // Necessary for nameof and static field InitializeArray
                                CLRTypeSpec containerClassSpec = fieldResolution.DeclaredInClassSpec;
                                string fieldName = fieldResolution.Name;

                                CLRTypeSpec fldDefSpec = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(m_cppBuilder.Assemblies.RuntimeFieldHandleDef);

                                SsaRegister reg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, fldDefSpec));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, reg));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadFieldInfoHandle, codeLocation, reg, containerClassSpec, fieldName, fieldResolution.Field.Static));
                                stackTracker.Push(reg);
                            }
                        }
                        break;
                    case CLR.CIL.HLOpcode.@switch:
                        {
                            SsaRegister valueReg = stackTracker.Pop();
                            List<CfgOutboundEdge> targetOutboundEdges = new List<CfgOutboundEdge>();

                            CfgOutboundEdgePrototype cfgEdgeProto = stackTracker.GenerateCfgEdge();
                            foreach (uint targetInstr in (uint[])instr.Arguments.ObjValue)
                            {
                                CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)targetInstr, cfgEdgeProto.OutboundTypes);
                                targetOutboundEdges.Add(new CfgOutboundEdge(codeLocation, targetNode, cfgEdgeProto));
                            }

                            CfgNode fallThroughNode = cfgBuilder.AddCfgTarget(this, nextInstr, cfgEdgeProto.OutboundTypes);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Switch, codeLocation, valueReg, targetOutboundEdges.ToArray()));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, valueReg));

                            OutputFallThroughEdge = new CfgOutboundEdge(codeLocation, fallThroughNode, cfgEdgeProto);
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.unbox:
                        {
                            CLRTypeSpec typeTok = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister objReg = stackTracker.Pop();
                            SsaRegister valueReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ManagedPtr, typeTok));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.UnboxPtr, codeLocation, objReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.unbox_any:
                        {
                            CLRTypeSpec typeTok = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister objReg = stackTracker.Pop();
                            SsaRegister valueReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, typeTok));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.UnboxValue, codeLocation, objReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.initobj:
                        {
                            SsaRegister objLoc = stackTracker.Pop();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ZeroFillPtr, codeLocation, objLoc));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, objLoc));
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldftn:
                        {
                            CppMethodSpec boundMethodSpec = CppBuilder.ResolveMethodDefOrRef((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister ftnReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.DelegateSimpleMethod, null, boundMethodSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, ftnReg));
                            stackTracker.Push(ftnReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldvirtftn:
                        {
                            SsaRegister throwawayObjReg = stackTracker.Pop();

                            CppMethodSpec boundVirtMethod = CppBuilder.ResolveMethodDefOrRef((CLRTableRow)instr.Arguments.ObjValue);
                            SsaRegister ftnReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.DelegateVirtualMethod, null, boundVirtMethod));
                            stackTracker.Push(ftnReg);

                            // This reverses the preceding dup sequence
                            if (cilMethod.Instructions[instrNum - 1].Opcode != CLR.CIL.HLOpcode.dup || firstInstr == instrNum)
                                throw new ArgumentException();

                            // WARNING: This must be kept in sync with dup!
                            midInstrs.RemoveRange(midInstrs.Count - 2, 2);
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, codeLocation, ftnReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.endfinally:
                        {
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ExitFinally, codeLocation));
                            while (stackTracker.Depth > 0)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, codeLocation, stackTracker.Pop()));
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.constrained_pfx:
                    case CLR.CIL.HLOpcode.readonly_pfx:
                        break;
                    case CLR.CIL.HLOpcode.jmp:
                    case CLR.CIL.HLOpcode.calli:
                    case CLR.CIL.HLOpcode.@break:
                    case CLR.CIL.HLOpcode.cpobj:
                    case CLR.CIL.HLOpcode.refanyval:
                    case CLR.CIL.HLOpcode.ckfinite:
                    case CLR.CIL.HLOpcode.mkrefany:
                    case CLR.CIL.HLOpcode.arglist:
                    case CLR.CIL.HLOpcode.localloc:
                    case CLR.CIL.HLOpcode.endfilter:
                    case CLR.CIL.HLOpcode.unaligned_pfx:
                    case CLR.CIL.HLOpcode.volatile_pfx:
                    case CLR.CIL.HLOpcode.tail_pfx:
                    case CLR.CIL.HLOpcode.cpblk:
                    case CLR.CIL.HLOpcode.initblk:
                    case CLR.CIL.HLOpcode.no_pfx:
                    case CLR.CIL.HLOpcode.rethrow:
                    case CLR.CIL.HLOpcode.@sizeof:
                    case CLR.CIL.HLOpcode.refanytype:
                        throw new NotImplementedException("Unimplemented opcode: " + instr.Opcode.ToString());
                        break;
                }
            }

            // Post-terminal-edge cleanup
            // Leak any registers alive past the terminal edge
            while (stackTracker.Depth > 0)
                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LeakReg, codeLocation, stackTracker.Pop()));

            OutputInstructions = midInstrs.ToArray();
        }
    }
}