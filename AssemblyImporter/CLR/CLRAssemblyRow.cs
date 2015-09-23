using System;

namespace AssemblyImporter.CLR
{
    // II.22.2
    public class CLRAssemblyRow : CLRTableRow
    {
        public enum AssemblyHashAlgorithm
        {
            None = 0x0,
            ReservedMD5 = 0x8003,
            SHA1 = 0x8004,
        }

        public AssemblyHashAlgorithm HashAlgId { get; private set; }
        public ushort MajorVersion { get; private set; }
        public ushort MinorVersion { get; private set; }
        public ushort BuildNumber { get; private set; }
        public ushort RevisionNumber { get; private set; }
        public ArraySegment<byte> PublicKey { get; private set; }
        public string Name { get; private set; }
        public string Culture { get; private set; }

        public bool HasPublicKey { get; private set; }
        public bool Retargetable { get; private set; }
        public bool DisableJITcompileOptimizer { get; private set; }
        public bool EnableJITcompileTracking { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            HashAlgId = (AssemblyHashAlgorithm)parser.ReadU32();
            if (HashAlgId != AssemblyHashAlgorithm.None && HashAlgId != AssemblyHashAlgorithm.ReservedMD5 && HashAlgId != AssemblyHashAlgorithm.SHA1)
                throw new ParseFailedException("Unknown hash algorith");
            MajorVersion = parser.ReadU16();
            MinorVersion = parser.ReadU16();
            BuildNumber = parser.ReadU16();
            RevisionNumber = parser.ReadU16();

            uint flags = parser.ReadU32();

            PublicKey = parser.ReadBlob();
            Name = parser.ReadString();
            Culture = parser.ReadString();

            HasPublicKey = ((flags & 0x1) != 0);
            Retargetable = ((flags & 0x100) != 0);
            DisableJITcompileOptimizer = ((flags & 0x4000) != 0);
            EnableJITcompileTracking = ((flags & 0x8000) != 0);
        }

        public override void Validate()
        {
            if (RowNumber > 0)
                throw new ParseFailedException("Multiple assemblies");
            // HashAlgId restricted
            // Flags restricted
            // Name not null or empty
            // Culture can be null, must be valid CIL culture
            // Name contains none of :/\.
            // Probably alpha-numeric with limited extensions
        }
    }
}
