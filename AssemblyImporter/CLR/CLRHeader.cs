using System;

namespace AssemblyImporter.CLR
{
    public class CLRHeader
    {
        public uint HeaderSize { get; private set; }
        public ushort MajorRuntimeVersion { get; private set; }
        public ushort MinorRuntimeVersion { get; private set; }
        public RvaAndSize MetaData { get; private set; }
        public uint Flags { get; private set; }
        public uint EntryPointToken { get; private set; }
        public RvaAndSize Resources { get; private set; }
        public RvaAndSize StrongNameSignature { get; private set; }
        public RvaAndSize CodeManagerTable { get; private set; }
        public RvaAndSize VTableFixups { get; private set; }
        public RvaAndSize ExportAddressTableJumps { get; private set; }
        public RvaAndSize ManagedNativeHeader { get; private set; }

        public bool Is32BitRequired { get { return (Flags & 0x00000002) != 0; } }
        public bool IsStrongNameSigned { get { return (Flags & 0x00000008) != 0; } }
        public bool ILOnly { get { return (Flags & 0x00000001) != 0; } }

        public CLRHeader(StreamParser parser)
        {
            parser.Skip(8);
            HeaderSize = parser.ReadU32();
            if (HeaderSize != 0x48)
                throw new ParseFailedException("Invalid CLI header");

            MajorRuntimeVersion = parser.ReadU16();
            MinorRuntimeVersion = parser.ReadU16();
            MetaData = new RvaAndSize(parser);
            Flags = parser.ReadU32();
            EntryPointToken = parser.ReadU32();
            Resources = new RvaAndSize(parser);
            StrongNameSignature = new RvaAndSize(parser);
            CodeManagerTable = new RvaAndSize(parser);
            VTableFixups = new RvaAndSize(parser);
            ExportAddressTableJumps = new RvaAndSize(parser);
            ManagedNativeHeader = new RvaAndSize(parser);

            if (EntryPointToken != 0)
                throw new NotImplementedException();    // Need to figure this out
        }
    }
}
