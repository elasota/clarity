using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public struct TCLRIndex
    {
        ushort value;

        public TCLRIndex(uint loc, string indexType)
        {
            if (loc >= 0xffff)
                throw new OverflowException("Index overflow, too many " + indexType);
            value = (ushort)loc;
        }

        private TCLRIndex(uint loc)
        {
            value = (ushort)loc;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public static TCLRIndex Empty
        {
            get
            {
                return new TCLRIndex(0xffff);
            }
        }
    }
}
