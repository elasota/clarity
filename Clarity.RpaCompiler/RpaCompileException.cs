using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.RpaCompiler
{
    public class RpaCompileException : Exception
    {
        public RpaCompileException(string msg)
            : base(msg)
        {
        }
    }
}
