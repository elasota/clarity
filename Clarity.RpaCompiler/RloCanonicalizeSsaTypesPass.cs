using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    // This pass canonicalizes all SSA value types to ValueValue if they're natively value type,
    // ReferenceValue if they're natively reference type, and BoxedValue if they're boxed.
    // It should be run early, before cross-node SSA passes.
    public class RloCanonicalizeSsaTypesPass : RloPerNodePass
    {
        private HighInstruction.VisitSsaDelegate m_ssaVisitor;
        private Dictionary<HighSsaRegister, HighSsaRegister> m_ssaReplacements;

        public RloCanonicalizeSsaTypesPass(Compiler compiler, RloMethodBody methodBody)
            : base(compiler, methodBody)
        {
            m_ssaVisitor = VisitSsa;
            m_ssaReplacements = new Dictionary<HighSsaRegister, HighSsaRegister>();
        }

        private void VisitSsa(ref HighSsaRegister ssaReg)
        {
            if (ssaReg != null)
                ProcessSsa(ssaReg);
        }

        private void ProcessSsa(HighSsaRegister ssaReg)
        {
            switch (ssaReg.ValueType)
            {
                case HighValueType.ConstantValue:
                case HighValueType.ConstantString:
                case HighValueType.Null:
                case HighValueType.ManagedPtr:
                    return;
                case HighValueType.ReferenceValue:
                case HighValueType.ValueValue:
                    {
                        TypeSpecTag tsTag = ssaReg.Type;
                        bool isValueType;
                        if (tsTag is TypeSpecArrayTag)
                            isValueType = false;
                        else if (tsTag is TypeSpecClassTag)
                        {
                            TypeSpecClassTag classSpec = (TypeSpecClassTag)tsTag;
                            switch (this.Compiler.GetTypeDef(classSpec.TypeName).Semantics)
                            {
                                case TypeSemantics.Class:
                                case TypeSemantics.Delegate:
                                case TypeSemantics.Interface:
                                    isValueType = false;
                                    break;
                                case TypeSemantics.Enum:
                                case TypeSemantics.Struct:
                                    isValueType = true;
                                    break;
                                default:
                                    throw new RpaCompileException("Invalid SSA type");
                            }
                        }
                        else
                            throw new RpaCompileException("Invalid SSA type");

                        if (ssaReg.ValueType == HighValueType.ValueValue)
                        {
                            if (!isValueType)
                                m_ssaReplacements.Add(ssaReg, new HighSsaRegister(HighValueType.ReferenceValue, tsTag, ssaReg.ConstantValue));
                        }
                        else if (ssaReg.ValueType == HighValueType.ReferenceValue)
                        {
                            if (isValueType)
                            {
                                if (tsTag is TypeSpecClassTag)
                                {
                                    TypeSpecClassTag tsClass = (TypeSpecClassTag)tsTag;
                                    if (tsClass.TypeName.FastIs("mscorlib", "System", "Nullable`1", 1, null))
                                    {
                                        TypeSpecTag nullableTypeArg = tsClass.ArgTypes[0];

                                        if (!(nullableTypeArg is TypeSpecClassTag))
                                            throw new RpaCompileException("Boxed nullable type subscript is not a class");

                                        TypeSpecClassTag nullableTypeClass = (TypeSpecClassTag)nullableTypeArg;
                                        TypeSemantics semantics = this.Compiler.GetTypeDef(nullableTypeClass.TypeName).Semantics;
                                        if (semantics != TypeSemantics.Enum && semantics != TypeSemantics.Struct)
                                            throw new RpaCompileException("Boxed nullable type subscript is not a value type");

                                        tsTag = nullableTypeClass;
                                    }
                                }
                                m_ssaReplacements.Add(ssaReg, new HighSsaRegister(HighValueType.BoxedValue, tsTag, ssaReg.ConstantValue));
                            }
                        }
                        else
                            throw new Exception();
                    }
                    break;
                default:
                    throw new RpaCompileException("Invalid SSA type");
            }
        }

        protected override void ProcessNode(HighCfgNode cfgNode)
        {
            foreach (HighPhi phi in cfgNode.Phis)
                ProcessSsa(phi.Dest);
            foreach (HighInstruction instr in cfgNode.Instructions)
                instr.VisitSsaDests(m_ssaVisitor);
        }

        protected override void FinalizePass()
        {
            (new RloReplaceSsaPass(this.Compiler, this.MethodBody, m_ssaReplacements)).Run();
        }
    }
}
