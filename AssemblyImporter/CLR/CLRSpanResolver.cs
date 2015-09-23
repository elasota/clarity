using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    public class CLRSpanResolver<TThis, TItem>
        where TThis : CLRTableRow
        where TItem : CLRTableRow, ICLROwnedBy<TThis>
    {
        public delegate uint GetFromNextRowDelegate(TThis nextRow);

        public static TItem[] Resolve(TThis self, CLRTableRow nextRow, ref uint thisAnchor, CLRMetaDataParser parser, CLRMetaDataTables.TableIndex tableIndex, GetFromNextRowDelegate fetchNext)
        {
            uint nextItem;
            if (nextRow == null)
                nextItem = parser.GetTableNumRows(tableIndex) + 1;
            else
                nextItem = fetchNext((TThis)nextRow);

            // Handle null param
            if (thisAnchor == 0)
                thisAnchor = nextItem;

            if (thisAnchor > nextItem)
                throw new ParseFailedException("Span out of order");

            uint numItems = nextItem - thisAnchor;

            TItem[] items = new TItem[numItems];

            for (uint i = 0; i < numItems; i++)
            {
                TItem item = (TItem)parser.GetTableRawRow(tableIndex, i + thisAnchor);
                items[i] = item;
                item.Owner = self;
            }
            return items;
        }
    }
}
