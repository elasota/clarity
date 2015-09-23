using System;

namespace AssemblyImporter.CLR
{
    // II.22.39
    public class CLRTypeSpecRow : CLRTableRow, ICLRResolvable
    {
        public CLRSigTypeSpec Signature { get; private set; }

        public CLRTypeSpec Resolution { get; private set; }

        public bool IsResolved { get { return Resolution != null; } }

        public override void Parse(CLRMetaDataParser parser)
        {
            Signature = new CLRSigTypeSpec(new CLRSignatureParser(parser.ReadBlob(), parser.Tables));
        }

        public override void Validate()
        {
            // Signature is valid Type specification
            // No duplicates by Signature
        }

        public void Resolve(CLRAssemblyCollection assemblies)
        {
            Resolution = assemblies.InternTypeSpec(Signature);
        }
    }
}
