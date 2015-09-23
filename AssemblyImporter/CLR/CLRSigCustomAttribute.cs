using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    // II.23.3
    public class CLRSigCustomAttribute
    {
        public CLRTableRow Constructor { get; private set; }
        public CLRSigCustomAttributeFixedArg[] FixedArgs { get; private set; }
        public CLRSigCustomAttributeNamedArg[] NamedArgs { get; private set; }

        public CLRSigCustomAttribute(CLRSignatureParser parser, CLRTableRow constructor)
        {
            Constructor = constructor;

            if (parser.ReadU16() != 0x0001)
                throw new NotSupportedException("Unusual CA prolog");

            CLRSigMethodDefOrRefSig methodSig = null;
            if (constructor is CLRMethodDefRow)
            {
                CLRMethodDefRow methodDef = (CLRMethodDefRow)constructor;
                methodSig = methodDef.Signature;
            }
            else if (constructor is CLRMemberRefRow)
            {
                CLRMemberRefRow memberRef = (CLRMemberRefRow)constructor;
                methodSig = memberRef.MethodSig;
                if (methodSig == null)
                    throw new ParseFailedException("Bad CA constructor");
            }
            else
                throw new ParseFailedException("Missing CA constructor");

            int numFixedArgs = methodSig.ParamTypes.Length;
            FixedArgs = new CLRSigCustomAttributeFixedArg[numFixedArgs];
            for (int i = 0; i < numFixedArgs; i++)
            {
                CLRSigType paramType = methodSig.ParamTypes[i].Type;
                CLRSigType containedType = paramType;
                if (paramType is CLRSigTypeArray)
                    containedType = ((CLRSigTypeArray)paramType).ContainedType;
                FixedArgs[i] = new CLRSigCustomAttributeFixedArg(parser, paramType);
            }

            uint numNamedArgs = parser.ReadU16();
            NamedArgs = new CLRSigCustomAttributeNamedArg[numNamedArgs];
            for (uint i = 0; i < numNamedArgs; i++)
                NamedArgs[i] = new CLRSigCustomAttributeNamedArg(parser);
        }
    }
}
