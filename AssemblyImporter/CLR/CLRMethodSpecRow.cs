using System;

namespace AssemblyImporter.CLR
{
    // II.22.29
    public class CLRMethodSpecRow : CLRTableRow
    {
        public CLRTableRow Method { get; private set; }
        public CLRSigMethodSpec Instantiation { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            Method = parser.ReadMethodDefOrRef();
            Instantiation = new CLRSigMethodSpec(new CLRSignatureParser(parser.ReadBlob(), parser.Tables));
        }

        public override void Validate()
        {
            // No duplicates by method + instantiation
        }
    }
}
