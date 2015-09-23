using System;

namespace AssemblyImporter.CLR
{
    public class CLRTable<T> : ICLRTable
        where T : CLRTableRow, new()
    {
        private T[] m_rows;
        private uint m_numRows;
        private CLRMetaData m_metaData;

        public void Init(uint numRows, CLRMetaData metaData)
        {
            m_rows = new T[numRows];
            m_numRows = numRows;
            m_metaData = metaData;

            for (uint i = 0; i < numRows; i++)
            {
                m_rows[i] = new T();
                m_rows[i].Initialize(i, this);
            }
        }

        public void Parse(CLRMetaDataParser parser)
        {
            for (uint i = 0; i < m_rows.LongLength; i++)
                m_rows[i].Parse(parser);

            CLRTableRow nextRow = null;
            for (uint i = 0; i < m_rows.LongLength; i++)
            {
                CLRTableRow thisRow = m_rows[m_rows.LongLength - 1 - i];
                thisRow.ResolveSpans(nextRow, parser);
                nextRow = thisRow;
            }
        }

        CLRTableRow ICLRTable.GetRow(uint row)
        {
            return m_rows[row];
        }

        uint ICLRTable.NumRows
        {
            get
            {
                return m_numRows;
            }
        }

        CLRMetaData ICLRTable.MetaData
        {
            get
            {
                return m_metaData;
            }
        }
    }
}
