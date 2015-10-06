using System;

namespace AssemblyImporter.CLR
{
    public interface ICLRHasConstant
    {
        CLRConstantRow[] AttachedConstants { get; set; }
    }
}
