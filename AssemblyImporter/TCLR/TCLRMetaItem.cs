using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public abstract class TCLRMetaItem
    {
        public uint RowNumber { get; set; }
        public abstract void Write(BinaryWriter writer);
    }
}
