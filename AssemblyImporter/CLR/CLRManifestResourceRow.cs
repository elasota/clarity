using System;

namespace AssemblyImporter.CLR
{
    // II.22.24
    public class CLRManifestResourceRow : CLRTableRow
    {
        public enum ManifestResourceVisibility
        {
            Public = 0x1,
            Private = 0x2,
        }

        public uint Offset { get; private set; }
        public string Name { get; private set; }
        public CLRTableRow Implementation { get; private set; }
        public ManifestResourceVisibility Visibility { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            Offset = parser.ReadU32();
            uint flags = parser.ReadU32();
            uint visibility = (flags & 0x7);

            if (visibility < 0x1 || visibility > 0x2)
                throw new ParseFailedException("Unexpected visibility on manifest resource");
            Visibility = (ManifestResourceVisibility)visibility;

            Name = parser.ReadString();
            Implementation = parser.ReadImplementation();
        }

        public override void Validate()
        {
            // Offset is valid into target file, starting from CLI header
            // Flags restricted
            // Visibility restricted
            // Name is non-empty
            // Implementation may be null
            // If implementation is null, offset is valid
            // If implementation is not null, offset indexes a row in File or AssemblyRef
            // No duplicates by Name
            // If resources indexes into file, offset is 0
        }
    }
}
