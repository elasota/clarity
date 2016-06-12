using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public interface IDisassemblyWritable
    {
        void WriteDisassembly(DisassemblyWriter dw);
    }
}
