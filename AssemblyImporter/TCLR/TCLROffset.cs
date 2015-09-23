using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public struct TCLROffset
    {
        ushort value;

        public TCLROffset(uint loc, string subType)
        {
            if (loc >= 0xffff)
                throw new OverflowException("Too many " + subType);
            value = (ushort)loc;
        }

        private TCLROffset(uint loc)
        {
            value = (ushort)loc;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public static TCLROffset Empty
        {
            get
            {
                return new TCLROffset(0xffff);
            }
        }
    }
}
