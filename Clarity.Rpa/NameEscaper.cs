using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.Rpa
{
    public class NameEscaper
    {
        public static string EscapeName(string str)
        {
            return "\"" + str + "\"";
        }
    }
}
