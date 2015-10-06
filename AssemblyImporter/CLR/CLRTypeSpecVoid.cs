using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecVoid : CLRTypeSpec, IEquatable<CLRTypeSpecVoid>
    {
        public CLRTypeSpecVoid()
        {
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecVoid);
        }

        public bool Equals(CLRTypeSpecVoid other)
        {
            return true;
        }

        public override CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return this;
        }

        public override bool UsesGenericParamOfType(CLRSigType.ElementType elementType)
        {
            return false;
        }
    }
}
