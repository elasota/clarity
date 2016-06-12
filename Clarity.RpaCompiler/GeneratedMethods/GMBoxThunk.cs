using System;
using Clarity.Rpa;
using System.Collections.Generic;

namespace Clarity.RpaCompiler.GeneratedMethods
{
    // This generated method thunks calls to box references to the underlying boxed value
    public class GMBoxThunk : MethodKey
    {
        private MethodSpecTag m_methodSpec;

        public GMBoxThunk(MethodSpecTag methodSpec)
        {
            m_methodSpec = methodSpec;
        }

        public override bool Equals(MethodKey other)
        {
            GMBoxThunk tOther = other as GMBoxThunk;
            if (tOther == null)
                return false;

            return m_methodSpec == tOther.m_methodSpec;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            if (m_methodSpec.MethodSlotType != MethodSlotType.Instance)
                throw new ArgumentException();

            TypeSpecClassTag valueClass = m_methodSpec.DeclaringClass;

            TypeSpecBoxTag boxType = new TypeSpecBoxTag(valueClass);
            boxType = (TypeSpecBoxTag)compiler.TagRepository.InternTypeSpec(boxType);

            CliClass cls = compiler.GetClosedClass(valueClass);

            HighLocal thisLocal = new HighLocal(boxType, HighLocal.ETypeOfType.Value);
            uint methodSlot;
            if (!cls.DeclTagToMethod.TryGetValue(m_methodSpec.MethodDecl, out methodSlot))
                throw new RpaCompileException("Unmatched vtable method");

            HighMethod method = cls.Methods[methodSlot];

            if (method.IsStatic)
                throw new RpaCompileException("Box-thunked method is static");

            List<HighLocal> locals = new List<HighLocal>();
            foreach (MethodSignatureParam param in method.MethodSignature.ParamTypes)
            {
                switch (param.TypeOfType.Value)
                {
                    case MethodSignatureParamTypeOfType.Values.ByRef:
                        locals.Add(new HighLocal(param.Type, HighLocal.ETypeOfType.ByRef));
                        break;
                    case MethodSignatureParamTypeOfType.Values.TypedByRef:
                        locals.Add(new HighLocal(param.Type, HighLocal.ETypeOfType.TypedByRef));
                        break;
                    case MethodSignatureParamTypeOfType.Values.Value:
                        locals.Add(new HighLocal(param.Type, HighLocal.ETypeOfType.Value));
                        break;
                    default:
                        throw new Exception();
                }
            }

            HighSsaRegister returnValue = null;
            TypeSpecTag retType = method.MethodSignature.RetType;
            if (!(retType is TypeSpecVoidTag))
                returnValue = new HighSsaRegister(compiler.TypeIsValueType(retType) ? HighValueType.ValueValue : HighValueType.ReferenceValue, retType, null);

            List<HighInstruction> returnInstrs = new List<HighInstruction>();
            if (returnValue == null)
                returnInstrs.Add(new Clarity.Rpa.Instructions.ReturnInstruction(null));
            else
                returnInstrs.Add(new Clarity.Rpa.Instructions.ReturnValueInstruction(null, returnValue));

            HighCfgNode returnNode = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], returnInstrs.ToArray());

            List<HighInstruction> instrs = new List<HighInstruction>();

            HighSsaRegister thisRef = new HighSsaRegister(HighValueType.BoxedValue, valueClass, null);
            HighSsaRegister thisManagedPtr = new HighSsaRegister(HighValueType.ManagedPtr, valueClass, null);

            instrs.Add(new Clarity.Rpa.Instructions.UnboxPtrInstruction(null, thisManagedPtr, thisRef));

            List<HighSsaRegister> paramSSAs = new List<HighSsaRegister>();
            foreach (HighLocal local in locals)
            {
                HighSsaRegister paramSSA;
                switch (local.TypeOfType)
                {
                    case HighLocal.ETypeOfType.ByRef:
                        paramSSA = new HighSsaRegister(HighValueType.ManagedPtr, local.Type, null);
                        break;
                    case HighLocal.ETypeOfType.TypedByRef:
                        throw new NotImplementedException();
                    case HighLocal.ETypeOfType.Value:
                        paramSSA = new HighSsaRegister(compiler.TypeIsValueType(local.Type) ? HighValueType.ValueValue : HighValueType.ReferenceValue, retType, null);
                        break;
                    default:
                        throw new RpaCompileException("Unknown type of type");
                }
                paramSSAs.Add(paramSSA);
                instrs.Add(new Clarity.Rpa.Instructions.LoadLocalInstruction(null, paramSSA, local));
            }

            MethodHandle methodHandle = compiler.InstantiateMethod(new MethodSpecMethodKey(m_methodSpec), instantiationPath);
            Instructions.CallRloInstanceMethodInstruction callInstr = new Instructions.CallRloInstanceMethodInstruction(null, methodHandle, returnValue, thisManagedPtr, paramSSAs.ToArray());

            callInstr.ContinuationEdge = new HighCfgEdge(callInstr, new HighCfgNodeHandle(returnNode));
            instrs.Add(callInstr);

            HighCfgNode entryNode = new HighCfgNode(new HighCfgNodeHandle[0], new HighPhi[0], instrs.ToArray());

            HighRegion entryRegion = new HighRegion(new HighCfgNodeHandle(entryNode));

            RloMethodBody methodBody = new RloMethodBody(locals.ToArray(), retType, entryRegion, instantiationPath);
            return new RloMethod(methodBody);
        }

        public override int GetHashCode()
        {
            return m_methodSpec.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gm_boxthunk(");
            m_methodSpec.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
