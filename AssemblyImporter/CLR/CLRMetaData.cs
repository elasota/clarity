using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRMetaData
    {
        public uint MajorVersion { get; private set; }
        public uint MinorVersion { get; private set; }
        public ushort Flags { get; private set; }
        public string Version { get; private set; }
        public CLRStreamHeader[] StreamHeaders { get; private set; }
        public CLRMetaDataTables MetaDataTables { get; private set; }
        public CLRAssembly Assembly { get; private set; }

        public CLRMetaData(StreamParser parser, CLRAssembly assembly)
        {
            Assembly = assembly;

            long metaRootPosition = parser.Position;
            uint magic = parser.ReadU32();
            if (magic != 0x424a5342)
                throw new ParseFailedException("Bad metadata magic");
            MajorVersion = parser.ReadU16();
            MinorVersion = parser.ReadU16();
            parser.Skip(4);     // Reserved
            uint versionLength = parser.ReadU32();
            if (versionLength > 255)
                throw new ParseFailedException("Oversized section length");
            uint paddedLength = versionLength + 3;
            paddedLength -= paddedLength % 4;
            Version = parser.ReadUTF8String((int)versionLength);
            parser.Skip(paddedLength - versionLength);
            Flags = parser.ReadU16();

            ushort nStreams = parser.ReadU16();
            StreamHeaders = new CLRStreamHeader[nStreams];
            Dictionary<string, CLRStreamHeader> streamHeadersByName = new Dictionary<string, CLRStreamHeader>();
            for (int i = 0; i < nStreams; i++)
            {
                CLRStreamHeader sh = new CLRStreamHeader(parser);
                if (streamHeadersByName.ContainsKey(sh.Name))
                    throw new ParseFailedException("Duplicate metadata stream");
                streamHeadersByName[sh.Name] = sh;
            }

            // Strings US GUID Blob
            CLRMetaStreamBinaryData binData = new CLRMetaStreamBinaryData(parser, metaRootPosition, streamHeadersByName);

            // Parse metadata tables
            parser.Seek(streamHeadersByName["#~"].Offset + metaRootPosition);
            MetaDataTables = new CLRMetaDataTables(parser, this, binData);
        }
    }
}
