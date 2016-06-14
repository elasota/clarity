using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.GeneratedMethods
{
    // This generated method thunks calls to box references to the underlying boxed value
    public class GMBoxedValueTypeGetHashCode : MethodKey
    {
        private TypeSpecBoxTag m_bt;
        private VTableGenerationCache m_vtCache;

        public GMBoxedValueTypeGetHashCode(TypeSpecBoxTag bt, VTableGenerationCache vtCache)
        {
            m_bt = bt;
            m_vtCache = vtCache;
        }

        public override bool Equals(MethodKey other)
        {
            GMBoxedValueTypeGetHashCode tOther = other as GMBoxedValueTypeGetHashCode;
            if (tOther == null)
                return false;

            return m_bt == tOther.m_bt;
        }

        // Inspects a value for hashable fields.  If the field is hashable, returns true with the stack containing
        // the field descension.
        private bool RecursiveFindHashableField(Compiler compiler, TypeSpecTag fieldSpec, Stack<HighField> fieldStack)
        {
            // If the field is a reference type, this is OK
            if (!compiler.TypeIsValueType(fieldSpec))
                return true;

            CliClass cls = compiler.GetClosedClass((TypeSpecClassTag)fieldSpec);

            if (cls.InstanceFields.Length == 0)
            {
                TypeNameTag typeName = cls.TypeName;

                if (typeName.AssemblyName == "mscorlib" && typeName.ContainerType == null && typeName.TypeNamespace == "System" && typeName.NumGenericParameters == 0)
                {
                    string tn = typeName.TypeName;
                    if (tn == "Boolean" || tn == "Char"
                        || tn == "SByte" || tn == "Int16" || tn == "Int32" || tn == "Int64" || tn == "IntPtr"
                        || tn == "Byte" || tn == "UInt16" || tn == "UInt32" || tn == "UInt64" || tn == "UIntPtr")
                        return true;
                }

                // Empty structs that aren't a built-in POD type can't be hashed
                return false;
            }

            if ((m_vtCache.GetClassPodFlags(compiler, cls) & VTableGenerationCache.PodFlags.HashCode) != VTableGenerationCache.PodFlags.None)
                return true;

            // See if the class has a GetHashCode override
            uint vtSlot = m_vtCache.GetGetHashCodeVTableSlot(compiler);

            if (cls.VTable[vtSlot].MethodIndex.Depth == 0)
                return true;

            foreach (HighField fld in cls.InstanceFields)
            {
                fieldStack.Push(fld);
                if (RecursiveFindHashableField(compiler, fld.Type, fieldStack))
                    return true;
                fieldStack.Pop();
            }

            return false;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            HighLocal[] locals = new HighLocal[0];
            HighLocal[] args = new HighLocal[0];
            HighLocal instanceLocal = new HighLocal(m_bt, HighLocal.ETypeOfType.Value);

            Stack<HighField> fieldStack = new Stack<HighField>();

            uint getHashCodeSlot = m_vtCache.GetGetHashCodeVTableSlot(compiler);
            HighSsaRegister result = new HighSsaRegister(HighValueType.ValueValue, m_vtCache.GetSystemInt32Type(compiler), null);

            MethodSignatureTag methodSignature = new MethodSignatureTag(0, m_vtCache.GetSystemInt32Type(compiler), new MethodSignatureParam[0]);
            methodSignature = compiler.TagRepository.InternMethodSignature(methodSignature);

            if (RecursiveFindHashableField(compiler, m_bt.ContainedType, fieldStack))
            {
                // Found a field that can be hashed
                Queue<HighField> fieldQueue = new Queue<HighField>();
                while (fieldStack.Count > 0)
                    fieldQueue.Enqueue(fieldStack.Pop());

                HighSsaRegister thisRef = new HighSsaRegister(HighValueType.BoxedValue, m_bt.ContainedType, null);
                HighSsaRegister thisPtr = new HighSsaRegister(HighValueType.ManagedPtr, m_bt.ContainedType, null);

                HighCfgNodeHandle unboxHdl = new HighCfgNodeHandle();
                HighCfgNodeHandle locateAndHashFieldHdl = new HighCfgNodeHandle();

                {
                    List<HighInstruction> instrs = new List<HighInstruction>();
                    instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, thisRef, instanceLocal));
                    Rpa.Instructions.UnboxPtrInstruction unboxInstr = new Rpa.Instructions.UnboxPtrInstruction(null, thisPtr, thisRef);
                    unboxInstr.ContinuationEdge = new HighCfgEdge(unboxInstr, locateAndHashFieldHdl);
                    instrs.Add(unboxInstr);

                    unboxHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());
                }

                HighSsaRegister hashablePtr = thisPtr;

                List<HighInstruction> lahInstrs = new List<HighInstruction>();
                while (fieldQueue.Count > 0)
                {
                    HighField fld = fieldQueue.Dequeue();

                    HighSsaRegister nextFldPtr = new HighSsaRegister(HighValueType.ManagedPtr, fld.Type, null);
                    lahInstrs.Add(new Rpa.Instructions.PtrFieldInstruction(null, nextFldPtr, hashablePtr, fld.Name));
                    hashablePtr = nextFldPtr;
                }

                if (compiler.TypeIsValueType(hashablePtr.Type))
                {
                    HighCfgNodeHandle returnResultHdl = new HighCfgNodeHandle();

                    CliClass cls = compiler.GetClosedClass((TypeSpecClassTag)hashablePtr.Type);

                    CliMethodIndex vtableHashCodeIndex = cls.VTable[getHashCodeSlot].MethodIndex;
                    if (vtableHashCodeIndex.Depth == 0)
                    {
                        HighMethod method = cls.Methods[vtableHashCodeIndex.Index];

                        Instructions.CallRloVirtualMethodInstruction callInstr = new Instructions.CallRloVirtualMethodInstruction(null, getHashCodeSlot, result, hashablePtr, new HighSsaRegister[0]);
                        callInstr.ContinuationEdge = new HighCfgEdge(callInstr, returnResultHdl);
                        lahInstrs.Add(callInstr);
                    }
                    else
                    {
                        // Base class GetHashCode, but field was found as hashable, which means it's POD
                        MethodDeclTag hashBytesDecl = m_vtCache.GetHashBytesDeclTag(compiler);

                        MethodSpecTag methodSpec = new MethodSpecTag(MethodSlotType.Static, new TypeSpecTag[] { hashablePtr.Type }, m_vtCache.GetClarityToolsType(compiler), hashBytesDecl);
                        methodSpec = compiler.TagRepository.InternMethodSpec(methodSpec);

                        MethodHandle methodHandle = compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), instantiationPath);

                        Instructions.CallRloStaticMethodInstruction callInstr = new Instructions.CallRloStaticMethodInstruction(null, methodHandle, result, new HighSsaRegister[] { hashablePtr });
                        callInstr.ContinuationEdge = new HighCfgEdge(callInstr, returnResultHdl);
                        lahInstrs.Add(callInstr);
                    }

                    // Add return block
                    {
                        List<HighInstruction> rrInstrs = new List<HighInstruction>();
                        rrInstrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, result));

                        returnResultHdl.Value = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], rrInstrs.ToArray());
                    }
                }
                else
                {
                    HighCfgNodeHandle fldNullHdl = new HighCfgNodeHandle();
                    HighCfgNodeHandle callGetHashCodeHdl = new HighCfgNodeHandle();
                    HighCfgNodeHandle returnResultHdl = new HighCfgNodeHandle();

                    HighSsaRegister fldValue = new HighSsaRegister(HighValueType.ReferenceValue, hashablePtr.Type, null);

                    lahInstrs.Add(new Rpa.Instructions.LoadPtrInstruction(null, fldValue, hashablePtr));
                    lahInstrs.Add(new Rpa.Instructions.BranchRefNullInstruction(null, fldValue, fldNullHdl, callGetHashCodeHdl));

                    {
                        HighSsaRegister zeroConstant = new HighSsaRegister(HighValueType.ConstantValue, m_vtCache.GetSystemInt32Type(compiler), 0);

                        List<HighInstruction> instrs = new List<HighInstruction>();
                        instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, zeroConstant));
                        fldNullHdl.Value = new HighCfgNode(instrs.ToArray());
                    }

                    {
                        List<HighInstruction> instrs = new List<HighInstruction>();

                        Instructions.CallRloVirtualMethodInstruction callInstr = new Instructions.CallRloVirtualMethodInstruction(null, getHashCodeSlot, result, fldValue, new HighSsaRegister[0]);
                        callInstr.ContinuationEdge = new HighCfgEdge(callInstr, returnResultHdl);

                        callGetHashCodeHdl.Value = new HighCfgNode(instrs.ToArray());
                    }

                    {
                        List<HighInstruction> instrs = new List<HighInstruction>();
                        instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, result));
                        returnResultHdl.Value = new HighCfgNode(instrs.ToArray());
                    }
                }

                HighCfgNodeHandle entryHdl = new HighCfgNodeHandle(new HighCfgNode(lahInstrs.ToArray()));
                HighRegion region = new HighRegion(entryHdl);
                RloMethodBody methodBody = new RloMethodBody(instanceLocal, args, locals, m_vtCache.GetSystemInt32Type(compiler), region, methodSignature, instantiationPath);

                return new RloMethod(methodBody);
            }
            else
            {
                // No fields can be POD hashed, hash the type instead
                HighSsaRegister type = new HighSsaRegister(HighValueType.ReferenceValue, m_vtCache.GetSystemTypeType(compiler), null);

                HighCfgNodeHandle entryHdl = new HighCfgNodeHandle();
                HighCfgNodeHandle returnResultHdl = new HighCfgNodeHandle();

                {
                    List<HighInstruction> instrs = new List<HighInstruction>();
                    instrs.Add(new Rpa.Instructions.GetTypeInfoInstruction(null, type, m_bt.ContainedType));
                    Instructions.CallRloVirtualMethodInstruction callInstr = new Instructions.CallRloVirtualMethodInstruction(null, getHashCodeSlot, result, type, new HighSsaRegister[0]);
                    callInstr.ContinuationEdge = new HighCfgEdge(callInstr, returnResultHdl);
                    instrs.Add(callInstr);
                    entryHdl.Value = new HighCfgNode(instrs.ToArray());
                }

                {
                    List<HighInstruction> instrs = new List<HighInstruction>();
                    instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, result));
                    returnResultHdl.Value = new HighCfgNode(instrs.ToArray());
                }

                HighRegion region = new HighRegion(entryHdl);
                RloMethodBody methodBody = new RloMethodBody(instanceLocal, args, locals, m_vtCache.GetSystemInt32Type(compiler), region, methodSignature, instantiationPath);
                return new RloMethod(methodBody);
            }
        }

        public override int GetHashCode()
        {
            return m_bt.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gm_boxedvaluetypegethashcode(");
            m_bt.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
