using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class RpaLoadException : Exception
    {
        public RpaLoadException(string msg)
            : base(msg)
        {
        }
    }
}
