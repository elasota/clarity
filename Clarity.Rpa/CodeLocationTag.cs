using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class CodeLocationTag
    {
        private MethodDeclTag m_methodDecl;
        private uint m_offset;

        public uint Offset { get { return m_offset; } }
        public MethodDeclTag MethodDecl { get { return m_methodDecl; } }

        public CodeLocationTag(MethodDeclTag methodDecl, uint offset)
        {
            m_methodDecl = methodDecl;
            m_offset = offset;
        }

        public override string ToString()
        {
            return m_offset.ToString();
        }
    }
}
