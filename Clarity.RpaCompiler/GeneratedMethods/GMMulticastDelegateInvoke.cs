using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.GeneratedMethods
{
    public class GMMulticastDelegateInvoke : MethodKey
    {
        private TypeSpecClassTag m_dt;
        private VTableGenerationCache m_vtCache;

        public GMMulticastDelegateInvoke(TypeSpecClassTag dt, VTableGenerationCache vtCache)
        {
            m_dt = dt;
            m_vtCache = vtCache;
        }

        public override bool Equals(MethodKey other)
        {
            GMMulticastDelegateInvoke tOther = other as GMMulticastDelegateInvoke;
            if (tOther == null)
                return false;

            return m_dt == tOther.m_dt;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            TypeSpecClassTag delegateClassType = m_dt;

            CliClass delegateCls = compiler.GetClosedClass(delegateClassType);

            TypeNameTag delegateTypeName = delegateCls.TypeName;
            HighTypeDef delegateTypeDef = compiler.GetTypeDef(delegateTypeName);
            if (delegateTypeDef.Semantics != TypeSemantics.Delegate)
                throw new RpaCompileException("Delegate-bound class is not a delegate");

            MethodSignatureTag declSignature = delegateTypeDef.DelegateSignature;

            MethodDeclTag invokeDeclTag = new MethodDeclTag("Invoke", declSignature, delegateTypeName);
            invokeDeclTag = compiler.TagRepository.InternMethodDeclTag(invokeDeclTag);

            uint vtableSlotIndex = delegateCls.DeclTagToVTableSlot[invokeDeclTag];
            CliVtableSlot vtableSlot = delegateCls.VTable[vtableSlotIndex];

            MethodSignatureTag delegateMethodSignature = vtableSlot.MethodSignature;

            TypeSpecMulticastDelegateTag mdType = new TypeSpecMulticastDelegateTag(m_dt);
            mdType = (TypeSpecMulticastDelegateTag)compiler.TagRepository.InternTypeSpec(mdType);

            List<HighLocal> locals = new List<HighLocal>();
            List<HighSsaRegister> loadedParams = new List<HighSsaRegister>();

            HighLocal thisLocal = new HighLocal(mdType, HighLocal.ETypeOfType.Value);
            locals.Add(thisLocal);

            HighSsaRegister thisReg = new HighSsaRegister(HighValueType.ReferenceValue, mdType, null);

            List<HighInstruction> entryInstrs = new List<HighInstruction>();
            entryInstrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, thisReg, thisLocal));

            foreach (MethodSignatureParam param in delegateMethodSignature.ParamTypes)
            {
                switch (param.TypeOfType.Value)
                {
                    case MethodSignatureParamTypeOfType.Values.ByRef:
                        {
                            HighSsaRegister paramSsa = new HighSsaRegister(HighValueType.ManagedPtr, param.Type, null);
                            HighLocal local = new HighLocal(param.Type, HighLocal.ETypeOfType.ByRef);
                            locals.Add(local);
                            entryInstrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, paramSsa, local));
                            loadedParams.Add(paramSsa);
                        }
                        break;
                    case MethodSignatureParamTypeOfType.Values.Value:
                        {
                            bool isValueType = compiler.TypeIsValueType(param.Type);
                            HighSsaRegister paramSsa = new HighSsaRegister(isValueType ? HighValueType.ValueValue : HighValueType.ReferenceValue, param.Type, null);
                            HighLocal local = new HighLocal(param.Type, HighLocal.ETypeOfType.Value);
                            locals.Add(local);
                            entryInstrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, paramSsa, local));
                            loadedParams.Add(paramSsa);
                        }
                        break;
                    case MethodSignatureParamTypeOfType.Values.TypedByRef:
                        throw new NotImplementedException();
                    default:
                        throw new Exception();
                }
            }

            HighSsaRegister returnValue = null;
            if (!(delegateMethodSignature.RetType is TypeSpecVoidTag))
            {
                bool isValueType = compiler.TypeIsValueType(delegateMethodSignature.RetType);
                returnValue = new HighSsaRegister(isValueType ? HighValueType.ValueValue : HighValueType.ReferenceValue, delegateMethodSignature.RetType, null);
            }

            TypeSpecClassTag nativeUIntType = m_vtCache.GetSystemUIntPtrType(compiler);
            HighSsaRegister numDelegatesValue = new HighSsaRegister(HighValueType.ValueValue, nativeUIntType, null);
            HighSsaRegister zeroNativeUIntConstant = new HighSsaRegister(HighValueType.ConstantValue, nativeUIntType, (ulong)0);

            HighCfgNodeHandle entryHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle loopCallHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle loopIncrementHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle returnHdl = new HighCfgNodeHandle();

            HighSsaRegister postIncrementReg = new HighSsaRegister(HighValueType.ValueValue, nativeUIntType, null);

            entryInstrs.Add(new Instructions.GetMulticastDelegateInvocationCountInstruction(null, numDelegatesValue, thisReg));
            entryInstrs.Add(new Rpa.Instructions.BranchInstruction(null, loopCallHdl));

            entryHdl.Value = new HighCfgNode(entryInstrs.ToArray());

            HighSsaRegister currentCountReg = new HighSsaRegister(HighValueType.ValueValue, nativeUIntType, null);

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                HighSsaRegister delegateValue = new HighSsaRegister(HighValueType.ReferenceValue, m_dt, null);

                HighInstruction callInstr = new Instructions.CallRloVirtualMethodInstruction(null, vtableSlotIndex, returnValue, thisReg, loadedParams.ToArray());
                callInstr.ContinuationEdge = new HighCfgEdge(callInstr, loopIncrementHdl);

                instrs.Add(callInstr);

                HighPhiLink[] loopLinks = new HighPhiLink[] {
                    new HighPhiLink(entryHdl, zeroNativeUIntConstant),
                    new HighPhiLink(loopIncrementHdl, postIncrementReg)
                };
                HighCfgNodeHandle[] loopPredecessors = new HighCfgNodeHandle[] {
                    entryHdl,
                    loopIncrementHdl,
                };

                HighPhi phi = new HighPhi(currentCountReg, loopLinks);

                loopCallHdl.Value = new HighCfgNode(loopPredecessors, new HighPhi[] { phi }, instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                HighSsaRegister oneConstant = new HighSsaRegister(HighValueType.ConstantValue, nativeUIntType, (ulong)1);
                instrs.Add(new Rpa.Instructions.ArithInstruction(null, postIncrementReg, Rpa.Instructions.NumberArithOp.Add, Rpa.Instructions.NumberArithType.NativeUInt, currentCountReg, oneConstant, false));
                instrs.Add(new Rpa.Instructions.BranchCompareNumbersInstruction(null, Rpa.Instructions.NumberCompareOperation.LessThan, Rpa.Instructions.NumberArithType.NativeUInt, currentCountReg, numDelegatesValue, loopCallHdl, returnHdl));

                loopIncrementHdl.Value = new HighCfgNode(instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                if (returnValue == null)
                    instrs.Add(new Rpa.Instructions.ReturnInstruction(null));
                else
                    instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, returnValue));

                returnHdl.Value = new HighCfgNode(instrs.ToArray());
            }

            HighRegion region = new HighRegion(entryHdl);
            RloMethodBody methodBody = new RloMethodBody(locals.ToArray(), delegateMethodSignature.RetType, region, instantiationPath);
            return new RloMethod(methodBody);
        }

        public override int GetHashCode()
        {
            return m_dt.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gm_multicastdelegateinvoke(");
            m_dt.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
