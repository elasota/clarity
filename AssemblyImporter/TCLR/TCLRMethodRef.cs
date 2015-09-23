using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRMethodRef : TCLRMetaItem
    {
        public TCLRString name;
        public TCLRIndex container;    // TBL_TypeRef
        public TCLRSig sig;
        //ushort pad

        public override void Write(BinaryWriter writer)
        {
            name.Write(writer);
            container.Write(writer);
            sig.Write(writer);
            writer.Write((ushort)0);
        }
    }
}
