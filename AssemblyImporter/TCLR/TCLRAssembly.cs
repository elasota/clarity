using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRAssembly
    {
        public const uint c_Flags_NeedReboot = 0x00000001;
        public const uint c_Flags_Patch = 0x00000002;
        public const uint c_Flags_BigEndian = 0x80000080;

        public uint headerCRC;
        public uint assemblyCRC;
        public uint flags;

        public uint nativeMethodsChecksum;
        public uint patchEntryOffset;

        public TCLRVersion version;

        public TCLRString assemblyName;
        public ushort stringTableVersion;

        public TCLROffsetLong[] startOfTables;// [ TBL_Max ];
        public uint numOfPatchedMethods;

        public byte[] paddingOfTables;// [ ((TBL_Max - 1) + 3) / 4 * 4 ]

        public TCLRAssembly()
        {
            startOfTables = new TCLROffsetLong[(int)TCLRTablesEnum.TBL_Max];
            paddingOfTables = new byte[(((int)TCLRTablesEnum.TBL_Max - 1) + 3) / 4 * 4];
        }

        public void Write(BinaryWriter writer)
        {
            byte[] marker = new byte[8] { 0x4d, 0x53, 0x53, 0x70, 0x6f, 0x74, 0x31, 0x00 };

            writer.Write(marker);
            writer.Write(headerCRC);
            writer.Write(assemblyCRC);
            writer.Write(flags);
            writer.Write(nativeMethodsChecksum);
            writer.Write(patchEntryOffset);
            version.Write(writer);
            assemblyName.Write(writer);
            writer.Write(stringTableVersion);

            foreach (TCLROffsetLong v in startOfTables)
                v.Write(writer);
            writer.Write(numOfPatchedMethods);
            writer.Write(paddingOfTables);
        }
    }
}
