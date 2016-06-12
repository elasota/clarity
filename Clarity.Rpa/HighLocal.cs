using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighLocal
    {
        public enum ETypeOfType
        {
            Value,
            ByRef,
            TypedByRef,
        }

        private TypeSpecTag m_type;
        private ETypeOfType m_typeOfType;

        public TypeSpecTag Type { get { return m_type; } }
        public ETypeOfType TypeOfType { get { return m_typeOfType; } }

        public HighLocal()
        {
        }

        public HighLocal(TypeSpecTag type, ETypeOfType typeOfType)
        {
            m_type = type;
            m_typeOfType = typeOfType;
        }

        public void Write(HighFileBuilder builder, BinaryWriter writer)
        {
            writer.Write((byte)m_typeOfType);
            switch (m_typeOfType)
            {
                case ETypeOfType.ByRef:
                case ETypeOfType.Value:
                    writer.Write(builder.IndexTypeSpecTag(m_type));
                    break;
                case ETypeOfType.TypedByRef:
                    break;
                default:
                    throw new Exception();
            }
        }

        public void Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            ETypeOfType typeOfType = (ETypeOfType)reader.ReadByte();

            switch (typeOfType)
            {
                case ETypeOfType.ByRef:
                    m_typeOfType = ETypeOfType.ByRef;
                    m_type = catalog.GetTypeSpec(reader.ReadUInt32());
                    break;
                case ETypeOfType.TypedByRef:
                    m_typeOfType = ETypeOfType.TypedByRef;
                    break;
                case ETypeOfType.Value:
                    m_typeOfType = ETypeOfType.Value;
                    m_type = catalog.GetTypeSpec(reader.ReadUInt32());
                    break;
                default:
                    throw new Exception("Unrecognized type of type");
            }
        }

        public void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("local ");
            switch (m_typeOfType)
            {
                case ETypeOfType.ByRef:
                    dw.Write("byref ");
                    m_type.WriteDisassembly(dw);
                    break;
                case ETypeOfType.TypedByRef:
                    dw.Write("typedbyref");
                    break;
                case ETypeOfType.Value:
                    dw.Write("value ");
                    m_type.WriteDisassembly(dw);
                    break;
                default:
                    throw new Exception();
            }
            dw.WriteLine("");
        }
    }
}
