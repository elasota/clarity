using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public class DisassemblyWriter
    {
        private StreamWriter m_writer;
        private int m_indentLevel = 0;
        private bool m_haveWrittenIndent = false;
        private bool m_isMuted = false;

        private HashSet<char> m_allowedTokenChars = new HashSet<char>();
        private HashSet<char> m_allowedQuotedChars = new HashSet<char>();
        private Dictionary<object, IDisassemblyWritable> m_methodHandleReverseLookup;

        public DisassemblyWriter(StreamWriter streamWriter, Dictionary<object, IDisassemblyWritable> methodHandleReverseLookup)
        {
            m_writer = streamWriter;

            foreach (char c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_`0123456789")
                m_allowedTokenChars.Add(c);
            foreach (char c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !#$%&\'()*+,-./:;<=>?@[]^_`{|}~")
                m_allowedQuotedChars.Add(c);

            m_methodHandleReverseLookup = methodHandleReverseLookup;
        }

        public void PushIndent()
        {
            m_indentLevel++;
        }

        public void PopIndent()
        {
            m_indentLevel--;
        }

        private void StartIndented()
        {
            if (m_isMuted)
                return;

            if (!m_haveWrittenIndent)
            {
                int indentLevel = m_indentLevel;
                for (int i = 0; i < indentLevel; i++)
                    m_writer.Write("\t");
                m_haveWrittenIndent = true;
            }
        }

        public void Write(string str)
        {
            if (m_isMuted)
                return;

            StartIndented();
            m_writer.Write(str);
        }

        public void WriteLine(string str)
        {
            if (m_isMuted)
                return;

            Write(str);
            m_writer.WriteLine();
            m_haveWrittenIndent = false;
        }

        public void WriteToken(string str)
        {
            if (m_isMuted)
                return;

            bool allOK = true;
            foreach (char c in str)
            {
                if (!m_allowedTokenChars.Contains(c))
                {
                    allOK = false;
                    break;
                }
            }

            if (allOK)
                Write(str);
            else
                WriteQuoted(str);
        }

        internal void Unmute()
        {
            m_isMuted = false;
        }

        internal void Mute()
        {
            m_isMuted = true;
        }

        public void WriteMethodHandleKey(object methodHandle)
        {
            Write("method(");
            m_methodHandleReverseLookup[methodHandle].WriteDisassembly(this);
            Write(")");
        }

        public void WriteQuoted(string str)
        {
            if (m_isMuted)
                return;

            Write("\"");
            foreach (char c in str)
            {
                if (m_allowedQuotedChars.Contains(c))
                    m_writer.Write(c);
                else
                {
                    if (c == '\"')
                        m_writer.Write("\\\"");
                    else
                    {
                        m_writer.Write("\\");

                        int code = (int)c;
                        for (int h = 0; h < 4; h++)
                        {
                            int nibble = (h >> (12 - 4 * h)) & 0xf;
                            m_writer.Write(("0123456789abcdef")[nibble]);
                        }
                    }
                }

            }
            Write("\"");
        }
    }
}
