using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    // II.23.2.4
    public class CLRSigFieldSig
    {
        public CLRSigType Type { get; private set; }
        public CLRSigCustomMod[] CustomMods { get; private set; }

        public CLRSigFieldSig(CLRSignatureParser parser)
        {
            if (parser.NextByte() != 0x6)
                throw new ParseFailedException("Malformed FieldSig");
            parser.ConsumeByte();

            CustomMods = CLRSigType.ReadCustomMods(parser);
            Type = CLRSigType.Parse(parser, false);
        }
    }
}
