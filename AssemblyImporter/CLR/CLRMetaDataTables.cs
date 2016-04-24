using System;

namespace AssemblyImporter.CLR
{
    public class CLRMetaDataTables
    {
        public enum TableIndex
        {
            Module = 0x00,
            TypeRef = 0x01,
            TypeDef = 0x02,
            Field = 0x04,
            MethodDef = 0x06,
            Param = 0x08,
            InterfaceImpl = 0x09,
            MemberRef = 0x0a,
            Constant = 0x0b,
            CustomAttribute = 0x0c,
            FieldMarshal = 0x0d,
            DeclSecurity = 0x0e,
            ClassLayout = 0x0f,
            FieldLayout = 0x10,
            StandAloneSig = 0x11,
            EventMap = 0x12,
            Event = 0x13,
            PropertyMap = 0x15,
            Property = 0x17,
            MethodSemantics = 0x18,
            MethodImpl = 0x19,
            ModuleRef = 0x1a,
            TypeSpec = 0x1b,
            ImplMap = 0x1c,
            FieldRVA = 0x1d,
            Assembly = 0x20,
            AssemblyProcessor = 0x21,
            AssemblyOS = 0x22,
            AssemblyRef = 0x23,
            AssemblyRefProcessor = 0x24,
            AssemblyRefOS = 0x25,
            File = 0x26,
            ExportedType = 0x27,
            ManifestResource = 0x28,
            NestedClass = 0x29,
            GenericParam = 0x2a,
            GenericParamConstraint = 0x2c,
            MethodSpec = 0x2b,

            Invalid = 0x800,
        }

        public bool StringOffsets32Bit { get; private set; }
        public bool GuidOffsets32Bit { get; private set; }
        public bool BlobOffsets32Bit { get; private set; }
        public CLRMetaDataParser MetaDataParser { get; private set; }
        public CLRMetaData MetaData { get; private set; }

        private ICLRTable[] m_clrTables;
        private uint[] m_rowCounts;

        public CLRMetaDataTables(StreamParser parser, CLRMetaData metaData, CLRMetaStreamBinaryData binData)
        {
            MetaData = metaData;

            parser.Skip(4); // Reserved
            byte majorVersion = parser.ReadU8();
            byte minorVersion = parser.ReadU8();

            byte heapSizes = parser.ReadU8();
            parser.Skip(1); // Reserved
            ulong validMask = parser.ReadU64();
            ulong sortedMask = parser.ReadU64();

            m_rowCounts = new uint[64];
            for (int i = 0; i < 64; i++)
            {
                if ((validMask & ((ulong)1 << i)) != 0)
                    m_rowCounts[i] = parser.ReadU32();
            }

            StringOffsets32Bit = ((heapSizes & 1) != 0);
            GuidOffsets32Bit = ((heapSizes & 2) != 0);
            BlobOffsets32Bit = ((heapSizes & 4) != 0);

            if (majorVersion != 2 || minorVersion != 0)
                throw new ParseFailedException("Unknown metadata table version");

            m_clrTables = new ICLRTable[64];
            AddTable(0x00, new CLRTable<CLRModuleRow>());
            AddTable(0x01, new CLRTable<CLRTypeRefRow>());
            AddTable(0x02, new CLRTable<CLRTypeDefRow>());
            AddTable(0x04, new CLRTable<CLRFieldRow>());
            AddTable(0x06, new CLRTable<CLRMethodDefRow>());
            AddTable(0x08, new CLRTable<CLRParamRow>());
            AddTable(0x09, new CLRTable<CLRInterfaceImplRow>());
            AddTable(0x0a, new CLRTable<CLRMemberRefRow>());
            AddTable(0x0b, new CLRTable<CLRConstantRow>());
            AddTable(0x0c, new CLRTable<CLRCustomAttributeRow>());
            AddTable(0x0d, new CLRTable<CLRFieldMarshalRow>());
            AddTable(0x0e, new CLRTable<CLRDeclSecurityRow>());
            AddTable(0x0f, new CLRTable<CLRClassLayoutRow>());
            AddTable(0x10, new CLRTable<CLRFieldLayoutRow>());
            AddTable(0x11, new CLRTable<CLRStandAloneSigRow>());
            AddTable(0x12, new CLRTable<CLREventMapRow>());
            AddTable(0x14, new CLRTable<CLREventRow>());
            AddTable(0x15, new CLRTable<CLRPropertyMapRow>());
            AddTable(0x17, new CLRTable<CLRPropertyRow>());
            AddTable(0x18, new CLRTable<CLRMethodSemanticsRow>());
            AddTable(0x19, new CLRTable<CLRMethodImplRow>());
            AddTable(0x1a, new CLRTable<CLRModuleRefRow>());
            AddTable(0x1b, new CLRTable<CLRTypeSpecRow>());
            AddTable(0x1c, new CLRTable<CLRImplMapRow>());
            AddTable(0x1d, new CLRTable<CLRFieldRVARow>());
            AddTable(0x20, new CLRTable<CLRAssemblyRow>());
            AddTable(0x21, new CLRTable<CLRAssemblyProcessorRow>());
            AddTable(0x22, new CLRTable<CLRAssemblyOSRow>());
            AddTable(0x23, new CLRTable<CLRAssemblyRefRow>());
            AddTable(0x24, new CLRTable<CLRAssemblyRefProcessorRow>());
            AddTable(0x25, new CLRTable<CLRAssemblyRefOSRow>());
            AddTable(0x26, new CLRTable<CLRFileRow>());
            AddTable(0x27, new CLRTable<CLRExportedTypeRow>());
            AddTable(0x28, new CLRTable<CLRManifestResourceRow>());
            AddTable(0x29, new CLRTable<CLRNestedClassRow>());
            AddTable(0x2a, new CLRTable<CLRGenericParamRow>());
            AddTable(0x2c, new CLRTable<CLRGenericParamConstraintRow>());
            AddTable(0x2b, new CLRTable<CLRMethodSpecRow>());

            MetaDataParser = new CLRMetaDataParser(parser, binData, this, StringOffsets32Bit, GuidOffsets32Bit, BlobOffsets32Bit);

            for (int i = 0; i < 64; i++)
            {
                if ((validMask & (ulong)1 << i) != 0)
                {
                    if (m_clrTables[i] == null)
                        throw new ParseFailedException("Unknown table type");
                    m_clrTables[i].Parse(MetaDataParser);
                }
            }
        }

        public ICLRTable GetTable(int tableNum)
        {
            return m_clrTables[tableNum];
        }

        public ICLRTable GetTable(CLRMetaDataTables.TableIndex tableIndex)
        {
            return GetTable((int)tableIndex);
        }

        private void AddTable(int tableId, ICLRTable clrTable)
        {
            if (m_clrTables[tableId] != null)
                throw new InvalidOperationException("Internal error");
            m_clrTables[tableId] = clrTable;
            clrTable.Init((uint)tableId, m_rowCounts[tableId], MetaData);
        }
    }
}
