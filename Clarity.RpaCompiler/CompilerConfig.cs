using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.RpaCompiler
{
    public class CompilerConfig
    {
        private uint m_nativeIntSizeBits = 32;

        public uint NativeIntSizeBits { get { return m_nativeIntSizeBits; } }
    }
}
