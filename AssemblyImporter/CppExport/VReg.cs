using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class VReg
    {
        public VType VType { get; private set; }
        public int Slot { get; private set; }
        public string SlotName { get { return m_prefix + Slot.ToString(); } }
        public bool IsAlive { get { return m_isAlive; } }
        public VReg LinkedObjectReg { get; set; }

        private bool m_isAlive;
        private string m_prefix;

        public VReg(string prefix, VType vType, int slot)
        {
            VType = vType;
            Slot = slot;
            m_isAlive = false;
            m_prefix = prefix;
        }

        public void Liven()
        {
            m_isAlive = true;
        }

        public void Deaden()
        {
            m_isAlive = false;
        }
    }
}
