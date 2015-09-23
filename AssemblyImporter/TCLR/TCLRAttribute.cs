using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRAttribute : TCLRMetaItem
    {
        ushort ownerType;       // one of TBL_TypeDef, TBL_MethodDef, or TBL_FieldDef.
        ushort ownerIdx;        // TBL_TypeDef | TBL_MethodDef | TBL_FielfDef
        ushort constructor;
        TCLRSig data;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ownerType);
            writer.Write(ownerIdx);
            writer.Write(constructor);
            data.Write(writer);
        }
    }
}
