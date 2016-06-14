using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.GeneratedMethods
{
    public class GMDelegateInvoke : MethodKey
    {
        private TypeSpecDelegateTag m_dt;

        public GMDelegateInvoke(TypeSpecDelegateTag dt)
        {
            m_dt = dt;
        }

        public override bool Equals(MethodKey other)
        {
            GMDelegateInvoke tOther = other as GMDelegateInvoke;
            if (tOther == null)
                return false;

            return m_dt == tOther.m_dt;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            TypeSpecDelegateTag delegateType = m_dt;
            TypeSpecClassTag delegateClassType = delegateType.DelegateType;
            MethodSpecTag methodSpec = delegateType.MethodSpec;

            TypeSpecClassTag targetType = methodSpec.DeclaringClass;

            CliClass delegateCls = compiler.GetClosedClass(delegateClassType);

            TypeNameTag delegateTypeName = delegateCls.TypeName;
            HighTypeDef delegateTypeDef = compiler.GetTypeDef(delegateTypeName);
            if (delegateTypeDef.Semantics != TypeSemantics.Delegate)
                throw new RpaCompileException("Delegate-bound class is not a delegate");

            HighTypeDef targetTypeDef = compiler.GetTypeDef(targetType.TypeName);

            CliClass targetCls = null;
            CliInterface targetIfc = null;

            bool isInterface;
            switch (targetTypeDef.Semantics)
            {
                case TypeSemantics.Class:
                case TypeSemantics.Delegate:
                case TypeSemantics.Enum:
                case TypeSemantics.Struct:
                    isInterface = false;
                    targetCls = compiler.GetClosedClass(targetType);
                    break;
                case TypeSemantics.Interface:
                    isInterface = true;
                    targetIfc = compiler.GetClosedInterface(targetType);
                    break;
                default:
                    throw new ArgumentException();
            }

            MethodSignatureTag declSignature = delegateTypeDef.DelegateSignature;

            MethodDeclTag invokeDeclTag = new MethodDeclTag("Invoke", declSignature, delegateTypeName);
            invokeDeclTag = compiler.TagRepository.InternMethodDeclTag(invokeDeclTag);

            uint vtableSlotIndex = delegateCls.DeclTagToVTableSlot[invokeDeclTag];
            CliVtableSlot vtableSlot = delegateCls.VTable[vtableSlotIndex];

            MethodSignatureTag delegateMethodSignature = vtableSlot.MethodSignature;
            MethodSignatureTag targetMethodSignature;

            switch (methodSpec.MethodSlotType)
            {
                case MethodSlotType.Instance:
                case MethodSlotType.Static:
                    {
                        if (isInterface)
                            throw new RpaCompileException("Wrong method spec type for interface");

                        HighMethod method = targetCls.Methods[targetCls.DeclTagToMethod[methodSpec.MethodDecl]];
                        targetMethodSignature = method.MethodSignature.Instantiate(compiler.TagRepository, methodSpec.DeclaringClass.ArgTypes, methodSpec.GenericParameters);
                    }
                    break;
                case MethodSlotType.Virtual:
                    {
                        if (isInterface)
                        {
                            HighClassVtableSlot vtSlot = targetIfc.Slots[targetIfc.CliSlotForSlotTag(methodSpec.MethodDecl)];
                            targetMethodSignature = vtSlot.Signature.Instantiate(compiler.TagRepository, methodSpec.DeclaringClass.ArgTypes, methodSpec.GenericParameters);
                        }
                        else
                        {
                            CliVtableSlot calledVtableSlot = targetCls.VTable[targetCls.DeclTagToVTableSlot[methodSpec.MethodDecl]];

                            if (calledVtableSlot.MethodSignature.NumGenericParameters > 0)
                                throw new RpaCompileException("Can't generate delegate thunk to virtual generic");

                            targetMethodSignature = calledVtableSlot.MethodSignature.Instantiate(compiler.TagRepository, methodSpec.DeclaringClass.ArgTypes, methodSpec.GenericParameters);
                        }
                    }
                    break;
                default:
                    throw new Exception();
            }

            List<HighInstruction> instrs = new List<HighInstruction>();

            HighLocal thisLocal = new HighLocal(m_dt, HighLocal.ETypeOfType.Value);
            HighLocal[] locals = new HighLocal[0];

            List<HighLocal> args = new List<HighLocal>();

            int numParams = delegateMethodSignature.ParamTypes.Length;
            if (numParams != targetMethodSignature.ParamTypes.Length)
                throw new RpaCompileException("Delegate parameter count mismatch");

            List<HighSsaRegister> convertedParameters = new List<HighSsaRegister>();
            for (int i = 0; i < numParams; i++)
            {
                MethodSignatureParam delegateSigParam = delegateMethodSignature.ParamTypes[i];
                MethodSignatureParam targetSigParam = targetMethodSignature.ParamTypes[i];

                TypeSpecTag delegateSigType = delegateSigParam.Type;
                TypeSpecTag targetSigType = targetSigParam.Type;

                if (delegateSigParam.TypeOfType.Value != targetSigParam.TypeOfType.Value)
                    throw new RpaCompileException("Delegate parameter type-of-type mismatch");

                switch (delegateSigParam.TypeOfType.Value)
                {
                    case MethodSignatureParamTypeOfType.Values.ByRef:
                        {
                            if (delegateSigType != targetSigType)
                                throw new RpaCompileException("Delegate parameter type mismatch");

                            HighSsaRegister ssa = new HighSsaRegister(HighValueType.ManagedPtr, delegateSigType, null);
                            HighLocal arg = new HighLocal(delegateSigParam.Type, HighLocal.ETypeOfType.ByRef);

                            args.Add(arg);
                            instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, ssa, arg));
                            convertedParameters.Add(ssa);
                        }
                        break;
                    case MethodSignatureParamTypeOfType.Values.Value:
                        {
                            if (delegateSigType == targetSigType)
                            {
                                HighSsaRegister ssa = new HighSsaRegister(HighValueType.ValueValue, delegateSigParam.Type, null);
                                HighLocal arg = new HighLocal(delegateSigParam.Type, HighLocal.ETypeOfType.Value);
                                args.Add(arg);
                                instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, ssa, arg));
                                convertedParameters.Add(ssa);
                            }
                            else
                            {
                                if (compiler.TypeIsValueType(delegateSigType) || compiler.TypeIsValueType(targetSigType))
                                    throw new RpaCompileException("Delegate parameter type mismatch");

                                HighSsaRegister delegateParamSsa = new HighSsaRegister(HighValueType.ReferenceValue, delegateSigType, null);
                                HighLocal arg = new HighLocal(delegateSigParam.Type, HighLocal.ETypeOfType.Value);
                                args.Add(arg);
                                instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, delegateParamSsa, arg));


                                HighSsaRegister targetParamSsa = GenerateConvertReference(compiler, delegateParamSsa, targetSigType, instrs);

                                convertedParameters.Add(targetParamSsa);
                            }
                        }
                        break;
                    case MethodSignatureParamTypeOfType.Values.TypedByRef:
                        throw new NotImplementedException();
                    default:
                        throw new Exception();
                }
            }

            HighSsaRegister methodReturnValue;
            if (targetMethodSignature.RetType is TypeSpecVoidTag)
                methodReturnValue = null;
            else
            {
                bool isValueType = compiler.TypeIsValueType(targetMethodSignature.RetType);
                methodReturnValue = new HighSsaRegister(isValueType ? HighValueType.ValueValue : HighValueType.ReferenceValue, targetMethodSignature.RetType, null);
            }

            HighInstruction callInstr;

            switch (methodSpec.MethodSlotType)
            {
                case MethodSlotType.Instance:
                    {
                        HighSsaRegister instanceReg;
                        HighSsaRegister thisReg = new HighSsaRegister(HighValueType.ReferenceValue, m_dt, null);
                        instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, thisReg, thisLocal));

                        if (compiler.TypeIsValueType(methodSpec.DeclaringClass))
                        {
                            HighSsaRegister boxedReg = new HighSsaRegister(HighValueType.BoxedValue, methodSpec.DeclaringClass, null);
                            instrs.Add(new Instructions.LoadDelegateTargetInstruction(null, boxedReg, thisReg));
                            instanceReg = new HighSsaRegister(HighValueType.ManagedPtr, methodSpec.DeclaringClass, null);
                            instrs.Add(new Rpa.Instructions.UnboxPtrInstruction(null, instanceReg, boxedReg));
                        }
                        else
                        {
                            instanceReg = new HighSsaRegister(HighValueType.BoxedValue, methodSpec.DeclaringClass, null);
                            instrs.Add(new Instructions.LoadDelegateTargetInstruction(null, instanceReg, thisReg));
                        }

                        MethodHandle methodHandle = compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), instantiationPath);
                        callInstr = new Instructions.CallRloInstanceMethodInstruction(null, methodHandle, methodReturnValue, instanceReg, convertedParameters.ToArray());
                    }
                    break;
                case MethodSlotType.Virtual:
                    {
                        HighSsaRegister instanceReg;
                        HighSsaRegister thisReg = new HighSsaRegister(HighValueType.ReferenceValue, m_dt, null);
                        instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, thisReg, thisLocal));

                        if (compiler.TypeIsValueType(methodSpec.DeclaringClass))
                        {
                            HighSsaRegister boxedReg = new HighSsaRegister(HighValueType.BoxedValue, methodSpec.DeclaringClass, null);
                            instrs.Add(new Instructions.LoadDelegateTargetInstruction(null, boxedReg, thisReg));
                            instanceReg = new HighSsaRegister(HighValueType.ManagedPtr, methodSpec.DeclaringClass, null);
                            instrs.Add(new Rpa.Instructions.UnboxPtrInstruction(null, instanceReg, boxedReg));
                        }
                        else
                        {
                            instanceReg = new HighSsaRegister(HighValueType.BoxedValue, methodSpec.DeclaringClass, null);
                            instrs.Add(new Instructions.LoadDelegateTargetInstruction(null, instanceReg, thisReg));
                        }

                        if (isInterface)
                        {
                            uint cliSlot = targetIfc.CliSlotForSlotTag(methodSpec.MethodDecl);
                            callInstr = new Instructions.CallRloInterfaceMethodInstruction(null, cliSlot, methodReturnValue, instanceReg, convertedParameters.ToArray());
                        }
                        else
                        {
                            uint targetVtableSlot = targetCls.DeclTagToVTableSlot[methodSpec.MethodDecl];
                            callInstr = new Instructions.CallRloVirtualMethodInstruction(null, targetVtableSlot, methodReturnValue, instanceReg, convertedParameters.ToArray());
                        }
                    }
                    break;
                case MethodSlotType.Static:
                    {
                        MethodHandle methodHandle = compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), instantiationPath);
                        callInstr = new Instructions.CallRloStaticMethodInstruction(null, methodHandle, methodReturnValue, convertedParameters.ToArray());
                    }
                    break;
                default:
                    throw new Exception();
            }

            List<HighInstruction> returnInstrs = new List<HighInstruction>();

            if (methodReturnValue == null)
            {
                if (!(delegateMethodSignature.RetType is TypeSpecVoidTag))
                    throw new RpaCompileException("Delegate return type mismatch");

                returnInstrs.Add(new Rpa.Instructions.ReturnInstruction(null));
            }
            else
            {
                if (delegateMethodSignature.RetType is TypeSpecVoidTag)
                    throw new RpaCompileException("Delegate return type mismatch");

                TypeSpecTag delegateRetType = delegateMethodSignature.RetType;
                TypeSpecTag targetRetType = targetMethodSignature.RetType;

                if (delegateMethodSignature.RetType == targetMethodSignature.RetType)
                    returnInstrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, methodReturnValue));
                else
                {
                    if (compiler.TypeIsValueType(targetRetType) || compiler.TypeIsValueType(delegateRetType))
                        throw new RpaCompileException("Delegate return type mismatch");

                    HighSsaRegister retReg = GenerateConvertReference(compiler, methodReturnValue, delegateRetType, returnInstrs);
                    returnInstrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, retReg));
                }
            }

            HighCfgNode returnNode = new HighCfgNode(returnInstrs.ToArray());

            callInstr.ContinuationEdge = new HighCfgEdge(callInstr, new HighCfgNodeHandle(returnNode));
            instrs.Add(callInstr);

            HighCfgNode entryNode = new HighCfgNode(instrs.ToArray());
            HighRegion region = new HighRegion(new HighCfgNodeHandle(entryNode));
            RloMethodBody methodBody = new RloMethodBody(thisLocal, args.ToArray(), locals, delegateMethodSignature.RetType, region, delegateMethodSignature, instantiationPath);

            return new RloMethod(methodBody);
        }

        private HighSsaRegister GenerateConvertReference(Compiler compiler, HighSsaRegister sourceReg, TypeSpecTag targetType, List<HighInstruction> instrs)
        {
            HighSsaRegister targetSsa = new HighSsaRegister(HighValueType.ReferenceValue, targetType, null);

            switch (compiler.AssignabilityResolver.ResolveRefAssignable(sourceReg.Type, targetType))
            {
                case AssignabilityResolver.ConversionType.ArrayToGenericCollection:
                case AssignabilityResolver.ConversionType.ArrayToGenericEnumerable:
                case AssignabilityResolver.ConversionType.ArrayToGenericList:
                case AssignabilityResolver.ConversionType.ClassToInterface:
                    instrs.Add(new Instructions.ObjectToInterfaceInstruction(null, targetSsa, sourceReg));
                    break;
                case AssignabilityResolver.ConversionType.InterfaceToInterface:
                    instrs.Add(new Instructions.InterfaceToInterfaceInstruction(null, targetSsa, sourceReg));
                    break;
                case AssignabilityResolver.ConversionType.ClassToClass:
                    instrs.Add(new Instructions.ObjectToObjectInstruction(null, targetSsa, sourceReg));
                    break;
                case AssignabilityResolver.ConversionType.InterfaceToObject:
                    instrs.Add(new Instructions.InterfaceToObjectInstruction(null, targetSsa, sourceReg));
                    break;
                case AssignabilityResolver.ConversionType.NotConvertible:
                    throw new RpaCompileException("Incompatible delegate value conversion");
                case AssignabilityResolver.ConversionType.Exact:    // Should never happen
                default:
                    throw new Exception();
            }

            return targetSsa;
        }

        public override int GetHashCode()
        {
            return m_dt.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gm_delegateinvoke(");
            m_dt.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
