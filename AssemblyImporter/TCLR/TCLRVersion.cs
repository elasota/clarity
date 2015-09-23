using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public struct TCLRVersion
    {
        public ushort iMajorVersion;
        public ushort iMinorVersion;
        public ushort iBuildNumber;
        public ushort iRevisionNumber;

        public void Write(BinaryWriter writer)
        {
            writer.Write(iMajorVersion);
            writer.Write(iMinorVersion);
            writer.Write(iBuildNumber);
            writer.Write(iRevisionNumber);
        }
    }
}
