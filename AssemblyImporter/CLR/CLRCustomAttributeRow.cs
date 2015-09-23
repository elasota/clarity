using System;

namespace AssemblyImporter.CLR
{
    // II.22.10
    public class CLRCustomAttributeRow : CLRTableRow
    {
        public CLRTableRow Parent { get; private set; }
        public CLRTableRow Constructor { get; private set; }
        public CLRSigCustomAttribute CustomAttribute { get; private set; }

        private ArraySegment<byte> m_rawValue;
        private CLRMetaDataTables m_tables;

        public override void Parse(CLRMetaDataParser parser)
        {
            Parent = parser.ReadHasCustomAttribute();
            Constructor = parser.ReadCustomAttributeType();
            m_rawValue = parser.ReadBlob();
            m_tables = parser.Tables;
        }

        public void Resolve()
        {
            CustomAttribute = new CLRSigCustomAttribute(new CLRSignatureParser(m_rawValue, m_tables), Constructor);
            m_rawValue = new ArraySegment<byte>();
            m_tables = null;
        }

        public override void Validate()
        {
            // Binary values are stored in little endian, except for PackedLen for UTF8 strings
            // Value may be null
            // Type references a constructor method
            // If Value is non-null:
            //     Prolog is 0x0001
            //     As many FixedArg as in the constructor
            //     NumNamed may be zero
            //     Exactly NumNamed occurrences of NamedArg
            //     Each NamedArg is accessible by the caller
            //     If NumNamed = 0, no further items
            // FixedArg:
            //     If item is not for a vector (single-dimension zero-bound lower), one Elem
            //     If for a vector, NumElem is 1 or more, followed by NumElem x Elem
            // Elem:
            //     If simple type or enum, Elem is value
            //     If string or Type, Elem is a SerString - PackedLen count + UTF8
            //     If boxed simple value, Elem is corresponding type, followed by value
            // NamedArg:
            //     Starts with FIELD or PROPERTY
            //     If parameter kind is boxed simple value type, field or property is BOOLEAN, CHAR, I1, U1, I2, U2, I4, U4, I8, U8, R4, R8, STRING, or 0x50 (for System.Type)
            //     Name of FIELD or PROPERTY is packed UTF8
            //     NamedArg is a FixedArg
        }
    }
}
