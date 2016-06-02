using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class RloClass
    {
        private bool m_isSealed;
        private RloClass m_parentClass;
        private TypeSpecTag m_typeSpec;
        private TypeSpecTag m_cliTypeSpec;
        private MethodHandle[] m_vtable;

        public RloClass()
        {
        }
    }
}
