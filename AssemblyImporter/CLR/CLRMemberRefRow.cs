﻿using System;

namespace AssemblyImporter.CLR
{
    // II.22.25
    public class CLRMemberRefRow : CLRTableRow
    {
        public CLRTableRow Class { get; private set; }
        public string Name { get; private set; }
        public CLRSigFieldSig FieldSig { get; private set; }
        public CLRSigMethodDefOrRefSig MethodSig { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            Class = parser.ReadMemberRefParent();
            Name = parser.ReadString();

            using (CLRSignatureParser sigParser = new CLRSignatureParser(parser.ReadBlob(), parser.Tables))
            {
                if (sigParser.NextByte() == 0x06)
                    FieldSig = new CLRSigFieldSig(sigParser);
                else
                    MethodSig = new CLRSigMethodDefOrRefSig(sigParser, CLRSigMethodDefOrRefSig.Kind.Ref);
            }
        }
    }
}
