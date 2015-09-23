using System;

namespace AssemblyImporter.CLR
{
    // II.23.2.10
    public class CLRSigParamType : CLRSigParamOrRetType
    {
        public CLRSigParamType(CLRSignatureParser parser, bool allowSentinel)
            : base(parser, allowSentinel)
        {
        }

        protected override bool AllowVoid
        {
            get { return false; }
        }
    }
}
