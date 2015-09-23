using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.2.15
    public class CLRSigMethodSpec
    {
        public CLRSigType[] Types { get; private set; }

        public CLRSigMethodSpec(CLRSignatureParser parser)
        {
            if (parser.NextToken() != CLRSignatureParser.Token.GENERICINST)
                throw new ParseFailedException("Malformed method spec");
            parser.ConsumeToken();
            uint numGenericParams = parser.ReadCompressedUInt();
            Types = new CLRSigType[numGenericParams];

            for (uint i = 0; i < numGenericParams; i++)
            {
                Types[i] = CLRSigType.Parse(parser, false);
            }
        }
    }
}
