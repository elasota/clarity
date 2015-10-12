using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

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

        public CfgNodeCompiler(CfgNode cfgNode)
        {
            m_cppBuilder = cfgNode.CfgBuilder.CppBuilder;
            m_commonTypeLookup = new CommonTypeLookup(m_cppBuilder);
            m_cfgNode = cfgNode;
            m_ehRegion = cfgNode.CfgBuilder.Region;
            m_escapePaths = new SortedSet<uint>();
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

                CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_cppBuilder.Assemblies, memberRef.MethodSig);

                foreach (CppField field in cachedClass.Fields)
                {
                    if (field.Name == memberRef.Name && field.Type.Equals(fieldType))
                        return field;
                }
                throw new ParseFailedException("Unresolved field reference");
            }
            throw new ArgumentException();
        }

        private CppMethodSpec ResolveMethod(CLRTableRow tableRow)
        {
            if (tableRow is CLRMethodDefRow)
            {
                CLRMethodDefRow methodDef = (CLRMethodDefRow)tableRow;

                return new CppMethodSpec(new CppMethod(m_cppBuilder.Assemblies, methodDef.Owner, methodDef));
            }
            if (tableRow is CLRMemberRefRow)
            {
                CLRMemberRefRow memberRef = (CLRMemberRefRow)tableRow;
                CLRTypeSpec declaredIn = m_cppBuilder.ResolveTypeDefOrRefOrSpec(memberRef.Class);

                if (declaredIn is CLRTypeSpecComplexArray)
                    throw new NotImplementedException();

                CppClass cachedClass = m_cppBuilder.GetCachedClass(declaredIn);

                CLRMethodSignatureInstance sig = new CLRMethodSignatureInstance(m_cppBuilder.Assemblies, memberRef.MethodSig);

                foreach (CppMethod method in cachedClass.Methods)
                {
                    if (method.Name == memberRef.Name && method.DeclaredMethodSignature.Equals(sig))
                        return new CppMethodSpec(method);
                }
                throw new ParseFailedException("Unresolved method reference");
            }
            if (tableRow is CLRMethodSpecRow)
            {
                CLRMethodSpecRow methodSpec = (CLRMethodSpecRow)tableRow;
                CppMethod method = ResolveMethod(methodSpec.Method).CppMethod;
                List<CLRTypeSpec> types = new List<CLRTypeSpec>();
                foreach (CLRSigType type in methodSpec.Instantiation.Types)
                    types.Add(m_cppBuilder.Assemblies.InternVagueType(type));
                return new CppMethodSpec(method, types.ToArray());
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
            switch (arithMode & MidInstruction.ArithEnum.ArithType_Mask)
            {
                case MidInstruction.ArithEnum.ArithType_Float32:
                    return m_commonTypeLookup.F32;
                case MidInstruction.ArithEnum.ArithType_Float64:
                    return m_commonTypeLookup.F64;
                case MidInstruction.ArithEnum.ArithType_Int32:
                    return m_commonTypeLookup.I32;
                case MidInstruction.ArithEnum.ArithType_Int64:
                    return m_commonTypeLookup.I64;
                case MidInstruction.ArithEnum.ArithType_NativeInt:
                    return m_commonTypeLookup.I;
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
                case CLR.CIL.HLOpcode.beq:
                    return MidInstruction.OpcodeEnum.beq;
                case CLR.CIL.HLOpcode.bne:
                    return MidInstruction.OpcodeEnum.bne;
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
                case CLR.CIL.HLOpcode.cgt:
                    return MidInstruction.OpcodeEnum.cgt;
                case CLR.CIL.HLOpcode.ceq:
                    return MidInstruction.OpcodeEnum.ceq;
                case CLR.CIL.HLOpcode.brtrue:
                    return MidInstruction.OpcodeEnum.brtrue;
                case CLR.CIL.HLOpcode.brfalse:
                    return MidInstruction.OpcodeEnum.brfalse;
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

            foreach (VType entryType in m_cfgNode.EntryTypes)
            {
                SsaRegister reg = stackTracker.NewReg(entryType);
                reg.TrySpill();
                stackTracker.Push(reg);

                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.EntryReg, reg));
            }

            int firstInstr = m_cfgNode.StartInstr;
            int nextInstr = firstInstr;

            bool isTerminalEdge = false;
            while (!isTerminalEdge)
            {
                int instrNum = nextInstr++;

                if (instrNum != firstInstr && cfgBuilder.InstrIsJumpTarget(instrNum))
                {
                    CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();
                    OutputFallThroughEdge = new CfgOutboundEdge(cfgBuilder.AddCfgTarget(this, instrNum, edgeProto.OutboundTypes), edgeProto);
                    break;
                }

                if (cfgBuilder.EhClusters.ContainsKey((uint)instrNum))
                {
                    if (stackTracker.Depth != 0)
                        throw new ParseFailedException("Stack not empty at protected block entry point");
                    ExceptionHandlingCluster cluster = cfgBuilder.EhClusters[(uint)instrNum];
                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.EnterProtectedBlock, cluster));

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
                            CppMethodSpec ctorMethodSpec = ResolveMethod((CLRTableRow)instr.Arguments.ObjValue);

                            if (ctorMethodSpec.GenericParameters != null)
                                throw new ArgumentException();

                            CppMethod ctorMethod = ctorMethodSpec.CppMethod;

                            int numParams = ctorMethod.MethodSignature.ParamTypes.Length;

                            CLRTypeSpec instanceSpec = ctorMethod.DeclaredInClassSpec;

                            VType.ValTypeEnum resultValType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, instanceSpec);
                            if (resultValType == VType.ValTypeEnum.NullableReferenceValue)
                                resultValType = VType.ValTypeEnum.NotNullReferenceValue;
                            SsaRegister instanceReg = stackTracker.NewReg(new VType(resultValType, instanceSpec));

                            // Allocate the instance into an SSA reg
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, instanceReg));

                            stackTracker.SpillStack();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.AllocObject, instanceReg, instanceSpec));

                            // Determine the parameter set.
                            // Unlike when making calls, we don't spill here because we already spilled during the AllocObject call
                            SsaRegister[] passedParams = new SsaRegister[numParams];
                            for (int p = 0; p < numParams; p++)
                            {
                                SsaRegister paramReg = stackTracker.GetFromTop(numParams - 1 - p);
                                passedParams[p] = paramReg;
                            }

                            // Make the actual ctor call
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.CallConstructor, ctorMethodSpec, instanceReg, passedParams));

                            // Kill all of the parameter registers
                            for (int p = 0; p < numParams; p++)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, stackTracker.Pop()));

                            // Push the return value on to the stack
                            stackTracker.Push(instanceReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ret:
                        if (stackTracker.Depth == 1)
                        {
                            SsaRegister returnValue = stackTracker.Pop();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ReturnValue, returnValue));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, returnValue));
                        }
                        else if (stackTracker.Depth == 0)
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Return));
                        else
                            throw new ArgumentException();

                        isTerminalEdge = true;
                        break;
                    case CLR.CIL.HLOpcode.ldarg:
                        {
                            VReg argReg = args[instr.Arguments.U32Value];

                            VType evalType = argReg.VType;
                            SsaRegister evalReg = stackTracker.NewReg(evalType);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, evalReg));

                            string loadSrc = argReg.SlotName;
                            switch (argReg.VType.ValType)
                            {
                                case VType.ValTypeEnum.ManagedPtr:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_ManagedPtr, argReg, evalReg));
                                    break;
                                case VType.ValTypeEnum.NullableReferenceValue:
                                case VType.ValTypeEnum.NotNullReferenceValue:
                                case VType.ValTypeEnum.ValueValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_Value, argReg, evalReg));
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
                                case VType.ValTypeEnum.NullableReferenceValue:
                                //case VType.ValTypeEnum.NotNullReferenceValue: // ldarga on "this" isn't allowed
                                case VType.ValTypeEnum.ValueValue:
                                    opcode = MidInstruction.OpcodeEnum.LoadArgA_Value;
                                    outEvalType = new VType(VType.ValTypeEnum.ManagedPtr, argReg.VType.TypeSpec);
                                    break;
                                case VType.ValTypeEnum.ManagedPtr:
                                default:
                                    throw new ArgumentException();
                            }

                            SsaRegister evalReg = stackTracker.NewReg(outEvalType);
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, evalReg));
                            midInstrs.Add(new MidInstruction(opcode, argReg, evalReg));

                            stackTracker.Push(evalReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.starg:
                        {
                            VReg argReg = args[instr.Arguments.U32Value];
                            SsaRegister valueReg = stackTracker.Pop();

                            switch (valueReg.VType.ValType)
                            {
                                //case VType.ValTypeEnum.ManagedPtr:
                                case VType.ValTypeEnum.NullableReferenceValue:
                                case VType.ValTypeEnum.NotNullReferenceValue:
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.ConstantReference:
                                case VType.ValTypeEnum.ConstantValue:
                                case VType.ValTypeEnum.Null:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreReg_Value, argReg, valueReg));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, valueReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.call:
                    case CLR.CIL.HLOpcode.callvirt:
                        {
                            CppMethodSpec calledMethodSpec = ResolveMethod((CLRTableRow)instr.Arguments.ObjValue);
                            CppMethod calledMethod = calledMethodSpec.CppMethod;

                            int numParams = calledMethod.MethodSignature.ParamTypes.Length;

                            SsaRegister returnReg = null;
                            SsaRegister thisReg = null;

                            if (!CppBuilder.TypeSpecIsVoid(calledMethod.MethodSignature.RetType))
                            {
                                CLRTypeSpec retType = calledMethod.MethodSignature.RetType;
                                returnReg = stackTracker.NewReg(new VType(CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, retType), retType));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, returnReg));
                            }

                            if (!calledMethod.Static)
                                thisReg = stackTracker.GetFromTop(numParams);

                            MidInstruction.OpcodeEnum midOpcode;
                            if (instr.Opcode == CLR.CIL.HLOpcode.call)
                                midOpcode = MidInstruction.OpcodeEnum.CallMethod;
                            else if (instr.Opcode == CLR.CIL.HLOpcode.callvirt)
                                midOpcode = MidInstruction.OpcodeEnum.CallVirtualMethod;
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
                            midInstrs.Add(new MidInstruction(midOpcode, calledMethodSpec, returnReg, thisReg, passedParams));

                            // Emit parameter deadens (in stack order)
                            for (int p = 0; p < numParams; p++)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, passedParams[numParams - 1 - p]));

                            // Emit "this" deaden and remove it from the stack
                            if (thisReg != null)
                            {
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, thisReg));
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
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Store, localVar, evalVar));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, evalVar));
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldloc:
                        {
                            VReg localVar = locals[instr.Arguments.U32Value];
                            SsaRegister evalReg = stackTracker.NewReg(localVar.VType);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, evalReg));

                            switch (localVar.VType.ValType)
                            {
                                case VType.ValTypeEnum.ManagedPtr:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_ManagedPtr, localVar, evalReg));
                                    break;
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.NullableReferenceValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadReg_Value, localVar, evalReg));
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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, evalReg));

                            switch (localVar.VType.ValType)
                            {
                                case VType.ValTypeEnum.ValueValue:
                                case VType.ValTypeEnum.NullableReferenceValue:
                                    midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadRegA, localVar, evalReg));
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            stackTracker.Push(evalReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.beq:    // [.un]
                    case CLR.CIL.HLOpcode.bne:    // [.un]
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

                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), value1, value2, new CfgOutboundEdge(targetNode, edgeProto), (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value1));

                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.br:
                        {
                            CfgOutboundEdgePrototype edgeProto = stackTracker.GenerateCfgEdge();
                            CfgNode targetNode = cfgBuilder.AddCfgTarget(this, (int)instr.Arguments.U32Value, edgeProto.OutboundTypes);
                            OutputFallThroughEdge = new CfgOutboundEdge(targetNode, edgeProto);
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.leave:
                        {
                            uint escapePath = instr.Arguments.U32Value;
                            m_ehRegion.AddEscapePath(instr.Arguments.U32Value);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Leave, escapePath));

                            while (stackTracker.Depth > 0)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, stackTracker.Pop()));
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.ceq:
                    case CLR.CIL.HLOpcode.cgt:    // [.un]
                    case CLR.CIL.HLOpcode.clt:    // [.un]
                        {
                            SsaRegister value2 = stackTracker.Pop();
                            SsaRegister value1 = stackTracker.Pop();
                            SsaRegister returnValue = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, m_commonTypeLookup.I32));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, returnValue));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), returnValue, value1, value2, (instr.Flags & CLR.CIL.HLOpFlags.Un) != 0));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value1));

                            stackTracker.Push(returnValue);
                        }
                        break;

                    case CLR.CIL.HLOpcode.ldc:
                        {
                            SsaRegister resultReg;

                            switch (instr.Arguments.ArgsType)
                            {
                                case CLR.CIL.HLArguments.ArgsTypeEnum.I32:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.I32, instr.Arguments.S32Value), instr.Arguments.S32Value);
                                    break;
                                case CLR.CIL.HLArguments.ArgsTypeEnum.I64:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.I64, instr.Arguments.S64Value), instr.Arguments.S64Value);
                                    break;
                                case CLR.CIL.HLArguments.ArgsTypeEnum.F32:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.F32, instr.Arguments.F32Value), instr.Arguments.F32Value);
                                    break;
                                case CLR.CIL.HLArguments.ArgsTypeEnum.F64:
                                    resultReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantValue, m_commonTypeLookup.F64, instr.Arguments.F64Value), instr.Arguments.F64Value);
                                    break;
                                default:
                                    throw new ArgumentException();
                            }

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
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

                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), v, new CfgOutboundEdge(targetNode, edgeProto)));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, v));

                            OutputFallThroughEdge = new CfgOutboundEdge(fallThroughNode, edgeProto);

                            isTerminalEdge = true;
                        }
                        break;

                    case CLR.CIL.HLOpcode.ldnull:
                        {
                            SsaRegister constReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.Null, m_commonTypeLookup.Object), null);
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, constReg));
                            stackTracker.Push(constReg);
                        }
                        break;

                    case CLR.CIL.HLOpcode.@throw:
                        {
                            SsaRegister ex = stackTracker.Pop();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Throw, ex));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, ex));
                            isTerminalEdge = true;
                        }
                        break;

                    case CLR.CIL.HLOpcode.newarr:
                        {
                            CLRTableRow contentsType = (CLRTableRow)instr.Arguments.ObjValue;
                            CLRTypeSpec contentsTypeSpec = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(contentsType);

                            CLRTypeSpecSZArray arrayTS = new CLRTypeSpecSZArray(contentsTypeSpec);

                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.NotNullReferenceValue, arrayTS));

                            // It's OK to pop num elems here since, as an integer, we don't care if it doesn't spill
                            SsaRegister numElemsReg = stackTracker.Pop();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            stackTracker.SpillStack();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.NewSZArray, resultReg, numElemsReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, numElemsReg));
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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, valueReg));

                            MidInstruction.OpcodeEnum opcode;
                            if (objValType == VType.ValTypeEnum.ManagedPtr)
                                opcode = MidInstruction.OpcodeEnum.LoadField_ManagedPtr;
                            else if (objValType == VType.ValTypeEnum.NotNullReferenceValue || objValType == VType.ValTypeEnum.NullableReferenceValue)
                                opcode = MidInstruction.OpcodeEnum.LoadField_Object;
                            else
                                throw new ArgumentException();

                            midInstrs.Add(new MidInstruction(opcode, objReg, valueReg, field.Name));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldflda:
                        {
                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = VType.ValTypeEnum.ManagedPtr;

                            SsaRegister valueReg = stackTracker.NewReg(new VType(valType, fieldValueSpec));
                            SsaRegister objReg = stackTracker.Pop();

                            VType.ValTypeEnum objValType = objReg.VType.ValType;

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, valueReg));

                            MidInstruction.OpcodeEnum opcode;
                            if (objValType == VType.ValTypeEnum.ManagedPtr)
                                opcode = MidInstruction.OpcodeEnum.LoadFieldA_ManagedPtr;
                            else if (objValType == VType.ValTypeEnum.NotNullReferenceValue || objValType == VType.ValTypeEnum.NullableReferenceValue)
                                opcode = MidInstruction.OpcodeEnum.LoadFieldA_Object;
                            else
                                throw new ArgumentException();

                            midInstrs.Add(new MidInstruction(opcode, objReg, valueReg, field.Name));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldsfld:
                        {
                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, fieldValueSpec);

                            SsaRegister valueReg = stackTracker.NewReg(new VType(valType, fieldValueSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadStaticField, valueReg, field.DeclaredInClassSpec, field.Name));

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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, contentsReg));
                            midInstrs.Add(new MidInstruction(op, arrayReg, indexReg, contentsReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, indexReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, arrayReg));

                            stackTracker.Push(contentsReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.stelem:     // [.type]
                        {
                            // We just ignore the type suffix and get it from the array instead
                            SsaRegister valueReg = stackTracker.Pop();
                            SsaRegister indexReg = stackTracker.Pop();
                            SsaRegister arrayReg = stackTracker.Pop();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreArrayElem, arrayReg, indexReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, indexReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, arrayReg));
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
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreField_ManagedPtr, objReg, valueReg, field.Name));
                            else if (objValType == VType.ValTypeEnum.NotNullReferenceValue || objValType == VType.ValTypeEnum.NullableReferenceValue)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadField_Object, objReg, valueReg, field.Name));
                            else
                                throw new ArgumentException();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, objReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.stsfld:
                        {
                            SsaRegister valueReg = stackTracker.Pop();

                            CppField field = ResolveField((CLRTableRow)instr.Arguments.ObjValue);
                            CLRTypeSpec fieldValueSpec = field.Type;
                            VType.ValTypeEnum valType = CppCilExporter.ValTypeForTypeSpec(m_cppBuilder, fieldValueSpec);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreStaticField, valueReg, field.DeclaredInClassSpec, field.Name));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, valueReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldstr:
                        {
                            SsaRegister constReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.ConstantReference, m_commonTypeLookup.String, instr.Arguments.ObjValue), instr.Arguments.ObjValue);
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, constReg));
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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), value1, value2, resultReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value1));

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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), value1, value2, resultReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value2));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, value1));

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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            midInstrs.Add(new MidInstruction(SimpleTranslateInstr(instr.Opcode), v, resultReg, arithMode));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, v));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.isinst:
                        {
                            CLRTypeSpec type = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister inputReg = stackTracker.Pop();
                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.NullableReferenceValue, type));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.TryConvertObj, inputReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, inputReg));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.castclass:
                        {
                            CLRTypeSpec type = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister inputReg = stackTracker.Pop();
                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.NotNullReferenceValue, type));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ConvertObj, inputReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, inputReg));

                            stackTracker.Push(resultReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.dup:
                        {
                            SsaRegister top = stackTracker.GetFromTop(0);
                            SsaRegister duplicate = stackTracker.NewReg(top.VType);

                            // WARNING: If you update this sequence, you must update ldvirtftn too!
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, duplicate));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.DuplicateReg, top, duplicate));

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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadIndirect, addr, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, addr));

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

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.StoreIndirect, addr, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, val));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, addr));
                        }
                        break;
                    case CLR.CIL.HLOpcode.pop:
                        {
                            SsaRegister val = stackTracker.Pop();
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, val));
                        }
                        break;
                    case CLR.CIL.HLOpcode.box:
                        {
                            CLRTableRow typeTok = (CLRTableRow)instr.Arguments.ObjValue;

                            stackTracker.SpillStack();
                            SsaRegister val = stackTracker.Pop();

                            if (val.VType.ValType != VType.ValTypeEnum.ConstantValue && val.VType.ValType != VType.ValTypeEnum.ValueValue)
                                throw new ArgumentException();

                            SsaRegister boxed = stackTracker.NewReg(new VType(VType.ValTypeEnum.NotNullReferenceValue, val.VType.TypeSpec));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, boxed));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Box, val, boxed));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, val));

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
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, destReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ConvertNumber, srcReg, destReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, srcReg));

                            stackTracker.Push(destReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldlen:
                        {
                            SsaRegister arrayReg = stackTracker.Pop();
                            SsaRegister resultReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, m_commonTypeLookup.I32));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadArrayLength, arrayReg, resultReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, arrayReg));

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
                                    methodResolution = ResolveMethod(memberRef);
                                else
                                    throw new ArgumentException();
                            }
                            else if (tokenRow is CLRMethodDefRow)
                                methodResolution = ResolveMethod(tokenRow);
                            else if (tokenRow is CLRFieldRow)
                                fieldResolution = ResolveField(tokenRow);
                            else
                                throw new ArgumentException();

                            if (typeResolution != null)
                            {
                                CLRTypeSpec rtDefSpec = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec(m_cppBuilder.Assemblies.RuntimeTypeHandleDef);
                                SsaRegister reg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ValueValue, rtDefSpec));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, reg));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadTypeInfoHandle, reg, typeResolution));
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
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, reg));
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LoadFieldInfoHandle, reg, containerClassSpec, fieldName));
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
                                targetOutboundEdges.Add(new CfgOutboundEdge(targetNode, cfgEdgeProto));
                            }

                            CfgNode fallThroughNode = cfgBuilder.AddCfgTarget(this, nextInstr, cfgEdgeProto.OutboundTypes);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.Switch, valueReg, targetOutboundEdges.ToArray()));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, valueReg));

                            OutputFallThroughEdge = new CfgOutboundEdge(fallThroughNode, cfgEdgeProto);
                            isTerminalEdge = true;
                        }
                        break;
                    case CLR.CIL.HLOpcode.unbox:
                        {
                            CLRTypeSpec typeTok = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister objReg = stackTracker.Pop();
                            SsaRegister valueReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ManagedPtr, typeTok));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.UnboxPtr, objReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.unbox_any:
                        {
                            CLRTypeSpec typeTok = m_cppBuilder.Assemblies.InternTypeDefOrRefOrSpec((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister objReg = stackTracker.Pop();
                            SsaRegister valueReg = stackTracker.NewReg(new VType(VType.ValTypeEnum.ManagedPtr, typeTok));

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.UnboxValue, objReg, valueReg));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, objReg));

                            stackTracker.Push(valueReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.initobj:
                        {
                            SsaRegister objLoc = stackTracker.Pop();

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ZeroFillPtr, objLoc));
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, objLoc));
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldftn:
                        {
                            CppMethodSpec boundMethodSpec = ResolveMethod((CLRTableRow)instr.Arguments.ObjValue);

                            SsaRegister ftnReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.DelegateSimpleMethod, null, boundMethodSpec), boundMethodSpec);

                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, ftnReg));
                            stackTracker.Push(ftnReg);
                        }
                        break;
                    case CLR.CIL.HLOpcode.ldvirtftn:
                        {
                            SsaRegister throwawayObjReg = stackTracker.Pop();

                            CppMethodSpec boundVirtMethod = ResolveMethod((CLRTableRow)instr.Arguments.ObjValue);
                            SsaRegister ftnReg = SsaRegister.Constant(new VType(VType.ValTypeEnum.DelegateVirtualMethod, null, boundVirtMethod), boundVirtMethod);
                            stackTracker.Push(ftnReg);

                            // This reverses the preceding dup sequence
                            if (cilMethod.Instructions[instrNum - 1].Opcode != CLR.CIL.HLOpcode.dup || firstInstr == instrNum)
                                throw new ArgumentException();

                            // WARNING: This must be kept in sync with dup!
                            midInstrs.RemoveRange(midInstrs.Count - 2, 2);
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LivenReg, ftnReg));
                        }
                        break;
                    case CLR.CIL.HLOpcode.endfinally:
                        {
                            midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.ExitFinally));
                            while (stackTracker.Depth > 0)
                                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.KillReg, stackTracker.Pop()));
                        }
                        break;
                    case CLR.CIL.HLOpcode.jmp:
                    case CLR.CIL.HLOpcode.calli:
                    case CLR.CIL.HLOpcode.@break:
                    case CLR.CIL.HLOpcode.cpobj:
                    case CLR.CIL.HLOpcode.ldsflda:
                    case CLR.CIL.HLOpcode.refanyval:
                    case CLR.CIL.HLOpcode.ckfinite:
                    case CLR.CIL.HLOpcode.mkrefany:
                    case CLR.CIL.HLOpcode.arglist:
                    case CLR.CIL.HLOpcode.localloc:
                    case CLR.CIL.HLOpcode.endfilter:
                    case CLR.CIL.HLOpcode.unaligned_pfx:
                    case CLR.CIL.HLOpcode.volatile_pfx:
                    case CLR.CIL.HLOpcode.tail_pfx:
                    case CLR.CIL.HLOpcode.constrained_pfx:
                    case CLR.CIL.HLOpcode.cpblk:
                    case CLR.CIL.HLOpcode.initblk:
                    case CLR.CIL.HLOpcode.no_pfx:
                    case CLR.CIL.HLOpcode.rethrow:
                    case CLR.CIL.HLOpcode.@sizeof:
                    case CLR.CIL.HLOpcode.refanytype:
                    case CLR.CIL.HLOpcode.readonly_pfx:
                        throw new NotImplementedException("Unimplemented opcode: " + instr.Opcode.ToString());
                        break;
                }
            }

            // Post-terminal-edge cleanup
            // Leak any registers alive past the terminal edge
            while (stackTracker.Depth > 0)
                midInstrs.Add(new MidInstruction(MidInstruction.OpcodeEnum.LeakReg, stackTracker.Pop()));

            OutputInstructions = midInstrs.ToArray();
        }
    }
}