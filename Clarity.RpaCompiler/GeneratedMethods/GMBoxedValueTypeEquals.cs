using System;
using Clarity.Rpa;
using System.Collections.Generic;

namespace Clarity.RpaCompiler.GeneratedMethods
{
    // TODO: This could be significantly sped up.
    public class GMBoxedValueTypeEquals : MethodKey
    {
        private TypeSpecBoxTag m_bt;
        private VTableGenerationCache m_vtCache;

        public GMBoxedValueTypeEquals(TypeSpecBoxTag bt, VTableGenerationCache vtCache)
        {
            m_bt = bt;
            m_vtCache = vtCache;
        }

        public override bool Equals(MethodKey other)
        {
            GMBoxedValueTypeEquals tOther = other as GMBoxedValueTypeEquals;
            if (tOther == null)
                return false;

            return m_bt == tOther.m_bt;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            VTableGenerationCache vtCache = m_vtCache;
            HighTypeDef typeDef = compiler.GetTypeDef(m_bt.ContainedType.TypeName);

            switch (typeDef.Semantics)
            {
                case TypeSemantics.Enum:
                case TypeSemantics.Struct:
                    break;
                default:
                    throw new ArgumentException("Unknown method type");
            }

            CliClass cls = compiler.GetClosedClass(m_bt.ContainedType);

            HighLocal[] locals = new HighLocal[2];
            locals[0] = new HighLocal(m_bt, HighLocal.ETypeOfType.Value);
            locals[1] = new HighLocal(vtCache.GetSystemObjectType(compiler), HighLocal.ETypeOfType.Value);

            HighCfgNodeHandle returnFalseHdl = new HighCfgNodeHandle();

            HighCfgNodeHandle entryHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle getOtherTypeHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle checkTypeHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle unboxThisHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle unboxOtherHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle nextFieldCheckHdl = new HighCfgNodeHandle();

            HighSsaRegister thisRef = new HighSsaRegister(HighValueType.ReferenceValue, vtCache.GetSystemObjectType(compiler), null);
            HighSsaRegister otherRef = new HighSsaRegister(HighValueType.ReferenceValue, vtCache.GetSystemObjectType(compiler), null);

            HighSsaRegister thisType = new HighSsaRegister(HighValueType.ReferenceValue, vtCache.GetSystemTypeType(compiler), null);
            HighSsaRegister otherType = new HighSsaRegister(HighValueType.ReferenceValue, vtCache.GetSystemTypeType(compiler), null);
            HighSsaRegister unboxedThisPtr = new HighSsaRegister(HighValueType.ManagedPtr, m_bt.ContainedType, null);
            HighSsaRegister unboxedOtherPtr = new HighSsaRegister(HighValueType.ManagedPtr, m_bt.ContainedType, null);

            TypeSpecClassTag boolType = vtCache.GetSystemBoolType(compiler);

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, thisRef, locals[0]));
                instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, otherRef, locals[1]));
                instrs.Add(new Rpa.Instructions.BranchRefNullInstruction(null, otherRef, returnFalseHdl, getOtherTypeHdl));
                entryHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                Instructions.CallRloInstanceMethodInstruction callInstr = new Instructions.CallRloInstanceMethodInstruction(null, vtCache.GetSystemObjectGetType(compiler), otherType, otherRef, new HighSsaRegister[0]);
                callInstr.ContinuationEdge = new HighCfgEdge(callInstr, checkTypeHdl);
                instrs.Add(callInstr);
                getOtherTypeHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                instrs.Add(new Rpa.Instructions.GetTypeInfoInstruction(null, thisType, m_bt.ContainedType));
                instrs.Add(new Rpa.Instructions.BranchCompareRefsInstruction(null, thisType, otherType, unboxThisHdl, returnFalseHdl));
                checkTypeHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                Rpa.Instructions.UnboxPtrInstruction unboxInstr = new Rpa.Instructions.UnboxPtrInstruction(null, unboxedThisPtr, thisRef);
                unboxInstr.ContinuationEdge = new HighCfgEdge(unboxInstr, unboxOtherHdl);
                instrs.Add(unboxInstr);
                unboxThisHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                Rpa.Instructions.UnboxPtrInstruction unboxInstr = new Rpa.Instructions.UnboxPtrInstruction(null, unboxedOtherPtr, otherRef);
                unboxInstr.ContinuationEdge = new HighCfgEdge(unboxInstr, nextFieldCheckHdl);
                instrs.Add(unboxInstr);
                unboxOtherHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
            }

            if ((vtCache.GetClassPodFlags(compiler, cls) & VTableGenerationCache.PodFlags.Equality) != VTableGenerationCache.PodFlags.None)
            {
                HighCfgNodeHandle nextHdl = new HighCfgNodeHandle();
                List<HighInstruction> instrs = new List<HighInstruction>();
                instrs.Add(new Instructions.BranchComparePodInstruction(null, unboxedThisPtr, unboxedOtherPtr, nextHdl, returnFalseHdl));
                nextFieldCheckHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());

                nextFieldCheckHdl = nextHdl;
            }
            else
            {
                foreach (HighField fld in cls.InstanceFields)
                {
                    HighCfgNodeHandle nextHdl = new HighCfgNodeHandle();

                    TypeSpecTag fldType = fld.Type;
                    HighSsaRegister thisFldPtr = new HighSsaRegister(HighValueType.ManagedPtr, fldType, null);
                    HighSsaRegister otherFldPtr = new HighSsaRegister(HighValueType.ManagedPtr, fldType, null);

                    {
                        List<HighInstruction> instrs = new List<HighInstruction>();
                        instrs.Add(new Rpa.Instructions.PtrFieldInstruction(null, thisFldPtr, unboxedThisPtr, fld.Name));
                        instrs.Add(new Rpa.Instructions.PtrFieldInstruction(null, otherFldPtr, unboxedOtherPtr, fld.Name));

                        if (compiler.TypeIsValueType(fldType) &&
                            (((m_vtCache.GetClassPodFlags(compiler, compiler.GetClosedClass((TypeSpecClassTag)fldType))) & VTableGenerationCache.PodFlags.Equality) != VTableGenerationCache.PodFlags.None))
                        {
                            instrs.Add(new Instructions.BranchComparePodInstruction(null, thisFldPtr, otherFldPtr, nextHdl, returnFalseHdl));
                        }
                        else
                        {
                            HighCfgNodeHandle checkResultHdl = new HighCfgNodeHandle();
                            HighSsaRegister result = new HighSsaRegister(HighValueType.ValueValue, vtCache.GetSystemBoolType(compiler), null);
                            HighSsaRegister resultInt = new HighSsaRegister(HighValueType.ValueValue, vtCache.GetSystemBoolType(compiler), null);
                            HighSsaRegister zeroConstant = new HighSsaRegister(HighValueType.ConstantValue, vtCache.GetSystemInt32Type(compiler), 0);

                            TypeSpecClassTag clarityToolsClass = vtCache.GetClarityToolsType(compiler);
                            MethodDeclTag compareFieldsDeclTag = vtCache.GetCompareFieldsDeclTag(compiler);
                            MethodSpecTag compareMethodSpec = new MethodSpecTag(MethodSlotType.Static, new TypeSpecTag[] { fldType }, clarityToolsClass, compareFieldsDeclTag);

                            MethodHandle compareHdl = compiler.InstantiateMethod(new MethodSpecMethodKey(compareMethodSpec), instantiationPath);
                            Instructions.CallRloStaticMethodInstruction callInstr = new Instructions.CallRloStaticMethodInstruction(null, compareHdl, result, new HighSsaRegister[] { thisFldPtr, otherFldPtr });

                            callInstr.ContinuationEdge = new HighCfgEdge(callInstr, checkResultHdl);
                            instrs.Add(callInstr);

                            List<HighInstruction> checkResultsInstrs = new List<HighInstruction>();
                            checkResultsInstrs.Add(new Instructions.RloConvertNumberInstruction(null, resultInt, result, Instructions.RloConvertNumberInstruction.NumConversionType.ZeroExtend, 32, 8));
                            checkResultsInstrs.Add(new Rpa.Instructions.BranchCompareNumbersInstruction(null, Rpa.Instructions.NumberCompareOperation.Equal, Rpa.Instructions.NumberArithType.Int32, resultInt, zeroConstant, returnFalseHdl, nextHdl));
                            checkResultHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], checkResultsInstrs.ToArray());
                        }

                        nextFieldCheckHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
                    }

                    nextFieldCheckHdl = nextHdl;
                }

                // Generate terminators
                {
                    HighSsaRegister falseConstant = new HighSsaRegister(HighValueType.ConstantValue, boolType, false);
                    List<HighInstruction> instrs = new List<HighInstruction>();
                    instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, falseConstant));
                    returnFalseHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
                }

                {
                    HighSsaRegister trueConstant = new HighSsaRegister(HighValueType.ConstantValue, boolType, false);
                    List<HighInstruction> instrs = new List<HighInstruction>();
                    instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, trueConstant));
                    nextFieldCheckHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
                }
            }

            HighRegion region = new HighRegion(entryHdl);
            RloMethodBody body = new RloMethodBody(locals, boolType, region, instantiationPath);
            return new RloMethod(body);
        }

        public override int GetHashCode()
        {
            return m_bt.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gm_boxedvaluetypeequals(");
            m_bt.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
