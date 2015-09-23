using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRMethodDef : TCLRMetaItem
    {
        public const uint MD_Scope_Mask = 0x00000007;
        public const uint MD_Scope_PrivateScope = 0x00000000;     // Member not referenceable.
        public const uint MD_Scope_Private = 0x00000001;     // Accessible only by the parent type.
        public const uint MD_Scope_FamANDAssem = 0x00000002;     // Accessible by sub-types only in this Assembly.
        public const uint MD_Scope_Assem = 0x00000003;     // Accessibly by anyone in the Assembly.
        public const uint MD_Scope_Family = 0x00000004;     // Accessible only by type and sub-types.
        public const uint MD_Scope_FamORAssem = 0x00000005;     // Accessibly by sub-types anywhere, plus anyone in assembly.
        public const uint MD_Scope_Public = 0x00000006;     // Accessibly by anyone who has visibility to this scope.

        public const uint MD_Static = 0x00000010;     // Defined on type, else per instance.
        public const uint MD_Final = 0x00000020;     // Method may not be overridden.
        public const uint MD_Virtual = 0x00000040;     // Method virtual.
        public const uint MD_HideBySig = 0x00000080;     // Method hides by name+sig, else just by name.

        public const uint MD_VtableLayoutMask = 0x00000100;
        public const uint MD_ReuseSlot = 0x00000000;     // The default.
        public const uint MD_NewSlot = 0x00000100;     // Method always gets a new slot in the vtable.
        public const uint MD_Abstract = 0x00000200;     // Method does not provide an implementation.
        public const uint MD_SpecialName = 0x00000400;     // Method is special.  Name describes how.
        public const uint MD_NativeProfiled = 0x00000800;

        public const uint MD_Constructor = 0x00001000;
        public const uint MD_StaticConstructor = 0x00002000;
        public const uint MD_Finalizer = 0x00004000;

        public const uint MD_DelegateConstructor = 0x00010000;
        public const uint MD_DelegateInvoke = 0x00020000;
        public const uint MD_DelegateBeginInvoke = 0x00040000;
        public const uint MD_DelegateEndInvoke = 0x00080000;

        public const uint MD_Synchronized = 0x01000000;
        public const uint MD_GloballySynchronized = 0x02000000;
        public const uint MD_Patched = 0x04000000;
        public const uint MD_EntryPoint = 0x08000000;
        public const uint MD_RequireSecObject = 0x10000000;     // Method calls another method containing security code.
        public const uint MD_HasSecurity = 0x20000000;     // Method has security associate with it.
        public const uint MD_HasExceptionHandlers = 0x40000000;
        public const uint MD_HasAttributes = 0x80000000;

        public TCLRString name;
        public TCLROffset RVA;

        public uint flags;

        public byte retVal;
        public byte numArgs;
        public byte numLocals;
        public byte lengthEvalStack;

        public TCLRSig locals;
        public TCLRSig sig;

        public override void Write(BinaryWriter writer)
        {
            name.Write(writer);
            RVA.Write(writer);

            writer.Write(flags);

            writer.Write(retVal);
            writer.Write(numArgs);
            writer.Write(numLocals);
            writer.Write(lengthEvalStack);

            locals.Write(writer);
            sig.Write(writer);
        }
    }
}
