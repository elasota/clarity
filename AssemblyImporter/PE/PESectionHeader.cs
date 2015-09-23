using System;

namespace AssemblyImporter.PE
{
    public class PESectionHeader
    {
        public string Name { get; private set; }
        public uint Misc { get; private set; }
        public uint VirtualAddress { get; private set; }
        public uint SizeOfRawData { get; private set; }
        public uint PointerToRawData { get; private set; }
        public uint PointerToRelocations { get; private set; }
        public uint PointerToLinenumbers { get; private set; }
        public ushort NumberOfRelocations { get; private set; }
        public ushort NumberOfLinenumbers { get; private set; }
        public uint Characteristics { get; private set; }

        public uint PhysicalAddress { get { return Misc; } }
        public uint VirtualSize { get { return Misc; } }

        public PESectionHeader(StreamParser parser)
        {
            Name = parser.ReadUTF8String(8);

            Misc = parser.ReadU32();
            VirtualAddress = parser.ReadU32();
            SizeOfRawData = parser.ReadU32();
            PointerToRawData = parser.ReadU32();
            PointerToRelocations = parser.ReadU32();
            PointerToLinenumbers = parser.ReadU32();
            NumberOfRelocations = parser.ReadU16();
            NumberOfLinenumbers = parser.ReadU16();
            Characteristics = parser.ReadU32();
        }
    }
}
