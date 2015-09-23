using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public struct TCLRSig
    {
        ushort value;

        public TCLRSig(uint loc)
        {
            if (loc >= 0xffff)
                throw new OverflowException("Too many signatures");
            value = (ushort)loc;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public static TCLRSig Empty
        {
            get
            {
                TCLRSig sig;
                sig.value = 0xffff;
                return sig;
            }
        }
    }
}
