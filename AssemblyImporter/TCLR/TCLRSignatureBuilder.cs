using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;

namespace AssemblyImporter.TCLR
{
    public class TCLRSignatureBuilder : IDisposable
    {
        private MemoryStream m_stream;
        private BinaryWriter m_writer;
        private TCLRAssemblyBuilder m_builder;

        public TCLRSignatureBuilder(TCLRAssemblyBuilder assemblyBuilder, bool bigEndian)
        {
            m_stream = new MemoryStream();
            m_builder = assemblyBuilder;
            if (bigEndian)
                m_writer = new BigEndianBinaryWriter(m_stream);
            else
                m_writer = new BinaryWriter(m_stream);
        }

        public byte[] Finish()
        {
            byte[] result = m_stream.ToArray();
            Dispose();
            return result;
        }

        public void Dispose()
        {
            if (m_stream != null)
                m_stream.Dispose();
            m_stream = null;
            if (m_writer != null)
                ((IDisposable)m_writer).Dispose();
            m_writer = null;
        }

        public void WriteDataType(TCLRDataType dataType)
        {
            m_writer.Write((byte)dataType);
        }

        public void WriteByte(byte b)
        {
            m_writer.Write(b);
        }

        private bool TypeDefIsValueType(CLRTypeDefRow tdef)
        {
            if (tdef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                return false;

            if (!(tdef.Extends is CLRTypeDefRow))
                return false;

            CLRTypeDefRow ex = (CLRTypeDefRow)tdef.Extends;
            if (ex == null)
                return false;   // System.Object

            if (ex.TypeNamespace == "System" && (ex.TypeName == "ValueType" || ex.TypeName == "Enum"))
                return true;

            return false;
        }
        
        private void WriteCompressedUInt(uint i)
        {
            if (i <= 0x7f)
                m_writer.Write((byte)i);
            else if (i <= 0x3fff)
            {
                m_writer.Write((byte)((i >> 8) | 0x80));
                m_writer.Write((byte)(i & 0xff));
            }
            else if (i <= 0x1fffffff)
            {
                m_writer.Write((byte)((i >> 24) | 0xc0));
                m_writer.Write((byte)((i >> 16) & 0xff));
                m_writer.Write((byte)((i >> 8) & 0xff));
                m_writer.Write((byte)(i & 0xff));
            }
            else
                throw new OverflowException("Oversized token");
        }

        private void WriteSimpleDataType(string typeNamespace, string typeName)
        {
            WriteDataType(TCLRDataType.DATATYPE_VALUETYPE);
            CLRTableRow typeRow = m_builder.FindType(typeNamespace, typeName);
            CLRSigTypeStructured structuredType = new CLRSigTypeStructured(CLRSigType.ElementType.VALUETYPE, typeRow);
        }

        public void WriteStructuredType(CLRSigTypeStructured structuredType, bool writePrefix)
        {
            CLRTableRow typeRow = structuredType.TypeDefOrRefOrSpec;
            string typeName, typeNamespace;
            bool isDataType;
            uint compressedTokenIndex;
            uint rowNumber;
            if (typeRow is CLRTypeDefRow)
            {
                CLRTypeDefRow tdef = (CLRTypeDefRow)typeRow;
                typeName = tdef.TypeName;
                typeNamespace = tdef.TypeNamespace;
                isDataType = TypeDefIsValueType(tdef);
                compressedTokenIndex = 0;
                rowNumber = m_builder.IndexTypeDef((CLRTypeDefRow)typeRow);
            }
            else if (typeRow is CLRTypeRefRow)
            {
                // CLARITYTODO
                compressedTokenIndex = 1;
                rowNumber = m_builder.IndexTypeRef((CLRTypeRefRow)typeRow);
                throw new NotImplementedException();
            }
            else if (typeRow is CLRTypeSpecRow)
            {
                // CLARITYTODO?
                compressedTokenIndex = 2;
                rowNumber = m_builder.IndexTypeSpec((CLRTypeSpecRow)typeRow);
                throw new NotSupportedException("Type specializations are not supported");
            }
            else
                throw new NotImplementedException();

            if (writePrefix && typeNamespace == "System" && typeName == "DateTime")
                WriteDataType(TCLRDataType.DATATYPE_DATETIME);
            else if (writePrefix && typeNamespace == "System" && typeName == "TimeSpan")
                WriteDataType(TCLRDataType.DATATYPE_TIMESPAN);
            else
            {
                if (structuredType.BasicType == CLRSigType.ElementType.VALUETYPE)
                {
                    if (writePrefix)
                        WriteDataType(TCLRDataType.DATATYPE_VALUETYPE);
                }
                else if (structuredType.BasicType == CLRSigType.ElementType.CLASS)
                {
                    if (writePrefix)
                        WriteDataType(TCLRDataType.DATATYPE_CLASS);
                }
                else
                    throw new NotSupportedException("Unexpected structured type basic type");
                if (rowNumber > 0x1fffffff)
                    throw new OverflowException("Too many type rows");
                WriteCompressedUInt((rowNumber << 2) | compressedTokenIndex);
            }
        }

        public void WriteType(CLRSigType type)
        {
            switch (type.BasicType)
            {
                case CLRSigType.ElementType.VOID:
                    WriteDataType(TCLRDataType.DATATYPE_VOID);
                    return;
                case CLRSigType.ElementType.BOOLEAN:
                    WriteDataType(TCLRDataType.DATATYPE_BOOLEAN);
                    return;
                case CLRSigType.ElementType.CHAR:
                    WriteDataType(TCLRDataType.DATATYPE_CHAR);
                    return;
                case CLRSigType.ElementType.I1:
                    WriteDataType(TCLRDataType.DATATYPE_I1);
                    return;
                case CLRSigType.ElementType.U1:
                    WriteDataType(TCLRDataType.DATATYPE_U1);
                    return;
                case CLRSigType.ElementType.I2:
                    WriteDataType(TCLRDataType.DATATYPE_I2);
                    return;
                case CLRSigType.ElementType.U2:
                    WriteDataType(TCLRDataType.DATATYPE_U2);
                    return;
                case CLRSigType.ElementType.I4:
                    WriteDataType(TCLRDataType.DATATYPE_I4);
                    return;
                case CLRSigType.ElementType.U4:
                    WriteDataType(TCLRDataType.DATATYPE_U4);
                    return;
                case CLRSigType.ElementType.I8:
                    WriteDataType(TCLRDataType.DATATYPE_I8);
                    return;
                case CLRSigType.ElementType.U8:
                    WriteDataType(TCLRDataType.DATATYPE_U8);
                    return;
                case CLRSigType.ElementType.R4:
                    WriteDataType(TCLRDataType.DATATYPE_R4);
                    return;
                case CLRSigType.ElementType.R8:
                    WriteDataType(TCLRDataType.DATATYPE_R8);
                    return;
                case CLRSigType.ElementType.STRING:
                    WriteDataType(TCLRDataType.DATATYPE_STRING);
                    return;
                case CLRSigType.ElementType.OBJECT:
                    WriteDataType(TCLRDataType.DATATYPE_OBJECT);
                    return;
                case CLRSigType.ElementType.I:
                    WriteSimpleDataType("System", "IntPtr");
                    return;
                case CLRSigType.ElementType.U:
                    WriteSimpleDataType("System", "UIntPtr");
                    return;
                default:
                    break;
            }

            // Not one of the above
            if (type is CLRSigTypeSZArray)
            {
                CLRSigTypeSZArray arrayType = (CLRSigTypeSZArray)type;
                WriteDataType(TCLRDataType.DATATYPE_SZARRAY);
                WriteType(arrayType.ContainedType);
            }
            else if (type is CLRSigTypeStructured)
            {
                WriteStructuredType((CLRSigTypeStructured)type, true);
            }
            else
                throw new NotSupportedException("Unsupported signature element type: " + type.GetType().Name);
        }
    }
}
