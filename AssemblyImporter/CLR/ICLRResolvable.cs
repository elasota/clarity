using System;

namespace AssemblyImporter.CLR
{
    public interface ICLRResolvable
    {
        void Resolve(CLRAssemblyCollection assemblies);
        bool IsResolved { get; }
    }
}
