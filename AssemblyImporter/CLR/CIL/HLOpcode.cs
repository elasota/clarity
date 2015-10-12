using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR.CIL
{
    public enum ArgEncoding
    {
        None,
        No,  // no. - III.2.2
        U8,
        U16,
        U32,
        U64,
        I8,
        I16,
        I32,
        I64,
        F32,
        F64,
        MethodDefOrRefOrSpec,
        MethodDefOrRef,
        Switch,     // III.3.66
        TypeDefOrRefOrSpec,
        Field,
        String,
        LoadToken,  // III.4.17
    }

    public enum HLOpFlags : byte
    {
        None = 0,
        Ovf = 0x1,
        Un = 0x2,
        Br = 0x4,
    }

    public enum HLOpType : byte
    {
        None = 0,
        I1,
        I2,
        I4,
        I8,
        U1,
        U2,
        U4,
        U8,
        R4,
        R8,
        I,
        U,
        R,
        Ref,
    }

    public enum HLOpcode : byte
    {
        nop,
        ldarg,
        ldarga,
        ldloc,
        stloc,
        ldnull,
        ldc,
        dup,
        pop,
        jmp,
        call,
        calli,
        ret,
        br,
        brfalse,
        brtrue,
        beq,    // [.un]
        bne,    // [.un]
        bge,    // [.un]
        bgt,    // [.un]
        ble,    // [.un]
        blt,    // [.un]
        @break,
        @switch,
        ldind,  // (.type)
        stind,  // (.type)
        add,    // [.ovf][.un]
        sub,    // [.ovf][.un]
        mul,    // [.ovf][.un]
        div,    // [.ovf][.un]
        rem,    // [.ovf][.un]
        and,
        or,
        xor,
        shl,
        shr,    // [.un]
        neg,
        not,
        conv,   // [.ovf](.type)[.un]
        callvirt,
        cpobj,
        ldobj,
        ldstr,
        newobj,
        castclass,
        isinst,
        unbox,
        unbox_any,
        @throw,
        ldfld,
        ldflda,
        stfld,
        ldsfld,
        ldsflda,
        stsfld,
        stobj,
        box,
        newarr,
        ldlen,
        ldelema,
        ldelem,     // [.type]
        stelem,     // [.type]
        refanyval,
        ckfinite,
        mkrefany,
        ldtoken,
        endfinally,
        leave,
        arglist,
        ceq,
        cgt,    // [.un]
        clt,    // [.un]
        ldftn,
        ldvirtftn,
        starg,
        ldloca,
        localloc,
        endfilter,
        unaligned_pfx,
        volatile_pfx,
        tail_pfx,
        initobj,
        constrained_pfx,
        cpblk,
        initblk,
        no_pfx,
        rethrow,
        @sizeof,
        refanytype,
        readonly_pfx,
    }
}
