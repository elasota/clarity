using System;

namespace AssemblyImporter.CLR
{
    // II.22.9
    public class CLRConstantRow : CLRTableRow
    {
        public CLRTableRow Parent { get; private set; }
        public byte Type { get; private set; }
        public ArraySegment<byte> Value { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            Type = parser.ReadU8();
            if (parser.ReadU8() != 0)
                throw new ParseFailedException("Pad missing");

            Parent = parser.ReadHasConstant();
            Value = parser.ReadBlob();
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Type must be ELEMENT_TYPE_*:
            // BOOLEAN, CHAR, U1, I2, I4, I8, R4, R8, STRING
            // ... or CLASS with a 4-byte zero value
            // Must not be I1, U2, U4, or U8 (CLS)
            // Parent is not null
            // No duplicates based on Parent
            // Type matches Param, Field or Property (CLS)
        }
    }
}
