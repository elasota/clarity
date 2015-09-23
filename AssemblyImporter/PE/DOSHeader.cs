using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.PE
{
    public class DOSHeader
    {
        public uint PEHeaderOffset { get; private set; }

        public DOSHeader(StreamParser parser)
        {
            ushort magic = parser.ReadU16();

            if (magic != 0x5a4d)
                throw new ParseFailedException("Invalid DOS header");
            parser.Skip(58);
            PEHeaderOffset = parser.ReadU32();
        }
    }
}
