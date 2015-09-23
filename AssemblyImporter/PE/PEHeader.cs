using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.PE
{
    public class PEHeader
    {
        public ushort Machine { get; private set; }
        public ushort NumberOfSections { get; private set; }
        public uint TimeDateStamp { get; private set; }
        public uint PointerToSymbolTable { get; private set; }
        public uint NumberOfSymbols { get; private set; }
        public ushort SizeOfOptionalHeader { get; private set; }
        public ushort Characteristics { get; private set; }

        public PEHeader(StreamParser parser)
        {
            uint peSig = parser.ReadU32();
            if (peSig != 0x00004550)
                throw new ParseFailedException("Bad PE signature");
            
            Machine = parser.ReadU16();
            NumberOfSections = parser.ReadU16();
            TimeDateStamp = parser.ReadU32();
            PointerToSymbolTable = parser.ReadU32();
            NumberOfSymbols = parser.ReadU32();
            SizeOfOptionalHeader = parser.ReadU16();
            Characteristics = parser.ReadU16();
        }
    }
}
