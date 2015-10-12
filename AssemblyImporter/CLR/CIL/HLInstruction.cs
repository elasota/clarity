using System;
using System.Collections.Generic;
using System.Text;

namespace AssemblyImporter.CLR.CIL
{
    public struct HLInstruction
    {
        public HLOpcode Opcode { get; private set; }
        public HLOpFlags Flags { get; private set; }
        public HLOpType TypeToken { get; private set; }
        public HLArguments Arguments { get; private set; }

        public HLInstruction(HLOpcode opcode)
        {
            this = new HLInstruction();
            Opcode = opcode;
            Flags = HLOpFlags.None;
            TypeToken = HLOpType.None;
            Arguments = new HLArguments();
        }

        public HLInstruction Fl(HLOpFlags flags)
        {
            HLInstruction copy = this;
            copy.Flags = flags;
            return copy;
        }

        public HLInstruction TT(HLOpType type)
        {
            HLInstruction copy = this;
            copy.TypeToken = type;
            return copy;
        }

        public HLInstruction Args(HLArguments args)
        {
            HLInstruction copy = this;
            copy.Arguments = args;
            return copy;
        }

        private static uint UnpatchInstruction(uint pc, int offset, Dictionary<uint, uint> offsetToInstruction)
        {
            int newPC = (int)pc + offset;
            if (newPC < 0)
                throw new OverflowException("Invalid PC");
            return offsetToInstruction[(uint)newPC];
        }

        private void UnpatchSwitch(uint pc, Dictionary<uint, uint> offsetToInstruction)
        {
            int[] targets = (int[])Arguments.ObjValue;
            uint[] convertedTargets = new uint[targets.Length];

            for (int i = 0; i < targets.Length; i++)
                convertedTargets[i] = UnpatchInstruction(pc, targets[i], offsetToInstruction);
            Arguments = HLArguments.CreateUnpatchedSwitch(convertedTargets);
        }

        public void ConvertBranchTargets(uint pc, Dictionary<uint, uint> offsetToInstruction)
        {
            switch(Opcode)
            {
                case HLOpcode.beq:
                case HLOpcode.bge:
                case HLOpcode.bgt:
                case HLOpcode.ble:
                case HLOpcode.blt:
                case HLOpcode.bne:
                case HLOpcode.br:
                case HLOpcode.brfalse:
                case HLOpcode.brtrue:
                case HLOpcode.leave:
                    this.Arguments = new HLArguments(UnpatchInstruction(pc, Arguments.S32Value, offsetToInstruction));
                    break;

                case HLOpcode.@switch:
                    UnpatchSwitch(pc, offsetToInstruction);
                    break;

                default:
                    throw new NotImplementedException("Unknown branch fixup");
            }
        }
    }
}
