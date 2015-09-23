using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.3
    public class CLRSigCustomAttributeElem
    {
        public CLRSigType.ElementType ElementType { get; private set; }
        public object Value { get; private set; }

        public CLRSigCustomAttributeElem(CLRSignatureParser parser, CLRSigType overallType)
        {
            Parse(parser, overallType);
        }

        private static bool IsValidFieldOrPropType(CLRSigType.ElementType t)
        {
            if (t != CLRSigType.ElementType.BOOLEAN &&
                            t != CLRSigType.ElementType.CHAR &&
                            t != CLRSigType.ElementType.STRING &&
                            t != CLRSigType.ElementType.I1 &&
                            t != CLRSigType.ElementType.I2 &&
                            t != CLRSigType.ElementType.I4 &&
                            t != CLRSigType.ElementType.I8 &&
                            t != CLRSigType.ElementType.U1 &&
                            t != CLRSigType.ElementType.U2 &&
                            t != CLRSigType.ElementType.U4 &&
                            t != CLRSigType.ElementType.U8 &&
                            t != CLRSigType.ElementType.R4 &&
                            t != CLRSigType.ElementType.R8)
                return false;
            return true;
        }

        public static CLRSigType ReadFieldOrPropType(CLRSignatureParser parser)
        {
            CLRSigType.ElementType basicType = (CLRSigType.ElementType)parser.ReadU8();

            switch (basicType)
            {
                case CLRSigType.ElementType.BOOLEAN:
                case CLRSigType.ElementType.CHAR:
                case CLRSigType.ElementType.R4:
                case CLRSigType.ElementType.R8:
                case CLRSigType.ElementType.I1:
                case CLRSigType.ElementType.U1:
                case CLRSigType.ElementType.I2:
                case CLRSigType.ElementType.U2:
                case CLRSigType.ElementType.I4:
                case CLRSigType.ElementType.U4:
                case CLRSigType.ElementType.I8:
                case CLRSigType.ElementType.U8:
                case CLRSigType.ElementType.STRING:
                    return new CLRSigTypeSimple(basicType);
                case CLRSigType.ElementType.Special_CustomAttribBoxedObject:
                    return new CLRSigTypeSimple(CLRSigType.ElementType.OBJECT);
                case CLRSigType.ElementType.Special_CustomAttribEnum:
                    {
                        string typeName = ReadUTF8String(parser);
                        throw new NotImplementedException();
                    }
                    break;
                case CLRSigType.ElementType.Special_SystemType:
                    // CLARITYTODO: Look up System.Type
                    throw new NotImplementedException();
                case CLRSigType.ElementType.SZARRAY:
                    {
                        CLRSigType containedType = ReadFieldOrPropType(parser);
                        return new CLRSigTypeArray(CLRSigType.ElementType.SZARRAY, containedType);
                    }
                default:
                    throw new ParseFailedException("Unexpected field or prop type");
            }
        }

        public static string ReadUTF8String(CLRSignatureParser parser)
        {
            byte firstByte = parser.NextByte();
            if (firstByte == 0xff)
                return null;
            else
            {
                uint stringLengthBytes = parser.ReadCompressedUInt();
                if (stringLengthBytes == 0)
                    return "";
                else
                {
                    byte[] utf8chars = new byte[stringLengthBytes];
                    parser.ReadBytes(utf8chars, stringLengthBytes);
                    return System.Text.Encoding.UTF8.GetString(utf8chars);
                }
            }
        }

        private void Parse(CLRSignatureParser parser, CLRSigType elemType)
        {
            ElementType = elemType.BasicType;

            switch (elemType.BasicType)
            {
                case CLRSigType.ElementType.BOOLEAN:
                    Value = (parser.ReadU8() != 0);
                    break;
                case CLRSigType.ElementType.CHAR:
                    Value = (char)(parser.ReadU16());
                    break;
                case CLRSigType.ElementType.R4:
                    Value = parser.ReadF32();
                    break;
                case CLRSigType.ElementType.R8:
                    Value = parser.ReadF64();
                    break;
                case CLRSigType.ElementType.I1:
                    Value = parser.ReadS8();
                    break;
                case CLRSigType.ElementType.U1:
                    Value = parser.ReadU8();
                    break;
                case CLRSigType.ElementType.I2:
                    Value = parser.ReadS16();
                    break;
                case CLRSigType.ElementType.U2:
                    Value = parser.ReadU16();
                    break;
                case CLRSigType.ElementType.I4:
                    Value = parser.ReadS32();
                    break;
                case CLRSigType.ElementType.U4:
                    Value = parser.ReadU32();
                    break;
                case CLRSigType.ElementType.I8:
                    Value = parser.ReadS64();
                    break;
                case CLRSigType.ElementType.U8:
                    Value = parser.ReadU64();
                    break;
                case CLRSigType.ElementType.STRING:
                    Value = ReadUTF8String(parser);
                    break;
                case CLRSigType.ElementType.OBJECT:
                    Parse(parser, ReadFieldOrPropType(parser));
                    return;
                case CLRSigType.ElementType.CLASS:
                case CLRSigType.ElementType.VALUETYPE:
                    {
                        CLRSigTypeStructured st = (CLRSigTypeStructured)elemType;
                        CLRTableRow underlyingType = st.TypeDefOrRefOrSpec;
                        string typeNamespace, typeName;
                        if (underlyingType is CLRTypeDefRow)
                        {
                            CLRTypeDefRow typeDef = (CLRTypeDefRow)underlyingType;
                            typeNamespace = typeDef.TypeNamespace;
                            typeName = typeDef.TypeName;
                        }
                        else if (underlyingType is CLRTypeRefRow)
                        {
                            CLRTypeRefRow typeRef = (CLRTypeRefRow)underlyingType;
                            typeNamespace = typeRef.TypeNamespace;
                            typeName = typeRef.TypeName;
                        }
                        else
                            throw new ParseFailedException("Unusual CA type");

                        if (typeNamespace == "System" && typeName == "Type")
                        {
                            Parse(parser, new CLRSigTypeSimple(CLRSigType.ElementType.STRING));
                            return;
                        }

                        // Must be an enum
                        {
                            CLRTypeDefRow enumType;

                            if (underlyingType is CLRTypeDefRow)
                                enumType = (CLRTypeDefRow)underlyingType;
                            else if (underlyingType is CLRTypeRefRow)
                            {
                                CLRTypeRefRow typeRef = (CLRTypeRefRow)underlyingType;
                                if (typeRef.Resolution == null)
                                    throw new ParseFailedException("Custom attribute references unresolved enum type");
                                enumType = typeRef.Resolution;
                            }
                            else
                                throw new ParseFailedException("Unexpected CA underlying type");

                            CLRSigType valueType = null;

                            foreach (CLRFieldRow field in enumType.Fields)
                            {
                                if (!field.Static)
                                {
                                    valueType = field.Signature.Type;
                                    break;
                                }
                            }

                            if (valueType == null)
                                throw new ParseFailedException("Unknown CA enum type");
                            Parse(parser, valueType);
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException("Unsupported custom attrib type");
            }
        }
    }
}
