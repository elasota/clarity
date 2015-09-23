using System;

namespace AssemblyImporter.CLR
{
    // II.22.36
    public class CLRPropertyMapRow : CLRTableRow
    {
        public CLRTypeDefRow Parent { get; private set; }
        public CLRPropertyRow[] PropertyList { get; private set; }

        private uint m_firstProperty;

        public override void Parse(CLRMetaDataParser parser)
        {
            Parent = (CLRTypeDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.TypeDef);
            m_firstProperty = parser.ReadTableRawRow(CLRMetaDataTables.TableIndex.Property);
        }

        public override void ResolveSpans(CLRTableRow nextRow, CLRMetaDataParser parser)
        {
            PropertyList = CLRSpanResolver<CLRPropertyMapRow, CLRPropertyRow>.Resolve(this, nextRow, ref m_firstProperty, parser,
                CLRMetaDataTables.TableIndex.Property, nextRowTyped => { return nextRowTyped.m_firstProperty; });
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // No duplicates based on Parent
        }
    }
}
