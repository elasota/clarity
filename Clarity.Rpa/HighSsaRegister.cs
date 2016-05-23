using System;
using System.IO;

namespace Clarity.Rpa
{
    public class HighSsaRegister
    {
        private TypeSpecTag m_type;
        private HighValueType m_valueType;
        private object m_constValue;

        public TypeSpecTag Type { get { return m_type; } }
        public HighValueType ValueType { get { return m_valueType; } }
        public object ConstantValue { get { return m_constValue; } }

        public bool IsConstant
        {
            get
            {
                switch (m_valueType)
                {
                    case HighValueType.ConstantString:
                    case HighValueType.ConstantValue:
                    case HighValueType.Null:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public HighSsaRegister(HighValueType valueType, TypeSpecTag type, object constValue)
        {
            m_valueType = valueType;
            m_type = type;
            m_constValue = constValue;

            if ((valueType == HighValueType.ConstantString || valueType == HighValueType.ConstantValue) && constValue == null)
                throw new ArgumentException("Missing value for constant SSA");
        }

        public static HighSsaRegister ReadDestinationDef(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            HighValueType vt = (HighValueType)reader.ReadByte();
            TypeSpecTag type = catalog.GetTypeSpec(reader.ReadUInt32());

            switch (vt)
            {
                case HighValueType.ManagedPtr:
                case HighValueType.ValueValue:
                case HighValueType.ReferenceValue:
                    break;
                default:
                    throw new RpaLoadException("Invalid SSA destination type");
            }

            return new HighSsaRegister(vt, type, null);
        }

        public void WriteDestinationDef(HighFileBuilder fileBuilder, HighRegionBuilder regionBuilder, BinaryWriter writer)
        {
            if (this.IsConstant)
                throw new Exception("Can't use constant as a destination");

            writer.Write((byte)m_valueType);
            writer.Write(fileBuilder.IndexTypeSpecTag(m_type));
        }

        public void WriteConstant(HighFileBuilder fileBuilder, HighRegionBuilder regionBuilder, BinaryWriter writer)
        {
            if (!this.IsConstant)
                throw new Exception("Can't use non-constant as a constant");
            writer.Write((byte)m_valueType);
            writer.Write(fileBuilder.IndexTypeSpecTag(m_type));

            if (m_valueType == HighValueType.Null)
            {
            }
            else if (m_valueType == HighValueType.ConstantString)
                writer.Write(fileBuilder.IndexString((string)m_constValue));
            else if (m_valueType == HighValueType.ConstantValue)
            {
                TypeSpecClassTag classTag = (TypeSpecClassTag)m_type;
                TypeNameTag className = classTag.TypeName;
                string classNameStr = className.TypeName;

                if (classNameStr == "SByte")
                    writer.Write((sbyte)m_constValue);
                else if (classNameStr == "Byte")
                    writer.Write((byte)m_constValue);
                else if (classNameStr == "Int16")
                    writer.Write((short)m_constValue);
                else if (classNameStr == "UInt16")
                    writer.Write((ushort)m_constValue);
                else if (classNameStr == "Int32")
                    writer.Write((int)m_constValue);
                else if (classNameStr == "UInt32")
                    writer.Write((uint)m_constValue);
                else if (classNameStr == "Int64")
                    writer.Write((long)m_constValue);
                else if (classNameStr == "UInt64")
                    writer.Write((ulong)m_constValue);
                else if (classNameStr == "IntPtr")
                    writer.Write((long)m_constValue);
                else if (classNameStr == "UIntPtr")
                    writer.Write((ulong)m_constValue);
                else if (classNameStr == "Single")
                    writer.Write((float)m_constValue);
                else if (classNameStr == "Double")
                    writer.Write((double)m_constValue);
                else
                    throw new ArgumentException();
            }
            else
                throw new Exception();
        }

        public static HighSsaRegister ReadConstant(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            HighValueType vt = (HighValueType)reader.ReadByte();
            TypeSpecTag type = catalog.GetTypeSpec(reader.ReadUInt32());

            TypeNameTag typeName = null;
            if (vt != HighValueType.Null)
            {
                TypeSpecClassTag classTag = type as TypeSpecClassTag;
                if (classTag == null)
                    throw new Exception("Constant is not a class tag");
                typeName = classTag.TypeName;

                if (classTag.ArgTypes.Length != 0 || typeName.ContainerType != null || typeName.AssemblyName != "mscorlib" || typeName.TypeNamespace != "System")
                    throw new Exception("Constant type unrecognized");
            }

            switch (vt)
            {
                case HighValueType.Null:
                    return new HighSsaRegister(HighValueType.Null, type, null);
                case HighValueType.ConstantString:
                    {
                        if (typeName.TypeName != "String")
                            throw new Exception("Constant type mismatch");
                        string str = catalog.GetString(reader.ReadUInt32());
                        return new HighSsaRegister(HighValueType.ConstantString, type, str);
                    }
                case HighValueType.ConstantValue:
                    {
                        string classNameStr = typeName.TypeName;
                        object constValue;

                        if (classNameStr == "SByte")
                            constValue = reader.ReadSByte();
                        else if (classNameStr == "Byte")
                            constValue = reader.ReadByte();
                        else if (classNameStr == "Int16")
                            constValue = reader.ReadInt16();
                        else if (classNameStr == "UInt16")
                            constValue = reader.ReadUInt16();
                        else if (classNameStr == "Int32")
                            constValue = reader.ReadInt32();
                        else if (classNameStr == "UInt32")
                            constValue = reader.ReadUInt32();
                        else if (classNameStr == "Int64")
                            constValue = reader.ReadInt64();
                        else if (classNameStr == "UInt64")
                            constValue = reader.ReadUInt64();
                        else if (classNameStr == "IntPtr")
                            constValue = reader.ReadInt64();
                        else if (classNameStr == "UIntPtr")
                            constValue = reader.ReadUInt64();
                        else if (classNameStr == "Single")
                            constValue = reader.ReadSingle();
                        else if (classNameStr == "Double")
                            constValue = reader.ReadDouble();
                        else
                            throw new Exception("Invalid constant");
                        return new HighSsaRegister(HighValueType.ConstantValue, type, constValue);
                    }
                default:
                    throw new Exception("Invalid constant");
            }
        }
    }
}
