using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public struct TCLROffsetLong
    {
        uint value;

        public TCLROffsetLong(uint v)
        {
            value = v;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}
