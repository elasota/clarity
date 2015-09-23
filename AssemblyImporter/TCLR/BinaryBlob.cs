using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.TCLR
{
    public sealed class BinaryBlob : IEquatable<BinaryBlob>
    {
        private byte[] m_bytes;
        private int m_hash;

        public byte[] Bytes { get { return m_bytes; } }

        public BinaryBlob(byte[] bytes)
        {
            if (bytes.LongLength > 0x7ffffff)
                throw new OverflowException();
            m_hash = (int)CRC32.Hash(bytes);
            m_bytes = bytes;
        }

        public bool Equals(BinaryBlob other)
        {
            if (other.m_hash != m_hash)
                return false;
            if (other.m_bytes.LongLength != m_bytes.LongLength)
                return false;
            int len = m_bytes.Length;
            for (long i = 0; i < len; i++)
                if (m_bytes[i] != other.m_bytes[i])
                    return false;
            return true;
        }

        public override int GetHashCode()
        {
            return m_hash;
        }

        public override bool Equals(object other)
        {
            return other != null && other.GetType() == typeof(BinaryBlob) && Equals((BinaryBlob)other);
        }
    }
}
