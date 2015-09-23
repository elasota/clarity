using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.2.6
    public class CLRSigLocalVar
    {
        public enum LocalVarKind
        {
            Default,
            TypedByRef,
            ByRef,
        }

        public CLRSigCustomMod[] CustomMods { get; private set; }
        public CLRSigConstraint[] Constraints { get; private set; }
        public LocalVarKind VarKind { get; private set; }
        public CLRSigType Type { get; private set; }

        public CLRSigLocalVar(CLRSignatureParser parser)
        {
            if (parser.NextToken() == CLRSignatureParser.Token.TYPEDBYREF)
            {
                VarKind = LocalVarKind.TypedByRef;
                parser.ConsumeToken();
            }
            else
            {
                CLRSigCustomMod[] customMods;
                CLRSigConstraint[] constraints;
                CLRSigType.ReadCustomModsAndConstraints(parser, out customMods, out constraints);

                CustomMods = customMods;
                Constraints = constraints;

                if (parser.NextToken() == CLRSignatureParser.Token.BYREF)
                {
                    VarKind = LocalVarKind.ByRef;
                    parser.ConsumeToken();
                }
                else
                    VarKind = LocalVarKind.Default;

                Type = CLRSigType.Parse(parser, false);
            }
        }
    }
}
