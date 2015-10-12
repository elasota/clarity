using System;
using System.Collections.Generic;

namespace AssemblyImporter.CppExport
{
    public class CppRegisterAllocator
    {
        private List<VReg> m_registers;
        private int m_ssaID;
        private int m_cfgID;
        private Dictionary<CfgNode, int> m_cfgNodeIDs;

        public IEnumerable<VReg> AllRegisters { get { return m_registers; } }

        public CppRegisterAllocator()
        {
            m_registers = new List<VReg>();
            m_ssaID = 1;
            m_cfgNodeIDs = new Dictionary<CfgNode, int>();
        }

        public int NewSsaID()
        {
            return m_ssaID++;
        }

        private static VType StrictStorageType(VType vType)
        {
            VType.ValTypeEnum storageType;
            switch (vType.ValType)
            {
                case VType.ValTypeEnum.ValueValue:
                    storageType = VType.ValTypeEnum.ValueValue;
                    break;
                case VType.ValTypeEnum.NotNullReferenceValue:
                case VType.ValTypeEnum.NullableReferenceValue:
                    storageType = VType.ValTypeEnum.NullableReferenceValue;
                    break;
                case VType.ValTypeEnum.ManagedPtr:
                    storageType = VType.ValTypeEnum.ManagedPtr;
                    break;
                default:
                    throw new ArgumentException("Illegal register allocation");
            }
            return new VType(storageType, vType.TypeSpec);
        }

        private VReg NewDeadReg(VType vType)
        {
            VReg newReg = new VReg("VReg", vType, m_registers.Count);
            m_registers.Add(newReg);
            return newReg;
        }

        public VReg AllocReg(VType vType)
        {
            VType regVType = StrictStorageType(vType);

            VReg resultReg = null;
            foreach (VReg reg in m_registers)
            {
                if (!reg.IsAlive && reg.VType.Equals(regVType))
                {
                    resultReg = reg;
                    break;
                }
            }

            if (resultReg == null)
                resultReg = NewDeadReg(regVType);

            resultReg.Liven();
            return resultReg;
        }

        public int TargetIDForCfgNode(CfgNode cfgNode)
        {
            int targetID;
            if (m_cfgNodeIDs.TryGetValue(cfgNode, out targetID))
                return targetID;
            targetID = m_cfgID++;
            m_cfgNodeIDs.Add(cfgNode, targetID);
            return targetID;
        }

        public static bool IsVTypeSpillable(VType inputType)
        {
            if (inputType.ValType == VType.ValTypeEnum.ConstantReference ||
                inputType.ValType == VType.ValTypeEnum.ConstantValue ||
                inputType.ValType == VType.ValTypeEnum.Null)
                return false;
            if (inputType.ValType == VType.ValTypeEnum.DelegateSimpleMethod ||
                inputType.ValType == VType.ValTypeEnum.DelegateVirtualMethod)
                throw new Exception("Delegate binding crossed a CFG edge");
            return true;
        }

        public VReg[] TargetRegsForCfgInput(VType[] inputTypes)
        {
            List<VReg> regs = new List<VReg>();
            HashSet<VReg> markedRegSet = new HashSet<VReg>();

            foreach (VType inputType in inputTypes)
            {
                if (!IsVTypeSpillable(inputType))
                {
                    regs.Add(null);
                    continue;   // Don't allocate registers for constants
                }

                VType regVType = StrictStorageType(inputType);

                bool matched = false;
                for (int regSlot = 0; regSlot < m_registers.Count; regSlot++)
                {
                    VReg candidateReg = m_registers[regSlot];
                    if (!candidateReg.VType.Equals(regVType))
                        continue;

                    if (markedRegSet.Contains(candidateReg))
                        continue;

                    markedRegSet.Add(candidateReg);
                    regs.Add(candidateReg);
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    VReg newReg = NewDeadReg(regVType);
                    regs.Add(newReg);
                    markedRegSet.Add(newReg);
                }
            }

            return regs.ToArray();
        }
    }
}
