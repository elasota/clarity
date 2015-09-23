using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.23.3
    public class CLRSigCustomAttributeFixedArg
    {
        public CLRSigCustomAttributeElem[] Elements { get; private set; }

        public CLRSigCustomAttributeFixedArg(CLRSignatureParser parser, CLRSigType argType)
        {
            CLRSigType elemType = argType;

            if (argType.BasicType == CLRSigType.ElementType.SZARRAY)
            {
                uint count = parser.ReadU32();
                if (count != 0xffffffff)
                    Elements = new CLRSigCustomAttributeElem[count];
                else
                    count = 0;
                elemType = ((CLRSigTypeArray)argType).ContainedType;
            }
            else
                Elements = new CLRSigCustomAttributeElem[1];

            if (Elements != null)
            {
                for (long i = 0; i < Elements.LongLength; i++)
                    Elements[i] = new CLRSigCustomAttributeElem(parser, elemType);
            }
        }
    }
}
