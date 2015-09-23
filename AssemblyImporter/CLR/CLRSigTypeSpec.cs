using System;

namespace AssemblyImporter.CLR
{
    // II.23.2.14
    public class CLRSigTypeSpec
    {
        public CLRSigType Type { get; private set; }

        public CLRSigTypeSpec(CLRSignatureParser parser)
        {
            byte token = parser.NextByte();

            if (token != (byte)CLRSigType.ElementType.PTR &&
                token != (byte)CLRSigType.ElementType.FNPTR &&
                token != (byte)CLRSigType.ElementType.ARRAY &&
                token != (byte)CLRSigType.ElementType.SZARRAY &&
                token != (byte)CLRSigType.ElementType.GENERICINST)
                throw new ParseFailedException("Invalid sig type for type spec");

            Type = CLRSigType.Parse(parser, false);
        }
    }
}
