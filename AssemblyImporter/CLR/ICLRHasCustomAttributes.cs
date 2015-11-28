using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyImporter.CLR
{
    public interface ICLRHasCustomAttributes
    {
        CustomAttributeCollection CustomAttributes { get; }
    }
}
