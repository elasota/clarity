using System;

namespace AssemblyImporter.CLR
{
    // II.22.34
    public class CLRPropertyRow : CLRTableRow, ICLROwnedBy<CLRPropertyMapRow>, ICLRHasConstant
    {
        public string Name { get; private set; }
        public CLRSigPropertySig Signature { get; private set; }
        public bool SpecialName { get; private set; }
        public bool RTSpecialName { get; private set; }
        public bool HasDefault { get; private set; }
        public CLRConstantRow[] AttachedConstants { get; set; }

        public CLRPropertyMapRow Owner { get; set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            uint flags = parser.ReadU16();
            SpecialName = ((flags & 0x200) != 0);
            RTSpecialName = ((flags & 0x400) != 0);
            HasDefault = ((flags & 0x1000) != 0);

            Name = parser.ReadString();
            Signature = new CLRSigPropertySig(new CLRSignatureParser(parser.ReadBlob(), parser.Tables)); ;
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // CLS requires getter and setter are both virtual or both instance (or both static?)
            // Only one owner row in PropertyMap
            // PropFlags restricted
            // Name is non-empty, valid CLS identifier
            // Signature not null
            // Signature is valid for property, low nibble of lead byte is 0x8 and signature is same as property's get_ method
            // No duplicates based on Name
            // UNSPECIFIED: RTSpecialName implies SpecialName?
        }
    }
}
