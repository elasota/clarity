using System;

namespace AssemblyImporter.CLR
{
    // II.23.2.1
    // II.23.2.2
    // II.23.2.3
    public class CLRSigMethodDefOrRefSig
    {
        public enum CallingConventionType
        {
            Default,
            VarArg,
            Generic,
            C,
            StdCall,
            ThisCall,
            FastCall,
        }

        public enum Kind
        {
            Def,
            Ref,
            DefOrRef,
            StandAlone
        }

        public bool HasThis { get; private set; }
        public bool ExplicitThis { get; private set; }
        public uint NumGenericParameters { get; private set; }
        public CLRSigRetType RetType { get; private set; }
        public CLRSigParamType[] ParamTypes { get; private set; }
        public CallingConventionType CallingConvention { get; private set; }

        public CLRSigMethodDefOrRefSig(CLRSignatureParser parser, Kind allowedKind)
        {
            byte baseByte = parser.NextByte();
            parser.ConsumeByte();

            CLRSignatureParser.Token callingConvention = (CLRSignatureParser.Token)(baseByte & 0x0f);

            if ((baseByte & (byte)CLRSignatureParser.Token.HASTHIS) != 0)
            {
                HasThis = true;
                if ((baseByte & (byte)CLRSignatureParser.Token.EXPLICITTHIS) != 0)
                    ExplicitThis = true;
            }

            switch (callingConvention)
            {
                case CLRSignatureParser.Token.DEFAULT:
                    if (allowedKind != Kind.Def && allowedKind != Kind.DefOrRef && allowedKind != Kind.Ref)
                        throw new ParseFailedException("Invalid method signature");
                    allowedKind = Kind.Def;
                    CallingConvention = CallingConventionType.Default;
                    break;
                case CLRSignatureParser.Token.VARARG:
                    CallingConvention = CallingConventionType.VarArg;
                    break;
                case CLRSignatureParser.Token.GENERIC:
                    if (allowedKind != Kind.Def && allowedKind != Kind.DefOrRef && allowedKind != Kind.Ref)
                        throw new ParseFailedException("Invalid method signature");
                    allowedKind = Kind.Def;
                    NumGenericParameters = parser.ReadCompressedUInt();
                    break;
                case CLRSignatureParser.Token.C:
                    if (allowedKind != Kind.StandAlone)
                        throw new ParseFailedException("Invalid method signature");
                    CallingConvention = CallingConventionType.C;
                    break;
                case CLRSignatureParser.Token.STDCALL:
                    if (allowedKind != Kind.StandAlone)
                        throw new ParseFailedException("Invalid method signature");
                    CallingConvention = CallingConventionType.StdCall;
                    break;
                case CLRSignatureParser.Token.THISCALL:
                    if (allowedKind != Kind.StandAlone)
                        throw new ParseFailedException("Invalid method signature");
                    CallingConvention = CallingConventionType.ThisCall;
                    break;
                case CLRSignatureParser.Token.FASTCALL:
                    if (allowedKind != Kind.StandAlone)
                        throw new ParseFailedException("Invalid method signature");
                    CallingConvention = CallingConventionType.FastCall;
                    break;
                default:
                    throw new ParseFailedException("Unexpected signature token");
            }
            uint paramCount = parser.ReadCompressedUInt();

            // Return type
            RetType = new CLRSigRetType(parser);

            ParamTypes = new CLRSigParamType[paramCount];

            bool allowSentinel = (allowedKind == Kind.StandAlone) && (CallingConvention == CallingConventionType.C || CallingConvention == CallingConventionType.VarArg);

            // Parameter types
            for (uint i = 0; i < paramCount; i++)
                ParamTypes[i] = new CLRSigParamType(parser, allowSentinel);
        }
    }
}
