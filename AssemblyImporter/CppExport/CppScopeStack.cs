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

        public CppScopeStack()
        {
            m_regStack = new Stack<Entry>();
            m_indent = "";
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
            if (reg.VType.ValType == VType.ValTypeEnum.Null
                || reg.VType.ValType == VType.ValTypeEnum.ConstantValue
                || reg.VType.ValType == VType.ValTypeEnum.ConstantReference
                || reg.VType.ValType == VType.ValTypeEnum.DelegateVirtualMethod
                || reg.VType.ValType == VType.ValTypeEnum.DelegateSimpleMethod)
                return false;
            return true;
        }

        public void LivenReg(SsaRegister reg, StreamWriter writer)
        {
            Console.WriteLine("Live reg: " + reg.SsaID);
            if (!IsRegScopable(reg))
            {
                writer.WriteLine("value in ssa " + reg.SsaID + " is a constant");
                return;
            }

            writer.Write(m_indent);
            writer.WriteLine("{");

            AddReg(reg);

            writer.Write(m_indent);
            writer.WriteLine("add ssa reg " + reg.VType.TypeSpec + " ssa" + reg.SsaID);
        }

        public void KillReg(SsaRegister reg, StreamWriter writer)
        {
            Console.WriteLine("Kill reg: " + reg.SsaID);
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
