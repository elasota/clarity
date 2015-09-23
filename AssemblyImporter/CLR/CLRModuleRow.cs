using System;

namespace AssemblyImporter.CLR
{
    public class CLRModuleRow : CLRTableRow
    {
        public string Name { get; private set; }
        public CLRGuid Mvid { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            if (parser.ReadU16() != 0)
                throw new ParseFailedException("Bad Generation value");
            Name = parser.ReadString();
            Mvid = parser.ReadGuid();
            CLRGuid encId = parser.ReadGuid();
            CLRGuid encBaseId = parser.ReadGuid();

            if (encId != null || encBaseId != null)
                throw new ParseFailedException("Unexpected EncId/EncBaseId values");
        }

        public override void Validate()
        {
            if (RowNumber > 0)
                throw new ParseFailedException("More than 1 module");
            if (Name == null || Name == "")
                throw new ParseFailedException("Empty module name");
            if (Mvid == null)
                throw new ParseFailedException("Null Mvid");
        }

        public override bool AllowEmptyTable()
        {
            return false;
        }
    }
}
