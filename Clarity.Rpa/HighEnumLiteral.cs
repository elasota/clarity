using System.IO;
using System;

namespace Clarity.Rpa
{
    public class HighEnumLiteral
    {
        private long m_value;
        private string m_name;

        public string Name { get { return m_name; } }
        public long Value { get { return m_value; } }

        public HighEnumLiteral(string name, long value)
        {
            m_name = name;
            m_value = value;
        }

        public static HighEnumLiteral Read(CatalogReader catalog, BinaryReader reader, uint literalSize, bool isSigned)
        {
            string name = catalog.GetString(reader.ReadUInt32());

            long value;
            switch (literalSize)
            {
                case 8:
                    value = reader.ReadInt64();
                    break;
                case 4:
                    if (isSigned)
                        value = reader.ReadInt32();
                    else
                        value = reader.ReadUInt32();
                    break;
                case 2:
                    if (isSigned)
                        value = reader.ReadInt16();
                    else
                        value = reader.ReadUInt16();
                    break;
                case 1:
                    if (isSigned)
                        value = reader.ReadSByte();
                    else
                        value = reader.ReadByte();
                    break;
                default:
                    throw new ArgumentException();
            }

            return new HighEnumLiteral(name, value);
        }
    }
}
