using System;

namespace AssemblyImporter.CLR
{
    // II.23.2.5
    public class CLRSigPropertySig
    {
        public bool HasThis { get; private set; }
        public CLRSigCustomMod[] CustomMods { get; private set; }
        public CLRSigType Type { get; private set; }
        public CLRSigParamType[] Parameters { get; private set; }

        public CLRSigPropertySig(CLRSignatureParser parser)
        {
            CLRSignatureParser.Token headToken = parser.NextToken();
            parser.ConsumeToken();

            if (headToken == CLRSignatureParser.Token.PROPERTY_HASTHIS)
                HasThis = true;
            else if (headToken == CLRSignatureParser.Token.PROPERTY)
                HasThis = false;
            else
                throw new ParseFailedException("Malformed PropertySig");

            uint paramCount = parser.ReadCompressedUInt();
            CustomMods = CLRSigType.ReadCustomMods(parser);

            Type = CLRSigType.Parse(parser, false);

            Parameters = new CLRSigParamType[paramCount];
            for (uint i = 0; i < paramCount; i++)
                Parameters[i] = new CLRSigParamType(parser, false);
        }
    }
}
