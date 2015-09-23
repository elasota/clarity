using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRSigCustomAttributeNamedArg
    {
        public enum ArgKindEnum
        {
            Field,
            Property
        }

        public ArgKindEnum ArgKind { get; private set; }
        public string Name { get; private set; }
        public CLRSigCustomAttributeFixedArg Arg { get; private set; }

        public CLRSigCustomAttributeNamedArg(CLRSignatureParser parser)
        {
            CLRSigType.ElementType baseType = (CLRSigType.ElementType)parser.ReadU8();

            if (baseType == CLRSigType.ElementType.Special_CustomAttribField)
                ArgKind = ArgKindEnum.Field;
            else if (baseType == CLRSigType.ElementType.Special_CustomAttribProperty)
                ArgKind = ArgKindEnum.Property;
            else
                throw new ParseFailedException("Unusual named arg type");

            CLRSigType argType = CLRSigCustomAttributeElem.ReadFieldOrPropType(parser);
            Name = CLRSigCustomAttributeElem.ReadUTF8String(parser);
            Arg = new CLRSigCustomAttributeFixedArg(parser, argType);
        }
    }
}
