using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public class HighTypeDef
    {
        public enum EnumUnderlyingType
        {
            Int8,
            UInt8,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,

            NotEnum,
        }

        private TypeSemantics m_semantics;
        private TypeNameTag m_typeName;
        private TypeSpecClassTag m_parentClass;
        private TypeSpecClassTag[] m_parentInterfaces;
        private HighMethod[] m_methods;
        private HighField[] m_instanceFields;
        private HighField[] m_staticFields;
        private HighClassVtableSlot[] m_replacedSlots;
        private HighClassVtableSlot[] m_newSlots;
        private HighInterfaceImplementation[] m_interfaceImpls;
        private HighEnumLiteral[] m_enumLiterals;
        private uint m_numGenericParameters;
        private HighVariance[] m_genericParameterVariance;
        private bool m_isSealed;
        private bool m_isAbstract;
        private EnumUnderlyingType m_underlyingType;
        private MethodSignatureTag m_delegateSignature;
        private bool m_isMulticastDelegate;

        public TypeNameTag TypeName { get { return m_typeName; } }
        public TypeSemantics Semantics { get { return m_semantics; } }
        public TypeSpecClassTag ParentClass { get { return m_parentClass; } }
        public TypeSpecClassTag[] ParentInterfaces { get { return m_parentInterfaces; } }
        public HighMethod[] Methods { get { return m_methods; } }
        public HighField[] InstanceFields { get { return m_instanceFields; } }
        public HighField[] StaticFields { get { return m_staticFields; } }
        public HighClassVtableSlot[] ReplacedSlots { get { return m_replacedSlots; } }
        public HighClassVtableSlot[] NewSlots { get { return m_newSlots; } }
        public HighInterfaceImplementation[] InterfaceImpls { get { return m_interfaceImpls; } }
        public uint NumGenericParameters { get { return m_numGenericParameters; } }
        public bool IsSealed { get { return m_isSealed; } }
        public bool IsAbstract { get { return m_isAbstract; } }
        public EnumUnderlyingType UnderlyingType { get { return m_underlyingType; } }
        public MethodSignatureTag DelegateSignature { get { return m_delegateSignature; } }
        public bool IsMulticastDelegate { get { return m_isMulticastDelegate; } }

        public HighVariance GenericParameterVariance(int index) { return m_genericParameterVariance[index]; }
        public HighVariance GenericParameterVariance(uint index) { return m_genericParameterVariance[index]; }

        public void Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            m_semantics = (TypeSemantics)reader.ReadByte();
            switch (m_semantics)
            {
                case TypeSemantics.Class:
                case TypeSemantics.Interface:
                case TypeSemantics.Struct:
                case TypeSemantics.Enum:
                case TypeSemantics.Delegate:
                    break;
                default:
                    throw new RpaLoadException("Invalid type semantics");
            }

            m_typeName = catalog.GetTypeName(reader.ReadUInt32());

            if (m_typeName.AssemblyName != catalog.AssemblyName)
                throw new Exception("Type definition outside of assembly");

            m_numGenericParameters = m_typeName.NumGenericParameters;

            m_underlyingType = EnumUnderlyingType.NotEnum;
            if (m_semantics == TypeSemantics.Delegate)
            {
                this.ReadDelegate(rpa, catalog, reader);
            }
            else if (m_semantics == TypeSemantics.Enum)
            {
                this.ReadEnum(rpa, catalog, reader);
            }
            else
            {
                this.ReadClassDefinitions(rpa, catalog, reader);

                if (m_semantics == Clarity.Rpa.TypeSemantics.Class || m_semantics == Clarity.Rpa.TypeSemantics.Struct)
                    this.ReadClassStatics(rpa, catalog, reader);
            }
        }

        private void ReadEnum(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            byte underlyingTypeSymbol = reader.ReadByte();
            bool isSigned;
            uint literalSize;

            switch ((EnumUnderlyingType)underlyingTypeSymbol)
            {
                case EnumUnderlyingType.Int8:
                case EnumUnderlyingType.Int16:
                case EnumUnderlyingType.Int32:
                case EnumUnderlyingType.Int64:
                    isSigned = true;
                    break;
                case EnumUnderlyingType.UInt8:
                case EnumUnderlyingType.UInt16:
                case EnumUnderlyingType.UInt32:
                case EnumUnderlyingType.UInt64:
                    isSigned = false;
                    break;
                default:
                    throw new Exception("Unrecognized enum underlying type");
            }

            switch ((EnumUnderlyingType)underlyingTypeSymbol)
            {
                case EnumUnderlyingType.Int8:
                case EnumUnderlyingType.UInt8:
                    literalSize = 1;
                    break;
                case EnumUnderlyingType.Int16:
                case EnumUnderlyingType.UInt16:
                    literalSize = 2;
                    break;
                case EnumUnderlyingType.Int32:
                case EnumUnderlyingType.UInt32:
                    literalSize = 4;
                    break;
                case EnumUnderlyingType.Int64:
                case EnumUnderlyingType.UInt64:
                    literalSize = 8;
                    break;
                default:
                    throw new Exception();
            }

            uint numLiterals = reader.ReadUInt32();
            HighEnumLiteral[] literals = new HighEnumLiteral[numLiterals];

            for (uint i = 0; i < numLiterals; i++)
                literals[i] = HighEnumLiteral.Read(catalog, reader, literalSize, isSigned);

            m_enumLiterals = literals;
            m_underlyingType = (EnumUnderlyingType)underlyingTypeSymbol;
        }

        private void ReadGenericParameterVariance(BinaryReader reader)
        {
            m_genericParameterVariance = new HighVariance[m_numGenericParameters];

            for (uint i = 0; i < m_numGenericParameters; i++)
            {
                HighVariance variance = (HighVariance)reader.ReadByte();
                switch (variance)
                {
                    case HighVariance.Contravariant:
                    case HighVariance.Covariant:
                    case HighVariance.None:
                        break;
                    default:
                        throw new RpaLoadException("Invalid variance");
                }
                m_genericParameterVariance[i] = variance;
            }
        }

        private void ReadDelegate(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            m_isMulticastDelegate = reader.ReadBoolean();
            ReadGenericParameterVariance(reader);
            m_delegateSignature = catalog.GetMethodSignature(reader.ReadUInt32());
            if (m_delegateSignature.NumGenericParameters > 0)
                throw new Exception("Delegate has generic parameters");
        }

        private void ReadClassStatics(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            uint numStaticFields = reader.ReadUInt32();

            m_staticFields = new HighField[numStaticFields];
            for (uint i = 0; i < numStaticFields; i++)
            {
                HighField fld = HighField.Read(rpa, catalog, reader);
                m_staticFields[i] = fld;
            }
        }

        private void ReadClassDefinitions(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            m_parentClass = null;
            if (m_semantics == TypeSemantics.Class)
            {
                m_isSealed = reader.ReadBoolean();
                m_isAbstract = reader.ReadBoolean();

                uint parentClassIndex = reader.ReadUInt32();
                if (parentClassIndex == 0)
                {
                    if (m_typeName.AssemblyName != "mscorlib"
                        || m_typeName.TypeNamespace != "System"
                        || m_typeName.TypeName != "Object")
                        throw new Exception("Parentless class is not System.Object");
                }
                else
                {
                    TypeSpecTag parentClass = catalog.GetTypeSpec(parentClassIndex - 1);
                    TypeSpecClassTag clsTag = parentClass as TypeSpecClassTag;
                    if (clsTag == null)
                        throw new Exception("Parent class spec wasn't a class");

                    m_parentClass = clsTag;
                }
            }

            if (m_semantics == TypeSemantics.Struct || m_semantics == TypeSemantics.Enum)
            {
                m_isSealed = true;
                m_isAbstract = false;
            }

            if (m_semantics == TypeSemantics.Interface)
            {
                m_isSealed = false;
                m_isAbstract = true;

                ReadGenericParameterVariance(reader);
            }

            uint numInterfaces = reader.ReadUInt32();

            TypeSpecClassTag[] interfaces = new TypeSpecClassTag[numInterfaces];

            for (uint i = 0; i < numInterfaces; i++)
            {
                TypeSpecTag typeSpec = catalog.GetTypeSpec(reader.ReadUInt32());
                TypeSpecClassTag classTag = typeSpec as TypeSpecClassTag;
                if (classTag == null)
                    throw new RpaLoadException("Interface implementation is not a class tag");
                interfaces[i] = classTag;
            }
            m_parentInterfaces = interfaces;

            this.ReadVtableThunks(rpa, catalog, reader);

            if (m_semantics == TypeSemantics.Class || m_semantics == TypeSemantics.Struct)
            {
                uint numMethods = reader.ReadUInt32();

                m_methods = new HighMethod[numMethods];

                for (uint i = 0; i < numMethods; i++)
                {
                    HighMethod method = HighMethod.Read(rpa, catalog, reader, m_typeName);
                    m_methods[i] = method;
                }

                uint numInstanceFields = reader.ReadUInt32();

                m_instanceFields = new HighField[numInstanceFields];

                for (uint i = 0; i < numInstanceFields; i++)
                {
                    HighField field = HighField.Read(rpa, catalog, reader);
                    m_instanceFields[i] = field;
                }

                this.ReadInterfaceImplementations(rpa, catalog, reader);
            }
        }

        private void ReadInterfaceImplementations(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            if (m_semantics != TypeSemantics.Class && m_semantics != TypeSemantics.Struct)
                return;

            uint numInterfaces = reader.ReadUInt32();

            m_interfaceImpls = new HighInterfaceImplementation[numInterfaces];
            for (uint i = 0; i < numInterfaces; i++)
            {
                HighInterfaceImplementation impl = new HighInterfaceImplementation();
                impl.Read(rpa, catalog, reader);
                m_interfaceImpls[i] = impl;
            }
        }

        private void ReadVtableThunks(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            bool isInterface = (m_semantics == TypeSemantics.Interface);

            if (!isInterface)
            {
                if (m_semantics != TypeSemantics.Class && m_semantics != TypeSemantics.Struct)
                    throw new ArgumentException();
            }

            if (!isInterface)
            {
                uint numReplacedSlots = reader.ReadUInt32();

                m_replacedSlots = new HighClassVtableSlot[numReplacedSlots];
                for (uint i = 0; i < numReplacedSlots; i++)
                {
                    HighClassVtableSlot mapping = HighClassVtableSlot.Read(rpa, catalog, reader, isInterface);
                    m_replacedSlots[i] = mapping;
                }
            }

            uint numNewSlots = reader.ReadUInt32();

            m_newSlots = new HighClassVtableSlot[numNewSlots];

            for (uint i = 0; i < numNewSlots; i++)
            {
                HighClassVtableSlot mapping = HighClassVtableSlot.Read(rpa, catalog, reader, isInterface);
                m_newSlots[i] = mapping;
            }
        }
    }
}
