using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRMetaStreamBinaryData
    {
        public byte[] BlobData { get; private set; }
        public byte[] GuidData { get; private set; }
        public byte[] USData { get; private set; }
        public byte[] StringData { get; private set; }

        private byte[] LoadStream(StreamParser parser, long metaRootPosition, string key, Dictionary<string, CLRStreamHeader> shByName)
        {
            CLRStreamHeader header = null;
            if (!shByName.TryGetValue(key, out header))
                throw new ParseFailedException("Missing metadata stream " + shByName);
            parser.Seek(metaRootPosition + header.Offset);
            byte[] bytes = new byte[header.Size];
            parser.Read(bytes, 0, header.Size);
            return bytes;
        }

        public CLRMetaStreamBinaryData(StreamParser parser, long metaRootPosition, Dictionary<string, CLRStreamHeader> shByName)
        {
            BlobData = LoadStream(parser, metaRootPosition, "#Blob", shByName);
            GuidData = LoadStream(parser, metaRootPosition, "#GUID", shByName);
            USData = LoadStream(parser, metaRootPosition, "#US", shByName);
            StringData = LoadStream(parser, metaRootPosition, "#Strings", shByName);
        }
    }
}
