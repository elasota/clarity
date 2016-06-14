using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighMethodBodyParseContext
    {
        private HighLocal m_instanceLocal;
        private HighLocal[] m_args;
        private HighLocal[] m_locals;

        public HighLocal[] Args { get { return m_args; } }
        public HighLocal[] Locals { get { return m_locals; } }

        public HighMethodBodyParseContext(HighLocal instanceLocal, HighLocal[] args, HighLocal[] locals)
        {
            m_instanceLocal = instanceLocal;
            m_args = args;
            m_locals = locals;
        }


        public HighLocal GetLocal(uint v)
        {
            if (m_instanceLocal != null)
            {
                if (v == 0)
                    return m_instanceLocal;
                v--;
            }

            uint numArgs = (uint)m_args.Length;
            if (v < numArgs)
                return m_args[v];
            v -= numArgs;

            return m_locals[v];
        }
    }
}
