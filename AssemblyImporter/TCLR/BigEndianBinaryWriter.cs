using System;
using System.IO;
using System.Text;

namespace AssemblyImporter.TCLR
{
    public class BigEndianBinaryWriter : BinaryWriter
    {
        private byte[] m_scratchBuffer;
        private BinaryWriter m_binWriter;
        private MemoryStream m_swapMemStream;

        private void Init()
        {
            m_scratchBuffer = new byte[16];
            m_swapMemStream = new MemoryStream(m_scratchBuffer, true);
            m_binWriter = new BinaryWriter(m_swapMemStream);
        }

        private void WriteSwapped(int nBytes)
        {
            int halfPoint = nBytes / 2;
            for (int i=0;i<nBytes;i++)
            {
                int swapPos = nBytes - 1 - i;
                byte temp = m_scratchBuffer[i];
                m_scratchBuffer[i] = m_scratchBuffer[swapPos];
                m_scratchBuffer[swapPos] = temp;
            }
            Write(m_scratchBuffer, 0, nBytes);
        }

        public BigEndianBinaryWriter(Stream output)
            : base(output)
        {
        }

        public BigEndianBinaryWriter(Stream output, Encoding encoding)
            : base(output, encoding)
        {
        }

        public BigEndianBinaryWriter(Stream output, Encoding encoding, bool leaveOpen)
            : base(output, encoding, leaveOpen)
        {
        }

        protected override void Dispose(bool b)
        {
            base.Dispose(b);
            if (m_binWriter != null)
                m_binWriter.Dispose();
            if (m_swapMemStream != null)
                m_swapMemStream.Dispose();
        }

        public override void Write(char ch)
        {
            Write((short)ch);
        }

        public override void Write(char[] chars)
        {
            foreach (char ch in chars)
                Write(ch);
        }


        public override void Write(decimal value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(16);
        }

        public override void Write(double value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(8);
        }

        public override void Write(float value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(4);
        }

        public override void Write(int value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(4);
        }

        public override void Write(long value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(8);
        }

        public override void Write(short value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(2);
        }

        public override void Write(uint value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(4);
        }

        public override void Write(ulong value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(8);
        }

        public override void Write(ushort value)
        {
            m_swapMemStream.Seek(0, SeekOrigin.Begin);
            m_binWriter.Write(value);
            WriteSwapped(2);
        }

        public override void Write(char[] chars, int index, int count)
        {
            for (int i = 0; i < count; i++)
                Write(chars[i + index]);
        }
    }
}
