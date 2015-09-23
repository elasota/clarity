using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public class TCLRTypeDef : TCLRMetaItem
    {
        public const ushort TD_Scope_Mask               = 0x0007;
        public const ushort TD_Scope_NotPublic          = 0x0000; // Class is not public scope.
        public const ushort TD_Scope_Public             = 0x0001; // Class is public scope.
        public const ushort TD_Scope_NestedPublic       = 0x0002; // Class is nested with public visibility.
        public const ushort TD_Scope_NestedPrivate      = 0x0003; // Class is nested with private visibility.
        public const ushort TD_Scope_NestedFamily       = 0x0004; // Class is nested with family visibility.
        public const ushort TD_Scope_NestedAssembly     = 0x0005; // Class is nested with assembly visibility.
        public const ushort TD_Scope_NestedFamANDAssem  = 0x0006; // Class is nested with family and assembly visibility.
        public const ushort TD_Scope_NestedFamORAssem   = 0x0007; // Class is nested with family or assembly visibility.

        public const ushort TD_Serializable = 0x0008;

        public const ushort TD_Semantics_Mask = 0x0030;
        public const ushort TD_Semantics_Class = 0x0000;
        public const ushort TD_Semantics_ValueType = 0x0010;
        public const ushort TD_Semantics_Interface = 0x0020;
        public const ushort TD_Semantics_Enum = 0x0030;

        public const ushort TD_Abstract = 0x0040;
        public const ushort TD_Sealed = 0x0080;

        public const ushort TD_SpecialName = 0x0100;
        public const ushort TD_Delegate = 0x0200;
        public const ushort TD_MulticastDelegate = 0x0400;

        public const ushort TD_Patched = 0x0800;

        public const ushort TD_BeforeFieldInit = 0x1000;
        public const ushort TD_HasSecurity = 0x2000;
        public const ushort TD_HasFinalizer = 0x4000;
        public const ushort TD_HasAttributes = 0x8000;

        public TCLRString name;
        public TCLRString nameSpace;
        public TCLRIndex extends;          // (TBL_TypeDef/TBL_TypeRef --> 0x8000)
        public TCLRIndex enclosingType;    // TBL_TypeDef

        public TCLRSig interfaces;
        public TCLRIndex methods_First;    // TBL_MethodDef

        public byte vMethods_Num;
        public byte iMethods_Num;
        public byte sMethods_Num;
        public byte dataType;

        public TCLRIndex sFields_First;    // TBL_FieldDef
        public TCLRIndex iFields_First;    // TBL_FieldDef

        public byte sFields_Num;
        public byte iFields_Num;
        public ushort flags;

        public override void Write(BinaryWriter writer)
        {
            name.Write(writer);
            nameSpace.Write(writer);
            extends.Write(writer);
            enclosingType.Write(writer);

            interfaces.Write(writer);
            methods_First.Write(writer);

            writer.Write(vMethods_Num);
            writer.Write(iMethods_Num);
            writer.Write(sMethods_Num);
            writer.Write(dataType);

            sFields_First.Write(writer);
            iFields_First.Write(writer);

            writer.Write(sFields_Num);
            writer.Write(iFields_Num);

            writer.Write(flags);
        }
    }
}
