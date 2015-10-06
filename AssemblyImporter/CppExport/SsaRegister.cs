using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CppExport
{
    public class SsaRegister
    {
        private VType m_vType;
        private bool m_isSpilled;
        private int m_index;
        private object m_constantValue;

        public VType VType { get { return m_vType; } }

        public SsaRegister(VType vt, int index)
        {
            m_index = index;
            m_isSpilled = false;
            m_vType = vt;
        }

        private SsaRegister(VType vt, object obj)
        {
            m_vType = vt;
            m_constantValue = obj;
            m_index = 0;
        }

        public void Spill()
        {
            m_isSpilled = true;
        }

        public static SsaRegister Constant(VType vt, object obj)
        {
            return new SsaRegister(vt, obj);
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
