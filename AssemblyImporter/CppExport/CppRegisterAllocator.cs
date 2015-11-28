using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppRegisterAllocator
    {
        private List<VReg> m_registers;
        private int m_ssaID;
        private int m_cfgID;
        private Dictionary<CfgNode, int> m_cfgNodeIDs;
        private CppBuilder m_builder;
        private List<CLR.CLRTypeSpec> m_staticInitTokens;

        public IEnumerable<VReg> AllRegisters { get { return m_registers; } }

        public int NumStaticTokens
        {
            get
            {
                return m_staticInitTokens.Count;
            }
        }

        public CppRegisterAllocator(CppBuilder builder)
        {
            m_registers = new List<VReg>();
            m_ssaID = 1;
            m_cfgNodeIDs = new Dictionary<CfgNode, int>();
            m_builder = builder;
            m_staticInitTokens = new List<CLR.CLRTypeSpec>();
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
                // These types have explicit representations in exported C++
                case VType.ValTypeEnum.ValueValue:
                case VType.ValTypeEnum.AnchoredManagedPtr:
                case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                case VType.ValTypeEnum.LocalManagedPtr:
                case VType.ValTypeEnum.NullableReferenceValue:
                    storageType = vType.ValType;
                    break;
                // These are only differ from normally spillable types in semantics
                case VType.ValTypeEnum.NotNullReferenceValue:
                    storageType = VType.ValTypeEnum.NullableReferenceValue;
                    break;
                default:
                    throw new ArgumentException("Illegal register allocation");
            }
            return new VType(storageType, vType.TypeSpec);
        }

        private VReg NewDeadReg(VType vType, VReg.UsageEnum usage)
        {
            VReg newReg = new VReg(m_builder, "bVReg", vType, m_registers.Count, usage);
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
                resultReg = NewDeadReg(regVType, VReg.UsageEnum.Temporary);

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
                inputType.ValType == VType.ValTypeEnum.Null ||
                inputType.ValType == VType.ValTypeEnum.DelegateSimpleMethod ||
                inputType.ValType == VType.ValTypeEnum.DelegateVirtualMethod)
                return false;
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
                    VReg newReg = NewDeadReg(regVType, VReg.UsageEnum.Temporary);
                    regs.Add(newReg);
                    markedRegSet.Add(newReg);
                }
            }

            return regs.ToArray();
        }

        public int AllocStaticToken(CLRTypeSpec typeSpecArg)
        {
            for (int i = 0; i < m_staticInitTokens.Count; i++)
            {
                if (m_staticInitTokens[i].Equals(typeSpecArg))
                    return i;
            }
            m_staticInitTokens.Add(typeSpecArg);
            return m_staticInitTokens.Count - 1;
        }

        public CLRTypeSpec GetStaticToken(int index)
        {
            return m_staticInitTokens[index];
        }
    }
}
