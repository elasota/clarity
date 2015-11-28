using System;

namespace AssemblyImporter.CLR
{
    public class CLREventRow : CLRTableRow, ICLRHasCustomAttributes
    {
        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }
    }
}
