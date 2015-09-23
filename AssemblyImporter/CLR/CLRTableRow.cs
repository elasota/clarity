using System;

namespace AssemblyImporter.CLR
{
    public abstract class CLRTableRow
    {
        public uint RowNumber { get; private set; }
        public ICLRTable Table { get; private set; }

        public void Initialize(uint rowNumber, ICLRTable table)
        {
            RowNumber = rowNumber;
            Table = table;
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
