using System;

namespace AssemblyImporter.CLR
{
    public class CLRInterfaceImplRow : CLRTableRow
    {
        public CLRTypeDefRow Class { get; private set; }
        public CLRTableRow Interface { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            Class = (CLRTypeDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.TypeDef);
            Interface = parser.ReadTypeDefOrRefOrSpec();
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Class is not null
            // Interface is an interface
            // No duplicates based on Class+Interface
        }
    }
}
