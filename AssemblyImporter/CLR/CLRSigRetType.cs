using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.2.11
    public class CLRSigRetType : CLRSigParamOrRetType
    {
        public CLRSigRetType(CLRSignatureParser parser)
            : base(parser, false)
        {
        }

        protected override bool AllowVoid
        {
            get { return true; }
        }
    }
}
