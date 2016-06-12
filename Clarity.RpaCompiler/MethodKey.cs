using System;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public abstract class MethodKey : IEquatable<MethodKey>, Rpa.IDisassemblyWritable
    {
        public abstract override int GetHashCode();
        public abstract bool Equals(MethodKey other);

        public override bool Equals(object obj)
        {
            MethodKey tOther = obj as MethodKey;

            if (obj == null)
                return false;
            return this.Equals(tOther);
        }

        public abstract RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath);
        public abstract void WriteDisassembly(DisassemblyWriter dw);
    }
}
