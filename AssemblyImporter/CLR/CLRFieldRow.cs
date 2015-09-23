using System;

namespace AssemblyImporter.CLR
{
    // II.22.15
    public class CLRFieldRow : CLRTableRow, ICLROwnedBy<CLRTypeDefRow>
    {
        public enum TypeFieldAccess
        {
            CompilerControlled = 0x0,
            Private = 0x1,
            FamilyAndAssembly = 0x2,
            Assembly = 0x3,
            Family = 0x4,
            FamilyOrAssembly = 0x5,
            Public = 0x6,
        }

        public TypeFieldAccess FieldAccess { get; private set; }
        public bool Static { get; private set; }
        public bool InitOnly { get; private set; }
        public bool Literal { get; private set; }
        public bool NotSerialized { get; private set; }
        public bool SpecialName { get; private set; }
        public bool PInvokeImpl { get; private set; }
        public bool RTSpecialName { get; private set; }
        public bool HasFieldMarshal { get; private set; }
        public bool HasDefault { get; private set; }
        public bool HasFieldRVA { get; private set; }
        public string Name { get; private set; }
        public CLRSigFieldSig Signature { get; private set; }

        public CLRTypeDefRow Owner { get; set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            ushort flags = parser.ReadU16();
            int accessFlags = (flags & 0x7);
            if (accessFlags > 0x6)
                throw new ParseFailedException("Unknown type field access");

            FieldAccess = (TypeFieldAccess)accessFlags;
            Static = ((flags & 0x10) != 0);
            InitOnly = ((flags & 0x20) != 0);
            Literal = ((flags & 0x40) != 0);
            NotSerialized = ((flags & 0x80) != 0);
            SpecialName = ((flags & 0x200) != 0);
            PInvokeImpl = ((flags & 0x2000) != 0);
            RTSpecialName = ((flags & 0x400) != 0);
            HasFieldMarshal = ((flags & 0x1000) != 0);
            HasDefault = ((flags & 0x8000) != 0);
            HasFieldRVA = ((flags & 0x100) != 0);

            Name = parser.ReadString();
            Signature = new CLRSigFieldSig(new CLRSignatureParser(parser.ReadBlob(), parser.Tables)); ;
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Only one owner from TypeDef
            // Owner must not be an interface
            // Flags only what are specified
            // Literal and InitOnly are mutually exclusive
            // If Literal = 1, then Static = 1
            // If RTSpecialName = 1, then SpecialName = 1
            // If HasFieldMarshal, owns one row in FieldMarshal
            // If HasDefault, owns one row in Constant
            // If HasFieldRVA, owns one row in FieldRVA
            // Name is non-empty, valid CLS identifier
            // Signature is valid field signature
            // If CompilerControlled, no dupe checks
            // Not owned by <Module>
            // No duplicate rows based on owner+Name
            // If Enum:
            //     Owner row derives from System.Enum
            //     Owner row in TypeDef has no other instance fields
            //     Signature is ELEMENT_TYPE_U1, ELEMENT_TYPE_I2, ELEMENT_TYPE_I4, or ELEMENT_TYPE_I8
            // Signature is integral type
        }
    }
}
