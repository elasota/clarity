using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CppExport
{
    public class EvalStackTracker
    {
        private List<SsaRegister> m_regs;

        public int Depth { get { return m_regs.Count; } }

        public EvalStackTracker()
        {
            m_regs = new List<SsaRegister>();
        }

        public void SpillStack()
        {
            foreach (SsaRegister reg in m_regs)
                reg.Spill();
        }

        public void Push(SsaRegister instanceReg)
        {
            m_regs.Add(instanceReg);
        }

        public SsaRegister Pop()
        {
            int lastIndex = m_regs.Count - 1;
            SsaRegister reg = m_regs[lastIndex];
            m_regs.RemoveAt(lastIndex);
            return reg;
        }

        public void Pop(int count)
        {
            if (count == 0)
                return;
            m_regs.RemoveRange(m_regs.Count - count, count);
        }

        public SsaRegister NewReg(VType vType)
        {
            return new SsaRegister(vType, 0);
        }

        public SsaRegister GetFromTop(int offset)
        {
            return m_regs[m_regs.Count - 1 - offset];
        }

        public VType[] GenerateCfgEdge()
        {
            VType[] types = new VType[m_regs.Count];

            for (int i = 0; i < types.Length; i++)
                types[i] = m_regs[i].VType;
            return types;
        }
    }
}
