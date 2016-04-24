using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyImporter.CppExport
{
    public class CppCfgNodeOutline
    {
        private Dictionary<SsaRegister, int> m_ssaRegisterLookup = new Dictionary<SsaRegister, int>();
        private List<SsaRegister> m_ssaRegisters = new List<SsaRegister>();

        public void AddRegister(SsaRegister reg)
        {
            m_ssaRegisterLookup[reg] = m_ssaRegisters.Count;
            m_ssaRegisters.Add(reg);
        }

        public CppCfgNodeOutline(CfgNode cfgNode)
        {
            foreach (MidInstruction instr in cfgNode.MidInstructions)
            {
                if (instr.Opcode == MidInstruction.OpcodeEnum.EntryReg
                    || instr.Opcode == MidInstruction.OpcodeEnum.LivenReg)
                    AddRegister(instr.RegArg);
            }
        }

        public int GetRegisterID(SsaRegister reg)
        {
            return m_ssaRegisterLookup[reg];
        }
    }
}
