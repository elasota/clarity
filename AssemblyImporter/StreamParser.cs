using System;
using System.Collections.Generic;

namespace AssemblyImporter
{
    public class StreamParser
    {
        private System.IO.Stream m_stream;
        private byte[] m_scratch;
        private bool m_isBigEndian;

        private ulong ParseBase(int nBytes)
        {
            long offset = m_stream.Position;
            m_stream.Read(m_scratch, 0, nBytes);
            ulong v = 0;
            if (m_isBigEndian)
                for (int i=0;i<nBytes;i++)
                    v = (v << 8) | m_scratch[i];
            else
                for (int i = 0; i < nBytes; i++)
                    v = (v << 8) | m_scratch[nBytes - 1 - i];
            return v;
        }

        public StreamParser(System.IO.Stream stream, bool isBigEndian)
        {
            m_stream = stream;
            m_scratch = new byte[8];
            m_isBigEndian = isBigEndian;
        }

        public byte ReadU8()
        {
            m_stream.Read(m_scratch, 0, 1);
            return m_scratch[0];
        }

        public ushort ReadU16()
        {
            return (ushort)ParseBase(2);
        }

        public uint ReadU32()
        {
            return (uint)ParseBase(4);
        }

        public ulong ReadU64()
        {
            return ParseBase(8);
        }

        public sbyte ReadS8()
        {
            m_stream.Read(m_scratch, 0, 1);
            return (sbyte)m_scratch[0];
        }

        public short ReadS16()
        {
            return (short)ParseBase(2);
        }

        public int ReadS32()
        {
            return (int)ParseBase(4);
        }

        public long ReadS64()
        {
            return (long)ParseBase(8);
        }

        public void Skip(long offset)
        {
            m_stream.Seek(offset, System.IO.SeekOrigin.Current);
        }

        public void Seek(long offset)
        {
            m_stream.Seek(offset, System.IO.SeekOrigin.Begin);
        }

        public string ReadVarAsciiString(int maxLength)
        {
            byte[] bytes = new byte[4];
            List<byte> truncated = new List<byte>();

            int lengthRemaining = maxLength;
            while (lengthRemaining > 0)
            {
                lengthRemaining--;
                m_stream.Read(bytes, 0, 4);

                foreach (byte b in bytes)
                {
                    if (b == 0)
                    {
                        lengthRemaining = 0;
                        break;
                    }
                    else
                        truncated.Add(b);
                }
            }
            bytes = truncated.ToArray();
            truncated = null;

            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        public string ReadUTF8String(int nBytes)
        {
            byte[] bytes = new byte[nBytes];
            m_stream.Read(bytes, 0, nBytes);

            List<byte> truncated = new List<byte>();
            foreach (byte b in bytes)
            {
                if (b == 0)
                    break;
                else
                    truncated.Add(b);
            }
            bytes = truncated.ToArray();
            truncated = null;

            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public void Read(byte[] bytes, uint offset, uint size)
        {
            while (size > 0x7ffffff)
            {
                m_stream.Read(bytes, 0, 0x7ffffff);
                offset += 0x7fffffff;
                size -= 0x7fffffff;
            }
            m_stream.Read(bytes, (int)offset, (int)size);
        }

        public long Position
        {
            get
            {
                return m_stream.Position;
            }
        }
    }
}
