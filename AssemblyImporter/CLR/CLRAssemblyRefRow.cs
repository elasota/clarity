using System;

namespace AssemblyImporter.CLR
{
    // II.22.5
    // II.23.1.2
    public class CLRAssemblyRefRow : CLRTableRow, ICLRResolvable, ICLRHasCustomAttributes
    {
        public ushort MajorVersion { get; private set; }
        public ushort MinorVersion { get; private set; }
        public ushort BuildNumber { get; private set; }
        public ushort RevisionNumber { get; private set; }
        public string Name { get; private set; }
        public string Culture { get; private set; }
        public ArraySegment<byte> HashValue { get; private set; }
        public ArraySegment<byte> PublicKeyOrToken { get; private set; }

        public bool HasPublicKey { get; private set; }
        public bool Retargetable { get; private set; }
        public bool DisableJITcompileOptimizer { get; private set; }
        public bool EnableJITcompileTracking { get; private set; }

        public CLRAssembly Resolution { get; private set; }
        public bool IsResolved { get { return this.Resolution != null; } }

        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }

        public override void Parse(CLRMetaDataParser parser)
        {
            MajorVersion = parser.ReadU16();
            MinorVersion = parser.ReadU16();
            BuildNumber = parser.ReadU16();
            RevisionNumber = parser.ReadU16();
            uint flags = parser.ReadU32();
            PublicKeyOrToken = parser.ReadBlob();
            Name = parser.ReadString();
            Culture = parser.ReadString();
            HashValue = parser.ReadBlob();

            HasPublicKey = ((flags & 0x1) != 0);
            Retargetable = ((flags & 0x100) != 0);
            DisableJITcompileOptimizer = ((flags & 0x4000) != 0);
            EnableJITcompileTracking = ((flags & 0x8000) != 0);
        }

        public void Resolve(CLRAssemblyCollection assemblies)
        {
            foreach (CLRAssembly assm in assemblies)
            {
                ICLRTable table = assm.MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.Assembly);
                CLRAssemblyRow assmRow = (CLRAssemblyRow)table.GetRow(0);
                if (assmRow.Name == Name &&
                    assmRow.Culture == Culture &&
                    assmRow.MajorVersion == MajorVersion &&
                    assmRow.MinorVersion == MinorVersion &&
                    assmRow.BuildNumber == BuildNumber &&
                    assmRow.RevisionNumber == RevisionNumber)
                {
                    Resolution = assm;
                    break;
                }
            }
        }
    }
}
