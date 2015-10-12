using System;

namespace AssemblyImporter.CppExport
{
    public class SsaRegister
    {
        private VType m_vType;
        private bool m_isSpilled;
        private int m_debugIndex;
        private object m_constantValue;
        private int m_ssaID;

        public VType VType { get { return m_vType; } }
        public VReg SpillVReg { get; set; }
        public VReg SinglePredecessorSpillVReg { get; set; }

        public int SsaID
        {
            get
            {
                if (m_ssaID == 0)
                    throw new Exception("Uninitialized SSA register used");
                return m_ssaID;
            }

            set
            {
                m_ssaID = value;
            }
        }

        public bool IsSpilled { get { return m_isSpilled; } }

        public SsaRegister(VType vt, int index)
        {
            m_debugIndex = index;
            m_isSpilled = false;
            m_vType = vt;
        }

        private SsaRegister(VType vt, object obj)
        {
            m_vType = vt;
            m_constantValue = obj;
            m_debugIndex = 0;
        }

        public void Spill()
        {
            if (!CppRegisterAllocator.IsVTypeSpillable(m_vType))
                throw new Exception("Spilled an unspillable SSA register");
            m_isSpilled = true;
        }

        public static SsaRegister Constant(VType vt, object obj)
        {
            return new SsaRegister(vt, obj);
        }

        public void TrySpill()
        {
            if (CppRegisterAllocator.IsVTypeSpillable(m_vType))
                m_isSpilled = true;
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
