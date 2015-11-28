using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public abstract class CLRTypeSpec : IEquatable<CLRTypeSpec>, ICLRHasCustomAttributes
    {
        public abstract CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams);
        public abstract bool UsesGenericParamOfType(CLRSigType.ElementType elementType);
        public override abstract bool Equals(object obj);
        public override abstract int GetHashCode();

        public bool UsesGenericTypeParams { get { return this.UsesGenericParamOfType(CLRSigType.ElementType.VAR); } }
        public bool UsesGenericMethodParams { get { return this.UsesGenericParamOfType(CLRSigType.ElementType.MVAR); } }
        public bool UsesAnyGenericParams { get { return this.UsesGenericTypeParams || this.UsesGenericMethodParams; } }

        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }

        public bool Equals(CLRTypeSpec typeSpec)
        {
            return this.Equals((object)typeSpec);
        }
    }
}
