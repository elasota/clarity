using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public struct TCLRString
    {
        ushort index;

        public TCLRString(uint loc)
        {
            if (loc > 0xffff)
                throw new OverflowException("Too much string data");
            this.index = (ushort)loc;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(index);
        }
    }
}
