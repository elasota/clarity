using System;

namespace AssemblyImporter.CLR
{
    public class CLRFieldRVARow : CLRTableRow
    {
        public uint RVA { get; private set; }
        public CLRFieldRow Field { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            RVA = parser.ReadU32();
            Field = (CLRFieldRow)parser.ReadTable(CLRMetaDataTables.TableIndex.Field);
        }

        public override void Validate()
        {
            // RVA points into module data
            // Field is not null
            // Fields with RVA:
            //     Must be ValueType
            //     Must not have any private fields, or ValueTypes with private fields
            //     Must not contain object references
        }
    }
}
