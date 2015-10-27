using System;
using System.Collections.Generic;

namespace AssemblyImporter.CppExport
{
    public class CppVtableSlotOverrideImpl
    {
        private CppVtableSlot m_declSlot;
        private CppVtableSlot m_bodySlot;

        public CppVtableSlotOverrideImpl(CppVtableSlot declSlot, CppVtableSlot bodySlot)
        {
            m_declSlot = declSlot;
            m_bodySlot = bodySlot;
        }
    }
}
