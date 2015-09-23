using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRTypeRef : TCLRMetaItem
    {
        public TCLRString name;
        public TCLRString nameSpace;
        public TCLRIndex scope;    // (TBL_AssemblyRef/TBL_TypeRef --> 0x8000)
        //ushort pad;

        public override void Write(BinaryWriter writer)
        {
            name.Write(writer);
            nameSpace.Write(writer);
            scope.Write(writer);
            writer.Write((ushort)0);
        }
    }
}
