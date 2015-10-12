using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.CLR
{
    public class CLRSignatureParser : IDisposable
    {
        public enum Token : byte
        {
            HASTHIS = 0x20,
            EXPLICITTHIS = 0x40,
            DEFAULT = 0x0,
            VARARG = 0x5,
            C = 0x1,
            STDCALL = 0x2,
            THISCALL = 0x3,
            FASTCALL = 0x4,
            FIELD = 0x6,
            PROPERTY = 0x08,
            PROPERTY_HASTHIS = 0x28,
            LOCAL_SIG = 0x7,
            SENTINEL = 0x41,
            PINNED = 0x45,
            GENERIC = 0x10,
            GENERICINST = 0x0a,
            BYREF = 0x10,
            TYPEDBYREF = 0x16,

            InvalidToken = 0,
        }

        private MemoryStream m_memoryStream;
        private CLRMetaDataTables m_tables;
        private BinaryReader m_reader;

        public CLRSignatureParser(ArraySegment<byte> seg, CLRMetaDataTables tables)
        {
            m_memoryStream = new MemoryStream(seg.Array, seg.Offset, seg.Count, false);
            m_tables = tables;
            m_reader = new BinaryReader(m_memoryStream);
        }

        public Token NextToken()
        {
            return (Token)NextByte();
        }

        public byte NextByte()
        {
            if (m_memoryStream.Position == m_memoryStream.Length)
                throw new IndexOutOfRangeException();
            byte result = m_reader.ReadByte();
            m_memoryStream.Seek(-1, SeekOrigin.Current);
            return result;
        }

        public void ConsumeByte()
        {
            if (m_memoryStream.Position == m_memoryStream.Length)
                throw new IndexOutOfRangeException();
            m_reader.ReadByte();
        }
        
        public void ConsumeToken()
        {
            ConsumeByte();
        }

        public int ReadCompressedInt()
        {
            int bits;
            uint b0 = NextByte();
            if ((b0 & 0x80) == 0)
                bits = 7;
            else if ((b0 & 0xc0) == 0x80)
                bits = 14;
            else if ((b0 & 0xe0) == 0xc0)
                bits = 29;
            else
                throw new ParseFailedException("Invalid encoded int");

            uint baseValue = ReadCompressedUInt();
            uint unrotatedAndOffset = (baseValue >> 1) | (((baseValue & 1) ^ 1) << (bits - 1));
            int s = (int)unrotatedAndOffset;
            s -= 1 << (bits - 1);
            return s;
        }

        public uint ReadCompressedUInt()
        {
            uint b0 = NextByte();
            ConsumeByte();

            if ((b0 & 0x80) == 0)
                return b0;
            if ((b0 & 0xc0) == 0x80)
            {
                byte b1 = NextByte();
                ConsumeByte();
                b0 &= 0x3f;
                return ((b0 << 8) | b1);
            }
            if ((b0 & 0xe0) == 0xc0)
            {
                b0 &= 0x1f;
                uint b1 = NextByte();
                ConsumeByte();
                uint b2 = NextByte();
                ConsumeByte();
                uint b3 = NextByte();
                ConsumeByte();
                return ((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
            }
            throw new ParseFailedException("Invalid encoded uint");
        }

        public bool TryPeekToken(out Token token)
        {
            if (m_memoryStream.Position == m_memoryStream.Length)
            {
                token = Token.InvalidToken;
                return false;
            }
            token = NextToken();
            return true;
        }

        // II.23.2.8
        public CLRTableRow ReadTypeDefOrRefOrSpecEncoded()
        {
            uint baseValue = ReadCompressedUInt();

            uint tableIndex = (baseValue & 0x3);
            uint rowIndex = (baseValue >> 2);

            CLRMetaDataTables.TableIndex realTableIndex;
            if (tableIndex == 0)
                realTableIndex = CLRMetaDataTables.TableIndex.TypeDef;
            else if (tableIndex == 1)
                realTableIndex = CLRMetaDataTables.TableIndex.TypeRef;
            else if (tableIndex == 2)
                realTableIndex = CLRMetaDataTables.TableIndex.TypeSpec;
            else
                throw new ParseFailedException("Unexpected table index");
            if (rowIndex == 0)
                throw new ParseFailedException("Null row in signature");
            return m_tables.GetTable((int)realTableIndex).GetRow(rowIndex - 1);
        }

        public void ReadBytes(byte[] bytes, uint count)
        {
            m_reader.Read(bytes, 0, (int)count);
        }

        public byte ReadU8()
        {
            return m_reader.ReadByte();
        }

        public sbyte ReadS8()
        {
            return m_reader.ReadSByte();
        }

        public ushort ReadU16()
        {
            return m_reader.ReadUInt16();
        }

        public short ReadS16()
        {
            return m_reader.ReadInt16();
        }

        public uint ReadU32()
        {
            return m_reader.ReadUInt32();
        }

        public int ReadS32()
        {
            return m_reader.ReadInt32();
        }

        public ulong ReadU64()
        {
            return m_reader.ReadUInt64();
        }

        public long ReadS64()
        {
            return m_reader.ReadInt64();
        }

        public float ReadF32()
        {
            return m_reader.ReadSingle();
        }

        public double ReadF64()
        {
            return m_reader.ReadDouble();
        }

        public void Dispose()
        {
            if (m_memoryStream != null)
                m_memoryStream.Dispose();
            if (m_reader != null)
                ((IDisposable)m_reader).Dispose();
        }
    }
}
