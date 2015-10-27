using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class VReg
    {
        public enum UsageEnum
        {
            Local,
            Argument,
            Temporary,
        }

        public VType VType { get; private set; }
        public int Slot { get; private set; }
        public string SlotName { get { return m_slotName; } }
        public string BasicName { get { return m_basicName; } }
        public bool IsAlive { get { return m_isAlive; } }
        public bool IsZombie { get { return m_isZombie; } }
        public VReg LinkedObjectReg { get; set; }
        public CppTraceabilityEnum Traceability { get { return m_traceability; } }
        public UsageEnum Usage { get; private set; }

        private bool m_isAlive;
        private bool m_isZombie;
        private string m_slotName;
        private string m_basicName;
        private CppTraceabilityEnum m_traceability;

        public VReg(CppBuilder builder, string prefix, VType vType, int slot, UsageEnum usage)
        {
            VType = vType;
            Slot = slot;
            Usage = usage;
            m_isAlive = false;
            DetermineTraceability(builder);

            string storagePrefix = "";
            if (m_traceability != CppTraceabilityEnum.NotTraced)
                storagePrefix = "bTracedLocals.";
            m_basicName = prefix + slot.ToString();
            m_slotName = storagePrefix + m_basicName;
        }

        public void Liven()
        {
            if (m_isAlive || m_isZombie)
                throw new Exception("Livened a vreg that's already alive");
            m_isAlive = true;
        }

        public void Kill()
        {
            if (!m_isAlive && !m_isZombie)
                throw new Exception("Killed a vreg that's already dead");
            m_isAlive = false;
            m_isZombie = false;
        }

        private void DetermineTraceability(CppBuilder builder)
        {
            switch (this.VType.ValType)
            {
                case VType.ValTypeEnum.ValueValue:
                    m_traceability = builder.GetCachedTraceability(this.VType.TypeSpec);
                    break;
                case VType.ValTypeEnum.NotNullReferenceValue:
                case VType.ValTypeEnum.NullableReferenceValue:
                case VType.ValTypeEnum.AnchoredManagedPtr:
                    m_traceability = CppTraceabilityEnum.DefinitelyTraced;
                    break;
                case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                    m_traceability = CppTraceabilityEnum.TracedIfMovingGC;
                    break;
                case VType.ValTypeEnum.LocalManagedPtr:
                    m_traceability = CppTraceabilityEnum.NotTraced;
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        public void Zombify()
        {
            if (!m_isAlive || m_isZombie)
                throw new Exception("Zombified a vreg that's not alive");
            m_isZombie = true;
        }
    }
}
