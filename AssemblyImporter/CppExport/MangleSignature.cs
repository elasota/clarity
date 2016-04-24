using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyImporter.CppExport
{
    public abstract class MangleSignature : IEquatable<MangleSignature>
    {
        public abstract override string ToString();
        public abstract bool Equals(MangleSignature other);
    }
}
