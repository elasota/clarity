using System;

namespace AssemblyImporter.CLR
{
    public interface ICLRTable
    {
        void Init(uint numRows, CLRMetaData metaData);
        CLRTableRow GetRow(uint row);
        uint NumRows { get; }
        void Parse(CLRMetaDataParser parser);
        CLRMetaData MetaData { get; }
    }
}
