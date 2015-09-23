﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.TCLR
{
    public enum TCLRTablesEnum
    {
        TBL_AssemblyRef = 0x00000000,
        TBL_TypeRef = 0x00000001,
        TBL_FieldRef = 0x00000002,
        TBL_MethodRef = 0x00000003,
        TBL_TypeDef = 0x00000004,
        TBL_FieldDef = 0x00000005,
        TBL_MethodDef = 0x00000006,
        TBL_Attributes = 0x00000007,
        TBL_TypeSpec = 0x00000008,
        TBL_Resources = 0x00000009,
        TBL_ResourcesData = 0x0000000A,
        TBL_Strings = 0x0000000B,
        TBL_Signatures = 0x0000000C,
        TBL_ByteCode = 0x0000000D,
        TBL_ResourcesFiles = 0x0000000E,
        TBL_EndOfAssembly = 0x0000000F,
        TBL_Max = 0x00000010,
    }
}
