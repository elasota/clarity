using System;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRTypeSpec : TCLRMetaItem
    {
        public TCLRSig sig;
        //ushort pad

        public override void Write(BinaryWriter writer)
        {
            sig.Write(writer);
            writer.Write((ushort)0);
        }
    }
}
