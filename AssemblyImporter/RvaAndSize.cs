using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter
{
    public struct RvaAndSize
    {
        private uint m_rva;
        private uint m_size;

        public uint RelativeVirtualAddress { get { return m_rva; } }
        public uint Size { get { return m_size; } }

        public RvaAndSize(StreamParser parser)
        {
            m_rva = parser.ReadU32();
            m_size = parser.ReadU32();
        }
    }
}
