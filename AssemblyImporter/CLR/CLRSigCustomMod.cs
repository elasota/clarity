using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRSigCustomMod
    {
        public bool IsOptional { get; private set; }
        public CLRTableRow IndexedType { get; private set; }

        public CLRSigCustomMod(CLRSignatureParser parser)
        {
            byte token = parser.NextByte();
            parser.ConsumeByte();

            if (token == 0x1f)
                IsOptional = false;
            else if (token == 0x20)
                IsOptional = true;
            else
                throw new ParseFailedException("Strange custom mod token");

            IndexedType = parser.ReadTypeDefOrRefOrSpecEncoded();

            throw new NotSupportedException("Custom modifiers are not supported");
        }
    }
}
