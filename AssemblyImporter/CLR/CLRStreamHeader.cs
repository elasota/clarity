using System;

namespace AssemblyImporter.CLR
{
    public class CLRStreamHeader
    {
        public uint Offset { get; private set; }
        public uint Size { get; private set; }
        public string Name { get; private set; }

        public CLRStreamHeader(StreamParser parser)
        {
            Offset = parser.ReadU32();
            Size = parser.ReadU32();
            Name = parser.ReadVarAsciiString(32);
        }
    }
}
