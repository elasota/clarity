using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.2.6
    public class CLRSigLocalVarSig
    {
        public CLRSigLocalVar[] LocalVars { get; private set; }

        public CLRSigLocalVarSig(CLRSignatureParser parser)
        {
            if (parser.NextToken() != CLRSignatureParser.Token.LOCAL_SIG)
                throw new ParseFailedException("Invalid local var sig");
            parser.ConsumeToken();

            uint numLocals = parser.ReadCompressedUInt();

            LocalVars = new CLRSigLocalVar[numLocals];
            for (uint i = 0; i < numLocals; i++)
                LocalVars[i] = new CLRSigLocalVar(parser);
        }
    }
}
