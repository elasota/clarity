using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.2.10 and II.23.2.11
    public abstract class CLRSigParamOrRetType
    {
        public enum TypeOfTypeEnum
        {
            Value,
            ByRef,
            TypedByRef,
            Sentinel,
        }

        public TypeOfTypeEnum TypeOfType { get; private set; }
        public CLRSigType Type { get; private set; }
        public CLRSigCustomMod[] CustomMods { get; private set; }

        public CLRSigParamOrRetType(CLRSignatureParser parser, bool allowSentinel)
        {
            CustomMods = CLRSigType.ReadCustomMods(parser);

            CLRSignatureParser.Token token = parser.NextToken();

            if (token == CLRSignatureParser.Token.TYPEDBYREF)
            {
                TypeOfType = TypeOfTypeEnum.TypedByRef;
                parser.ConsumeToken();
            }
            else if (token == CLRSignatureParser.Token.SENTINEL)
            {
                if (!allowSentinel)
                    throw new ParseFailedException("Malformed signature");
                TypeOfType = TypeOfTypeEnum.Sentinel;
                parser.ConsumeToken();
            }
            else
            {
                bool allowVoid = AllowVoid;
                if (token == CLRSignatureParser.Token.BYREF)
                {
                    TypeOfType = TypeOfTypeEnum.ByRef;
                    token = parser.NextToken();
                    parser.ConsumeToken();
                    allowVoid = false;
                }
                else
                    TypeOfType = TypeOfTypeEnum.Value;
                Type = CLRSigType.Parse(parser, allowVoid);
            }
        }

        protected abstract bool AllowVoid { get; }
    }
}
