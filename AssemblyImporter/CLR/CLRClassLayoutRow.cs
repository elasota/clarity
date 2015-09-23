using System;

namespace AssemblyImporter.CLR
{
    // II.22.8
    public class CLRClassLayoutRow : CLRTableRow
    {
        public ushort PackingSize { get; private set; }
        public uint ClassSize { get; private set; }
        public CLRTypeDefRow Parent { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            PackingSize = parser.ReadU16();
            ClassSize = parser.ReadU32();
            Parent = (CLRTypeDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.TypeDef);
        }

        public override void Validate()
        {
            // Layout must not start below any class but System.Object
            // Parent is not null, references a Class or ValueType
            // Parent has SequentialLayout or ExplicitLayout
            // If Parent indexes SequentialLayout:
            //     PackingSize is 0, 1, 2, 4, 8, 16, 32, 64, or 128.  0 is default.
            //     If Parent is a ValueType, ClassSize is less than 1MB
            // If Parent indexes ExplicitLayout:
            //     If Parent is a ValueType, ClassSize is less than 1MB
            //     PackingSize is 0.
            //     Layout must be checked for overlap.
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }
    }
}
