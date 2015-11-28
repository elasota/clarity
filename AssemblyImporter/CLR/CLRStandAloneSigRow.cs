using System;

namespace AssemblyImporter.CLR
{
    // II.22.36
    public class CLRStandAloneSigRow : CLRTableRow, ICLRHasCustomAttributes
    {
        public ArraySegment<byte> Signature { get; private set; }

        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }

        public override void Parse(CLRMetaDataParser parser)
        {
            Signature = parser.ReadBlob();
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Signature must be a valid METHOD or LOCALS signature
            // Duplicates are allowed
        }
    }
}
