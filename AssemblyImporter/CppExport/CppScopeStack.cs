using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.CppExport
{
    public class CppScopeStack
    {
        private class Entry
        {
            public SsaRegister SsaReg { get { return m_reg; } }
            public bool IsAlive { get { return m_isAlive; } }

            private SsaRegister m_reg;
            private bool m_isAlive;

            public Entry(SsaRegister reg)
            {
                m_isAlive = true;
                m_reg = reg;
            }

            public void Kill()
            {
                m_isAlive = false;
            }
        }

        private Stack<Entry> m_regStack;
        private string m_indent;

        public string Indent { get { return m_indent; } }

        public CppScopeStack(int indentLevels)
        {
            m_regStack = new Stack<Entry>();
            m_indent = "";
            for (int i = 0; i < indentLevels; i++)
                m_indent += "\t";
        }

        private void AddReg(SsaRegister reg)
        {
            m_regStack.Push(new Entry(reg));
            m_indent += "\t";
        }

        private static bool IsRegScopable(SsaRegister reg)
        {
            if (reg.IsSpilled)
                return false;
            return CppRegisterAllocator.IsVTypeSpillable(reg.VType);
        }

        public void LivenReg(SsaRegister reg, CppBuilder builder)
        {
            if (!IsRegScopable(reg))
                return;

            AddReg(reg);
        }

        public void KillReg(SsaRegister reg, StreamWriter writer)
        {
            if (!IsRegScopable(reg))
                return;

            bool anyMatch = false;
            foreach (Entry e in m_regStack)
            {
                if (e.SsaReg == reg)
                {
                    e.Kill();
                    anyMatch = true;
                    break;
                }
            }

            if (!anyMatch)
                throw new Exception("Killed an untracked register");

            while (m_regStack.Count > 0)
            {
                Entry top = m_regStack.Peek();
                if (top.IsAlive)
                    break;

                m_regStack.Pop();

                m_indent = m_indent.Substring(0, m_indent.Length - 1);
                writer.Write(m_indent);
                writer.WriteLine("}");
            }
        }

        public void RecycleReg(SsaRegister reg)
        {
            AddReg(reg);
        }
    }
}
