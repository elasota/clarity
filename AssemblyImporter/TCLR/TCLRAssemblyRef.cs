using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRAssemblyRef : TCLRMetaItem
    {
        public TCLRString name;
        //ushort pad;
        public TCLRVersion version;

        public override void Write(BinaryWriter writer)
        {
            name.Write(writer);
            writer.Write((ushort)0);
            version.Write(writer);
        }
    }
}
