using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.RpaCompiler
{
    public class RloInterfaceSlot
    {
        public enum ESlotStrength
        {
            DynamicOnly,
            WeakTerminator,
            StrongTerminator,
        }

        private ESlotStrength m_slotStrength;
        private MethodHandle m_method;
        
        public ESlotStrength SlotStrength { get { return m_slotStrength; } }
        public MethodHandle Method { get { return m_method; } }
    }
}
