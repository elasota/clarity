using System;

namespace AssemblyImporter.CLR
{
    public abstract class CLRTableRow
    {
        public uint RowNumber { get; private set; }
        public uint TableNumber { get; private set; }
        public ICLRTable Table { get; private set; }

        public uint MetadataToken { get { return (TableNumber << 24) | (RowNumber + 1); } }

        public void Initialize(uint tableNumber, uint rowNumber, ICLRTable table)
        {
            RowNumber = rowNumber;
            Table = table;
            TableNumber = tableNumber;
        }

        public virtual void Parse(CLRMetaDataParser parser)
        {
            throw new NotImplementedException();
        }

        public virtual void ResolveSpans(CLRTableRow nextRow, CLRMetaDataParser parser)
        {
        }

        public virtual void Validate()
        {
            throw new NotImplementedException();
        }

        public virtual bool AllowEmptyTable()
        {
            return true;
        }
    }
}
