using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRMetaDataParser
    {
        private class MixedTagDecoder
        {
            private CLRMetaDataTables.TableIndex[] m_tableIndexes;
            private uint m_tagMask;
            private uint m_maxTag;
            private uint m_tagBits;
            private uint m_maxCompactRows;

            public MixedTagDecoder(CLRMetaDataTables.TableIndex[] tableIndexes)
            {
                m_maxTag = (uint)tableIndexes.Length - 1;
                m_tagMask = 0;
                m_tagBits = 0;
                while (m_tagMask < m_maxTag)
                {
                    m_tagBits++;
                    m_tagMask = (m_tagMask << 1) + 1;
                }
                m_tableIndexes = tableIndexes;
                m_maxCompactRows = ((uint)1 << (int)(16 - m_tagBits)) - 1;
            }

            public CLRTableRow Parse(CLRMetaDataParser parser, CLRMetaDataTables tables)
            {
                uint code = parser.ReadU16();

                uint tag = (code & m_tagMask);
                if (tag > m_maxTag)
                    throw new ParseFailedException("Invalid coded tag");
                ICLRTable table = tables.GetTable((int)m_tableIndexes[tag]);
                if (table.NumRows > m_maxCompactRows)
                {
                    uint highCode = parser.ReadU16();
                    code |= (highCode << 16);
                }
                uint row = code >> (int)m_tagBits;
                if (row == 0)
                    return null;
                return table.GetRow(row - 1);
            }
        }

        private static MixedTagDecoder s_TypeDefOrRefOrSpec = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                    CLRMetaDataTables.TableIndex.TypeDef,
                    CLRMetaDataTables.TableIndex.TypeRef,
                    CLRMetaDataTables.TableIndex.TypeSpec,
                }
            );

        private static MixedTagDecoder s_HasConstant = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                    CLRMetaDataTables.TableIndex.Field,
                    CLRMetaDataTables.TableIndex.Param,
                    CLRMetaDataTables.TableIndex.Property,
                }
            );

        private static MixedTagDecoder s_HasCustomAttribute = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] {
                    CLRMetaDataTables.TableIndex.MethodDef,
                    CLRMetaDataTables.TableIndex.Field,
                    CLRMetaDataTables.TableIndex.TypeRef,
                    CLRMetaDataTables.TableIndex.TypeDef,
                    CLRMetaDataTables.TableIndex.Param,
                    CLRMetaDataTables.TableIndex.InterfaceImpl,
                    CLRMetaDataTables.TableIndex.MemberRef,
                    CLRMetaDataTables.TableIndex.Module,
                    CLRMetaDataTables.TableIndex.DeclSecurity,    // Listed as "Permission" in the spec
                    CLRMetaDataTables.TableIndex.Property,
                    CLRMetaDataTables.TableIndex.Event,
                    CLRMetaDataTables.TableIndex.StandAloneSig,
                    CLRMetaDataTables.TableIndex.ModuleRef,
                    CLRMetaDataTables.TableIndex.TypeSpec,
                    CLRMetaDataTables.TableIndex.Assembly,
                    CLRMetaDataTables.TableIndex.AssemblyRef,
                    CLRMetaDataTables.TableIndex.File,
                    CLRMetaDataTables.TableIndex.ExportedType,
                    CLRMetaDataTables.TableIndex.ManifestResource,
                    CLRMetaDataTables.TableIndex.GenericParam,
                    CLRMetaDataTables.TableIndex.GenericParamConstraint,
                    CLRMetaDataTables.TableIndex.MethodSpec,
                }
            );

        private static MixedTagDecoder s_HasFieldMarshall = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                    CLRMetaDataTables.TableIndex.Field,
                    CLRMetaDataTables.TableIndex.Param,
                }
            );

        private static MixedTagDecoder s_HasDeclSecurity = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.TypeDef,
                   CLRMetaDataTables.TableIndex.MethodDef,
                   CLRMetaDataTables.TableIndex.Assembly,
                }
            );

        private static MixedTagDecoder s_MemberRefParent = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.TypeDef,
                   CLRMetaDataTables.TableIndex.TypeRef,
                   CLRMetaDataTables.TableIndex.ModuleRef,
                   CLRMetaDataTables.TableIndex.MethodDef,
                   CLRMetaDataTables.TableIndex.TypeSpec,
                }
            );

        private static MixedTagDecoder s_HasSemantics = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.Event,
                   CLRMetaDataTables.TableIndex.Property,
                }
            );

        private static MixedTagDecoder s_MethodDefOrRef = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.MethodDef,
                   CLRMetaDataTables.TableIndex.MemberRef,
                }
            );

        private static MixedTagDecoder s_MemberForwarded = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.Field,
                   CLRMetaDataTables.TableIndex.MethodDef,
                }
            );

        private static MixedTagDecoder s_Implementation = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.File,
                   CLRMetaDataTables.TableIndex.AssemblyRef,
                   CLRMetaDataTables.TableIndex.ExportedType,
                }
            );

        private static MixedTagDecoder s_CustomAttributeType = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.Invalid,
                   CLRMetaDataTables.TableIndex.Invalid,
                   CLRMetaDataTables.TableIndex.MethodDef,
                   CLRMetaDataTables.TableIndex.MemberRef,
                   CLRMetaDataTables.TableIndex.Invalid,
                }
            );

        private static MixedTagDecoder s_ResolutionScope = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.Module,
                   CLRMetaDataTables.TableIndex.ModuleRef,
                   CLRMetaDataTables.TableIndex.AssemblyRef,
                   CLRMetaDataTables.TableIndex.TypeRef,
                }
            );

        private static MixedTagDecoder s_TypeOrMethodDef = new MixedTagDecoder(
            new CLRMetaDataTables.TableIndex[] { 
                   CLRMetaDataTables.TableIndex.TypeDef,
                   CLRMetaDataTables.TableIndex.MethodDef,
                }
            );

        private StreamParser m_parser;
        private CLRMetaStreamBinaryData m_binData;
        private CLRMetaDataTables m_tables;
        private bool m_largeStrings;
        private bool m_largeGuids;
        private bool m_largeBlobs;

        public CLRMetaDataParser(StreamParser parser, CLRMetaStreamBinaryData binData, CLRMetaDataTables tables, bool largeStrings, bool largeGuids, bool largeBlobs)
        {
            m_parser = parser;
            m_binData = binData;
            m_tables = tables;
            m_largeStrings = largeStrings;
            m_largeGuids = largeGuids;
            m_largeBlobs = largeBlobs;
        }

        public CLRMetaDataTables Tables { get { return m_tables; } }
        public CLRMetaStreamBinaryData BinData { get { return m_binData; } }

        public CLRMetaDataParser CreateInternalStreamParser(StreamParser secondaryStream)
        {
            return new CLRMetaDataParser(secondaryStream, m_binData, m_tables, m_largeStrings, m_largeGuids, m_largeBlobs);
        }

        public byte ReadU8()
        {
            return m_parser.ReadU8();
        }

        public ushort ReadU16()
        {
            return m_parser.ReadU16();
        }

        public uint ReadU32()
        {
            return m_parser.ReadU32();
        }

        public ulong ReadU64()
        {
            return m_parser.ReadU64();
        }

        public sbyte ReadS8()
        {
            return m_parser.ReadS8();
        }

        public short ReadS16()
        {
            return m_parser.ReadS16();
        }

        public int ReadS32()
        {
            return m_parser.ReadS32();
        }

        public long ReadS64()
        {
            return m_parser.ReadS64();
        }

        public byte[] ReadBytes(uint size)
        {
            byte[] bytes = new byte[size];
            m_parser.Read(bytes, 0, size);
            return bytes;
        }

        public string ReadString()
        {
            long offset;
            if (m_largeStrings)
                offset = m_parser.ReadU32();
            else
                offset = m_parser.ReadU16();

            List<byte> bytes = new List<byte>();
            byte[] strBlob = m_binData.StringData;
            while (strBlob[offset] != 0)
            {
                bytes.Add(strBlob[offset]);
                offset++;
            }
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }

        public CLRGuid ReadGuid()
        {
            long offset;
            if (m_largeGuids)
                offset = m_parser.ReadU32();
            else
                offset = m_parser.ReadU16();

            if (offset == 0)
                return null;

            offset--;

            byte[] guidBlob = m_binData.GuidData;
            CLRGuid guid = new CLRGuid();
            for (int i = 0; i < 8; i++)
                guid.low = guid.low | ((ulong)guidBlob[offset + i] << (i * 8));
            for (int i = 0; i < 8; i++)
                guid.high = guid.high | ((ulong)guidBlob[offset + 8 + i] << (i * 8));
            return guid;
        }

        public ArraySegment<byte> ReadBlob()
        {
            long offset;
            if (m_largeBlobs)
                offset = m_parser.ReadU32();
            else
                offset = m_parser.ReadU16();
            return ReadBlobOrUS(m_binData.BlobData, offset);
        }

        public ArraySegment<byte> ReadUS()
        {
            ushort offset = m_parser.ReadU16();
            return ReadBlobOrUS(m_binData.BlobData, offset);
        }

        public static ArraySegment<byte> ReadBlobOrUS(byte[] blobStream, long offset)
        {
            byte firstByte = blobStream[offset];
            uint blobSize;
            if ((firstByte & 0x80) == 0)
            {
                blobSize = (uint)(firstByte & 0x7f);
                offset++;
            }
            else if ((firstByte & 0xc0) == 0x80)
            {
                blobSize = ((uint)(firstByte & 0x3f) << 8) | (uint)blobStream[offset + 1];
                offset += 2;
            }
            else if ((firstByte & 0xe0) == 0xc0)
            {
                blobSize = ((uint)(firstByte & 0x3f) << 24) | ((uint)blobStream[offset + 1] << 16) | ((uint)blobStream[offset + 2] << 8) | (uint)blobStream[offset + 3];
                offset += 4;
            }
            else
                throw new ParseFailedException("Invalid blob");

            return new ArraySegment<byte>(blobStream, (int)offset, (int)blobSize);
        }

        public CLRTableRow ReadTypeDefOrRefOrSpec()
        {
            return s_TypeDefOrRefOrSpec.Parse(this, m_tables);
        }

        public CLRTableRow ReadHasConstant()
        {
            return s_HasConstant.Parse(this, m_tables);
        }

        public CLRTableRow ReadHasCustomAttribute()
        {
            return s_HasCustomAttribute.Parse(this, m_tables);
        }

        public CLRTableRow ReadHasFieldMarshall()
        {
            return s_HasFieldMarshall.Parse(this, m_tables);
        }

        public CLRTableRow ReadHasDeclSecurity()
        {
            return s_HasDeclSecurity.Parse(this, m_tables);
        }

        public CLRTableRow ReadMemberRefParent()
        {
            return s_MemberRefParent.Parse(this, m_tables);
        }

        public CLRTableRow ReadHasSemantics()
        {
            return s_HasSemantics.Parse(this, m_tables);
        }

        public CLRTableRow ReadMethodDefOrRef()
        {
            return s_MethodDefOrRef.Parse(this, m_tables);
        }

        public CLRTableRow ReadMemberForwarded()
        {
            return s_MemberForwarded.Parse(this, m_tables);
        }

        public CLRTableRow ReadImplementation()
        {
            return s_Implementation.Parse(this, m_tables);
        }

        public CLRTableRow ReadCustomAttributeType()
        {
            return s_CustomAttributeType.Parse(this, m_tables);
        }

        public CLRTableRow ReadResolutionScope()
        {
            return s_ResolutionScope.Parse(this, m_tables);
        }

        public CLRTableRow ReadTypeOrMethodDef()
        {
            return s_TypeOrMethodDef.Parse(this, m_tables);
        }

        public uint ReadTableRawRow(CLRMetaDataTables.TableIndex tableIndex)
        {
            ICLRTable table = m_tables.GetTable((int)tableIndex);
            if (table.NumRows >= 65536)
                return m_parser.ReadU32();
            else
                return m_parser.ReadU16();
        }

        public CLRTableRow ReadTable(CLRMetaDataTables.TableIndex tableIndex)
        {
            return GetTableRawRow(tableIndex, ReadTableRawRow(tableIndex));
        }

        public CLRTableRow ReadFatToken()
        {
            uint fatToken = m_parser.ReadU32();
            uint tableRow = fatToken & 0x00ffffff;
            if (tableRow == 0)
                return null;
            uint tableIndex = (fatToken >> 24);
            return m_tables.GetTable((int)tableIndex).GetRow(tableRow - 1);
        }

        public void Align(uint alignment)
        {
            uint misalignment = (uint)m_parser.Position % alignment;
            if (misalignment != 0)
                m_parser.Skip(alignment - misalignment);
        }

        public uint GetTableNumRows(CLRMetaDataTables.TableIndex tableIndex)
        {
            return m_tables.GetTable((int)tableIndex).NumRows;
        }

        public CLRTableRow GetTableRow(CLRMetaDataTables.TableIndex tableIndex, uint row)
        {
            return m_tables.GetTable((int)tableIndex).GetRow(row);
        }

        public CLRTableRow GetTableRawRow(CLRMetaDataTables.TableIndex tableIndex, uint row)
        {
            if (row == 0)
                return null;
            return m_tables.GetTable((int)tableIndex).GetRow(row - 1);
        }
    }
}
