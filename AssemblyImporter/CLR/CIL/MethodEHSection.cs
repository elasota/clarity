using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR.CIL
{
    public class MethodEHSection : MethodDataSection
    {
        public MethodEHClause[] Clauses { get; private set; }

        public MethodEHSection(CLRMetaDataParser parser, Dictionary<uint, uint> offsetToInstruction, bool isFat)
        {
            uint blockSize;
            blockSize = parser.ReadU8();
            blockSize |= (uint)parser.ReadU16() << 8;

            if (isFat == false && blockSize > 255)
                throw new ParseFailedException("Malformed EH section");

            if (blockSize < 4)
                throw new ParseFailedException("Malformed EH section");
            uint clauseBlockSize = blockSize - 4;

            uint clauseSize = (uint)(isFat ? 24 : 12);
            uint numClauses = clauseBlockSize / clauseSize;
            if (numClauses * clauseSize != clauseBlockSize)
                throw new ParseFailedException("Malformed EH section");

            Clauses = new MethodEHClause[numClauses];
            for (uint i = 0; i < numClauses; i++)
                Clauses[i] = new MethodEHClause(parser, offsetToInstruction, isFat);
        }
    }
}
