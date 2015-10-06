using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRSigTypeSimple : CLRSigType
    {
        public CLRSigTypeSimple(ElementType type)
        {
            BasicType = type;
        }
    }

    public class CLRSigTypeArray : CLRSigType
    {
        public uint Rank { get; private set; }
        public uint[] Sizes { get; private set; }
        public int[] LowBounds { get; private set; }
        public CLRSigType ContainedType { get; private set; }
        

        public CLRSigTypeArray(ElementType type, CLRSigType containedType)
        {
            BasicType = type;
            Rank = 1;
            Sizes = new uint[0];
            LowBounds = new int[0];
            ContainedType = containedType;
        }

        public CLRSigTypeArray(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            ContainedType = CLRSigType.Parse(parser, false);

            // ArrayShape (II.23.2.13)
            Rank = parser.ReadCompressedUInt();
            uint numSizes = parser.ReadCompressedUInt();
            if (numSizes > Rank)
                throw new ParseFailedException("Invalid array");

            Sizes = new uint[numSizes];
            for (uint i = 0; i < numSizes; i++)
                Sizes[i] = parser.ReadCompressedUInt();

            uint numLowBounds = parser.ReadCompressedUInt();
            if (numLowBounds > Rank)
                throw new ParseFailedException("Invalid array");

            LowBounds = new int[numLowBounds];
            for (uint i = 0; i < numLowBounds; i++)
                LowBounds[i] = parser.ReadCompressedInt();
        }
    }

    public class CLRSigTypeStructured : CLRSigType
    {
        public CLRTableRow TypeDefOrRefOrSpec { get; private set; }

        public CLRSigTypeStructured(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            TypeDefOrRefOrSpec = parser.ReadTypeDefOrRefOrSpecEncoded();
        }

        public CLRSigTypeStructured(ElementType basicType, CLRTableRow type)
        {
            BasicType = basicType;
            TypeDefOrRefOrSpec = type;
        }
    }

    public class CLRSigTypeFunctionPointer : CLRSigType
    {
        public CLRSigMethodDefOrRefSig MethodDefOrRefSig { get; private set; }

        public CLRSigTypeFunctionPointer(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            MethodDefOrRefSig = new CLRSigMethodDefOrRefSig(parser, CLRSigMethodDefOrRefSig.Kind.DefOrRef);
        }
    }

    public class CLRSigTypeGenericInstantiation : CLRSigType
    {
        public enum InstType
        {
            Class,
            ValueType,
        }

        public InstType InstantiationType { get; private set; }
        public CLRTableRow GenericType { get; private set; }
        public CLRSigType[] ArgTypes { get; private set; }

        public CLRSigTypeGenericInstantiation(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            ElementType instType = (ElementType)parser.NextByte();
            parser.ConsumeByte();
            if (instType == ElementType.CLASS)
                InstantiationType = InstType.Class;
            else if (instType == ElementType.VALUETYPE)
                InstantiationType = InstType.ValueType;
            else
                throw new ParseFailedException("Unexpected instantiation type");
            GenericType = parser.ReadTypeDefOrRefOrSpecEncoded();
            uint genArgCount = parser.ReadCompressedUInt();

            ArgTypes = new CLRSigType[genArgCount];
            for (uint i = 0; i < genArgCount; i++)
                ArgTypes[i] = CLRSigType.Parse(parser, false);
        }
    }

    public class CLRSigTypeVarOrMVar : CLRSigType
    {
        public uint Value { get; private set; }

        public CLRSigTypeVarOrMVar(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            Value = parser.ReadCompressedUInt();
        }
    }

    public class CLRSigTypePointer : CLRSigType
    {
        public uint Value { get; private set; }
        public CLRSigCustomMod[] CustomMods { get; private set; }
        public CLRSigType PointedToType { get; private set; }

        public CLRSigTypePointer(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            CustomMods = CLRSigType.ReadCustomMods(parser);
            PointedToType = CLRSigType.Parse(parser, true);
        }
    }

    public class CLRSigTypeSZArray : CLRSigType
    {
        public CLRSigCustomMod[] CustomMods { get; private set; }
        public CLRSigType ContainedType { get; private set; }

        public CLRSigTypeSZArray(ElementType type, CLRSignatureParser parser)
        {
            BasicType = type;

            CustomMods = CLRSigType.ReadCustomMods(parser);
            ContainedType = CLRSigType.Parse(parser, true);
        }
    }

    // II.23.2.12
    public abstract class CLRSigType
    {
        public enum ElementType
        {
            END = 0x0,
            VOID = 0x1,
            BOOLEAN = 0x2,
            CHAR = 0x3,
            I1 = 0x4,
            U1 = 0x5,
            I2 = 0x6,
            U2 = 0x7,
            I4 = 0x8,
            U4 = 0x9,
            I8 = 0xa,
            U8 = 0xb,
            R4 = 0xc,
            R8 = 0xd,
            STRING = 0xe,
            PTR = 0xf,
            BYREF = 0x10,
            VALUETYPE = 0x11,
            CLASS = 0x12,
            VAR = 0x13,
            ARRAY = 0x14,
            GENERICINST = 0x15,
            TYPEDBYREF = 0x16,
            I = 0x18,
            U = 0x19,
            FNPTR = 0x1b,
            OBJECT = 0x1c,
            SZARRAY = 0x1d,
            MVAR = 0x1e,
            CMOD_REQD = 0x1f,
            CMOD_OPT = 0x20,
            INTERNAL = 0x21,

            MODIFIER_BIT = 0x40,    // ORed with element types
            SENTINEL = 0x41,
            PINNED = 0x45,

            Special_SystemType = 0x50,
            Special_CustomAttribBoxedObject = 0x51,
            Special_CustomAttribField = 0x53,
            Special_CustomAttribProperty = 0x54,
            Special_CustomAttribEnum = 0x55,
        }

        public ElementType BasicType { get; protected set; }

        public static void ReadCustomModsAndConstraints(CLRSignatureParser parser, out CLRSigCustomMod[] outCustomMods, out CLRSigConstraint[] outConstraints)
        {
            List<CLRSigCustomMod> customMods = new List<CLRSigCustomMod>();
            List<CLRSigConstraint> constraints = new List<CLRSigConstraint>();

            byte nextToken = parser.NextByte();
            while (true)
            {
                if (nextToken == (byte)ElementType.PINNED)
                {
                    constraints.Add(new CLRSigConstraint(CLRSigConstraint.ConstraintTypeEnum.Pinned));
                    parser.ConsumeByte();
                }
                else if (nextToken == (byte)ElementType.CMOD_OPT || nextToken == (byte)ElementType.CMOD_REQD)
                {
                    customMods.Add(new CLRSigCustomMod(parser));
                    nextToken = parser.NextByte();
                }
                else
                    break;
            }
            outCustomMods = customMods.ToArray();
            outConstraints = constraints.ToArray();
        }

        public static CLRSigCustomMod[] ReadCustomMods(CLRSignatureParser parser)
        {
            List<CLRSigCustomMod> customMods = new List<CLRSigCustomMod>();

            byte nextToken = parser.NextByte();
            while (nextToken == (byte)ElementType.CMOD_OPT || nextToken == (byte)ElementType.CMOD_REQD)
            {
                customMods.Add(new CLRSigCustomMod(parser));
                nextToken = parser.NextByte();
                throw new NotImplementedException();
            }
            return customMods.ToArray();
        }

        public static CLRSigType Parse(CLRSignatureParser parser, bool permitVoid)
        {
            ElementType eType = (ElementType)parser.NextByte();
            parser.ConsumeByte();

            switch (eType)
            {
                case ElementType.BOOLEAN:
                case ElementType.CHAR:
                case ElementType.I1:
                case ElementType.U1:
                case ElementType.I2:
                case ElementType.U2:
                case ElementType.I4:
                case ElementType.U4:
                case ElementType.I8:
                case ElementType.U8:
                case ElementType.R4:
                case ElementType.R8:
                case ElementType.I:
                case ElementType.U:
                case ElementType.OBJECT:
                case ElementType.STRING:
                    return new CLRSigTypeSimple(eType);
                case ElementType.VOID:
                    if (!permitVoid)
                        throw new ParseFailedException("Unexpected void type");
                    return new CLRSigTypeSimple(eType);
                case ElementType.ARRAY:
                    return new CLRSigTypeArray(eType, parser);
                case ElementType.CLASS:
                case ElementType.VALUETYPE:
                    return new CLRSigTypeStructured(eType, parser);
                case ElementType.FNPTR:
                    return new CLRSigTypeFunctionPointer(eType, parser);
                case ElementType.GENERICINST:
                    return new CLRSigTypeGenericInstantiation(eType, parser);
                case ElementType.MVAR:
                case ElementType.VAR:
                    return new CLRSigTypeVarOrMVar(eType, parser);
                case ElementType.PTR:
                    return new CLRSigTypePointer(eType, parser);
                case ElementType.SZARRAY:
                    return new CLRSigTypeSZArray(eType, parser);
                default:
                    throw new ParseFailedException("Unexpected sig type");
            }
        }
    }
}
