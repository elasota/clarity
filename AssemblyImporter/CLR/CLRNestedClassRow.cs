using System;

namespace AssemblyImporter.CLR
{
    public class CLRNestedClassRow : CLRTableRow
    {
        public CLRTypeDefRow NestedClass { get; private set; }
        public CLRTypeDefRow EnclosingClass { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            NestedClass = (CLRTypeDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.TypeDef);
            EnclosingClass = (CLRTypeDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.TypeDef);
        }

        public override void Validate()
        {
            // Nested class is valid
            // EnclosingClass is valid
            // Type can only be nested by one encloser
        }
    }
}
