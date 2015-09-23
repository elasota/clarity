using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.PE
{
    public class PEOptionalHeader
    {
        public byte MajorLinkerVersion { get; private set; }
        public byte MinorLinkerVersion { get; private set; }
        public uint SizeOfCode { get; private set; }
        public uint SizeOfInitializedData { get; private set; }
        public uint SizeOfUninitializedData { get; private set; }
        public uint AddressOfEntryPoint { get; private set; }
        public uint BaseOfCode { get; private set; }
        public uint BaseOfData { get; private set; }
        public uint ImageBase { get; private set; }
        public uint SectionAlignment { get; private set; }
        public uint FileAlignment { get; private set; }
        public ushort MajorOperatingSystemVersion { get; private set; }
        public ushort MinorOperatingSystemVersion { get; private set; }
        public ushort MajorImageVersion { get; private set; }
        public ushort MinorImageVersion { get; private set; }
        public ushort MajorSubsystemVersion { get; private set; }
        public ushort MinorSubsystemVersion { get; private set; }
        public uint Win32VersionValue { get; private set; }
        public uint SizeOfImage { get; private set; }
        public uint SizeOfHeaders { get; private set; }
        public uint CheckSum { get; private set; }
        public ushort Subsystem { get; private set; }
        public ushort DllCharacteristics { get; private set; }
        public uint SizeOfStackReserve { get; private set; }
        public uint SizeOfStackCommit { get; private set; }
        public uint SizeOfHeapReserve { get; private set; }
        public uint SizeOfHeapCommit { get; private set; }
        public uint LoaderFlags { get; private set; }
        public uint NumberOfRvaAndSizes { get; private set; }
        public RvaAndSize[] DataDirectory { get; private set; }

        public PEOptionalHeader(StreamParser parser)
        {
            ushort magic = parser.ReadU16();
            if (magic != 0x010b)
                throw new ParseFailedException("Bad PE optional header magic");

            MajorLinkerVersion = parser.ReadU8();
            MinorLinkerVersion = parser.ReadU8();
            SizeOfCode = parser.ReadU32();
            SizeOfInitializedData = parser.ReadU32();
            SizeOfUninitializedData = parser.ReadU32();
            AddressOfEntryPoint = parser.ReadU32();
            BaseOfCode = parser.ReadU32();
            BaseOfData = parser.ReadU32();
            ImageBase = parser.ReadU32();
            SectionAlignment = parser.ReadU32();
            FileAlignment = parser.ReadU32();
            MajorOperatingSystemVersion = parser.ReadU16();
            MinorOperatingSystemVersion = parser.ReadU16();
            MajorImageVersion = parser.ReadU16();
            MinorImageVersion = parser.ReadU16();
            MajorSubsystemVersion = parser.ReadU16();
            MinorSubsystemVersion = parser.ReadU16();
            Win32VersionValue = parser.ReadU32();
            SizeOfImage = parser.ReadU32();
            SizeOfHeaders = parser.ReadU32();
            CheckSum = parser.ReadU32();
            Subsystem = parser.ReadU16();
            DllCharacteristics = parser.ReadU16();
            SizeOfStackReserve = parser.ReadU32();
            SizeOfStackCommit = parser.ReadU32();
            SizeOfHeapReserve = parser.ReadU32();
            SizeOfHeapCommit = parser.ReadU32();
            LoaderFlags = parser.ReadU32();
            NumberOfRvaAndSizes = parser.ReadU32();

            DataDirectory = new RvaAndSize[NumberOfRvaAndSizes];
            for (int i = 0; i < DataDirectory.Length; i++)
                DataDirectory[i] = new RvaAndSize(parser);
        }
    }
}
