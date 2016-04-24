using System;

namespace AssemblyImporter.CppExport
{
    public class SsaRegister
    {
        private VType m_vType;
        private bool m_isSpilled;
        private int m_ssaID;
        private bool m_isUsable;

        public VType VType { get { return m_vType; } }
        public VReg SpillVReg { get; set; }
        public VReg SinglePredecessorSpillVReg { get; set; }
        public object ConstantValue { get { return m_vType.ConstantValue; } }

        public int SsaID
        {
            get
            {
                if (!m_isUsable)
                    throw new Exception("Unusable SSA register used");
                return m_ssaID;
            }
        }

        public int NonUseSsaID
        {
            get
            {
                if (m_ssaID == 0)
                    throw new Exception("Uninitialized SSA register ID read");
                return m_ssaID;
            }
        }

        public bool IsSpilled { get { return m_isSpilled; } }

        public SsaRegister(VType vt)
        {
            m_isSpilled = false;
            m_vType = vt;
        }

        public void Spill()
        {
            if (!CppRegisterAllocator.IsVTypeSpillable(m_vType))
                throw new Exception("Spilled an unspillable SSA register");
            m_isSpilled = true;
        }

        public static SsaRegister Constant(VType vt)
        {
            if (vt.ConstantValue != null && vt.ConstantValue.GetType() == typeof(ulong))
                throw new Exception();
            return new SsaRegister(vt);
        }

        public void TrySpill()
        {
            if (CppRegisterAllocator.IsVTypeSpillable(m_vType))
                m_isSpilled = true;
        }

        public void GenerateUniqueID(CppRegisterAllocator regAllocator)
        {
            if (m_ssaID == 0)
                m_ssaID = regAllocator.NewSsaID();
        }

        public void MakeUsable()
        {
            if (m_isUsable)
                throw new Exception("Reg was marked usable multiple times");
            m_isUsable = true;
        }
    }

    /*
    public class RegisterSet
    {
        private List<VReg> m_typeSpecs;
        private string m_prefix;

        public RegisterSet(string prefix)
        {
            m_typeSpecs = new List<VReg>();
            m_prefix = prefix;
        }

        public VReg AllocRegister(VType vt)
        {
            foreach (VReg reg in m_typeSpecs)
            {
                if (reg.VType.Equals(vt))
                {
                    reg.Liven();
                    return reg;
                }
            }

            int regSlot = m_typeSpecs.Count;
            VReg newReg = new VReg(m_prefix, vt, regSlot);
            m_typeSpecs.Add(newReg);
            return newReg;
        }
    }
     * */
}
