using System;
using System.Collections.Generic;
using Clarity.Rpa;
using Clarity.Rpa.Instructions;

namespace Clarity.RpaCompiler
{
    // This pass lowers all high-level RPA instructions to RLO instructions and performs validation
    public class RloInitPass : RloPerNodePass
    {
        private RloFindPredecessorsAndSuccessorsPass m_psPass;

        public RloInitPass(Compiler compiler, RloMethodBody methodBody, RloFindPredecessorsAndSuccessorsPass psPass)
            : base(compiler, methodBody)
        {
            m_psPass = psPass;
        }

        private static bool CanPhiMatchVT(HighValueType phiVT, HighValueType destVT)
        {
            switch (phiVT)
            {
                case HighValueType.ConstantString:
                case HighValueType.Null:
                case HighValueType.ReferenceValue:
                    return destVT == HighValueType.ReferenceValue;
                case HighValueType.ConstantValue:
                case HighValueType.ValueValue:
                    return destVT == HighValueType.ValueValue;
                case HighValueType.ManagedPtr:
                    return destVT == HighValueType.ManagedPtr;
                default:
                    throw new Exception();
            }
        }

        protected override void ProcessNode(HighCfgNode cfgNode)
        {
            int numInstructions = cfgNode.Instructions.Length;
            if (cfgNode.Instructions.Length == 0)
                throw new Exception("CFG node has no instructions");

            HighInstruction[] instructions = cfgNode.Instructions;
            for (int i = 0; i < numInstructions; i++)
                if (instructions[i].TerminatesControlFlow != (i == numInstructions - 1))
                    throw new Exception("Unexpected control flow instruction location");

            // Validate phis
            ISet<HighCfgNode> nodePreds = m_psPass.PredecessorsForNode(cfgNode);

            foreach (HighPhi phi in cfgNode.Phis)
            {
                HashSet<HighCfgNode> linkPreds = new HashSet<HighCfgNode>();
                foreach (HighPhiLink link in phi.Links)
                {
                    if (!CanPhiMatchVT(link.Reg.ValueType, phi.Dest.ValueType) || link.Reg.Type != phi.Dest.Type)
                        throw new Exception("Phi predecessor type does not match link destination type");
                    if (!linkPreds.Add(link.Predecessor.Value))
                        throw new Exception("Duplicate phi predecessor");
                }

                // It's OK for a phi link predecessor to not be an actual predecessor, but it's not
                // OK for an actual predecessor to be missing a phi link
                if (nodePreds != null)
                {
                    foreach (HighCfgNode nodePred in nodePreds)
                        if (!linkPreds.Contains(nodePred))
                            throw new Exception("Phi is missing a predecessor link");
                }
            }

            // Process instructions
            List<HighInstruction> newInstrs = new List<HighInstruction>();
            foreach (HighInstruction instr in cfgNode.Instructions)
                ProcessInstruction(cfgNode, instr, newInstrs);
        }

        private void ProcessInstruction(HighCfgNode cfgNode, HighInstruction instr, List<HighInstruction> newInstrs)
        {
            switch (instr.Opcode)
            {
                case HighInstruction.Opcodes.LoadLocal:
                    {
                        LoadLocalInstruction tInstr = (LoadLocalInstruction)instr;
                        HighSsaRegister dest = tInstr.Dest;
                        HighLocal local = tInstr.Local;

                        switch (local.TypeOfType)
                        {
                            case HighLocal.ETypeOfType.ByRef:
                                if (dest.ValueType != HighValueType.ManagedPtr)
                                    throw new RpaCompileException("Illegal LoadLocal");
                                break;
                            case HighLocal.ETypeOfType.TypedByRef:
                                throw new NotImplementedException();
                            case HighLocal.ETypeOfType.Value:
                                if (dest.ValueType != HighValueType.ValueValue && dest.ValueType != HighValueType.ReferenceValue)
                                    throw new RpaCompileException("Illegal LoadLocal");
                                break;
                            default:
                                throw new Exception();
                        }

                        if (dest.Type != local.Type)
                            throw new RpaCompileException("Type mismatch in LoadLocal");

                        newInstrs.Add(instr);
                    }
                    break;
                case HighInstruction.Opcodes.AllocArray:
                case HighInstruction.Opcodes.AllocObj:
                case HighInstruction.Opcodes.Arith:
                case HighInstruction.Opcodes.Box:
                case HighInstruction.Opcodes.BranchCompareNumbers:
                case HighInstruction.Opcodes.Branch:
                case HighInstruction.Opcodes.DynamicCast:
                case HighInstruction.Opcodes.EnterProtectedBlock:
                case HighInstruction.Opcodes.ForceDynamicCast:
                case HighInstruction.Opcodes.GetArrayElementPtr:
                case HighInstruction.Opcodes.CompareRefs:
                case HighInstruction.Opcodes.BranchCompareRefs:
                case HighInstruction.Opcodes.GetStaticFieldAddr:
                case HighInstruction.Opcodes.BranchRefNull:
                case HighInstruction.Opcodes.GetTypeInfo:
                case HighInstruction.Opcodes.CallConstrainedMethod:
                case HighInstruction.Opcodes.CallConstrainedVirtualMethod:
                case HighInstruction.Opcodes.LeaveRegion:
                case HighInstruction.Opcodes.LoadPtr:
                case HighInstruction.Opcodes.PtrField:
                case HighInstruction.Opcodes.CallInstanceMethod:
                case HighInstruction.Opcodes.RefField:
                case HighInstruction.Opcodes.NumberConvert:
                case HighInstruction.Opcodes.CallStaticMethod:
                case HighInstruction.Opcodes.CompareNumbers:
                case HighInstruction.Opcodes.CallVirtualMethod:
                case HighInstruction.Opcodes.GetArrayLength:
                case HighInstruction.Opcodes.PassiveConvert:
                case HighInstruction.Opcodes.Return:
                case HighInstruction.Opcodes.GetLocalPtr:
                case HighInstruction.Opcodes.ReturnValue:
                case HighInstruction.Opcodes.UnaryArith:
                case HighInstruction.Opcodes.StoreLocal:
                case HighInstruction.Opcodes.UnboxPtr:
                case HighInstruction.Opcodes.ZeroFillPtr:
                case HighInstruction.Opcodes.UnboxValue:
                case HighInstruction.Opcodes.Switch:
                case HighInstruction.Opcodes.Throw:
                case HighInstruction.Opcodes.RefLocal:
                case HighInstruction.Opcodes.StorePtr:
                case HighInstruction.Opcodes.GetFieldInfo:
                case HighInstruction.Opcodes.LoadValueField:
                case HighInstruction.Opcodes.BindStaticDelegate:
                case HighInstruction.Opcodes.BindInstanceDelegate:
                case HighInstruction.Opcodes.BindVirtualDelegate:
                case HighInstruction.Opcodes.Catch:
                    newInstrs.Add(instr);
                    break;
                    //throw new NotImplementedException();
                default:
                    throw new ArgumentException();
            }
        }
    }
}
