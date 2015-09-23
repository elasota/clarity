using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRFieldDef : TCLRMetaItem
    {
        public const ushort FD_Scope_Mask = 0x0007;
        public const ushort FD_Scope_PrivateScope = 0x0000; // Member not referenceable.
        public const ushort FD_Scope_Private = 0x0001;      // Accessible only by the parent type.
        public const ushort FD_Scope_FamANDAssem = 0x0002;  // Accessible by sub-types only in this Assembly.
        public const ushort FD_Scope_Assembly = 0x0003;     // Accessibly by anyone in the Assembly.
        public const ushort FD_Scope_Family = 0x0004;       // Accessible only by type and sub-types.
        public const ushort FD_Scope_FamORAssem = 0x0005;   // Accessibly by sub-types anywhere, plus anyone in assembly.
        public const ushort FD_Scope_Public = 0x0006;       // Accessibly by anyone who has visibility to this scope.

        public const ushort FD_NotSerialized = 0x0008;      // Field does not have to be serialized when type is remoted.

        public const ushort FD_Static = 0x0010;             // Defined on type, else per instance.
        public const ushort FD_InitOnly = 0x0020;           // Field may only be initialized, not written to after init.
        public const ushort FD_Literal = 0x0040;            // Value is compile time constant.

        public const ushort FD_SpecialName = 0x0100;        // field is special.  Name describes how.
        public const ushort FD_HasDefault = 0x0200;         // Field has default.
        public const ushort FD_HasFieldRVA = 0x0400;        // Field has RVA.

        public const ushort FD_NoReflection = 0x0800;       // field does not allow reflection

        public const ushort FD_HasAttributes = 0x8000;

        public TCLRString name;
        public TCLRSig sig;
        public TCLRSig defaultValue;
        public ushort flags;

        public override void Write(BinaryWriter writer)
        {
            name.Write(writer);
            sig.Write(writer);
            defaultValue.Write(writer);
            writer.Write(flags);
        }
    }
}
