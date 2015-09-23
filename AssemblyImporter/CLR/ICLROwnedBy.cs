using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public interface ICLROwnedBy<T>
    {
        T Owner { get; set; }
    }
}
