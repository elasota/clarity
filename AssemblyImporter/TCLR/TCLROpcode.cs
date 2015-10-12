﻿namespace AssemblyImporter.TCLR
{
    public enum TCLROpcode
    {
        CEE_NOP,
        CEE_BREAK,
        CEE_LDARG_0,
        CEE_LDARG_1,
        CEE_LDARG_2,
        CEE_LDARG_3,
        CEE_LDLOC_0,
        CEE_LDLOC_1,
        CEE_LDLOC_2,
        CEE_LDLOC_3,
        CEE_STLOC_0,
        CEE_STLOC_1,
        CEE_STLOC_2,
        CEE_STLOC_3,
        CEE_LDARG_S,
        CEE_LDARGA_S,
        CEE_STARG_S,
        CEE_LDLOC_S,
        CEE_LDLOCA_S,
        CEE_STLOC_S,
        CEE_LDNULL,
        CEE_LDC_I4_M1,
        CEE_LDC_I4_0,
        CEE_LDC_I4_1,
        CEE_LDC_I4_2,
        CEE_LDC_I4_3,
        CEE_LDC_I4_4,
        CEE_LDC_I4_5,
        CEE_LDC_I4_6,
        CEE_LDC_I4_7,
        CEE_LDC_I4_8,
        CEE_LDC_I4_S,
        CEE_LDC_I4,
        CEE_LDC_I8,
        CEE_LDC_R4,
        CEE_LDC_R8,
        CEE_UNUSED49,
        CEE_DUP,
        CEE_POP,
        CEE_JMP,
        CEE_CALL,
        CEE_CALLI,
        CEE_RET,
        CEE_BR_S,
        CEE_BRFALSE_S,
        CEE_BRTRUE_S,
        CEE_BEQ_S,
        CEE_BGE_S,
        CEE_BGT_S,
        CEE_BLE_S,
        CEE_BLT_S,
        CEE_BNE_UN_S,
        CEE_BGE_UN_S,
        CEE_BGT_UN_S,
        CEE_BLE_UN_S,
        CEE_BLT_UN_S,
        CEE_BR,
        CEE_BRFALSE,
        CEE_BRTRUE,
        CEE_BEQ,
        CEE_BGE,
        CEE_BGT,
        CEE_BLE,
        CEE_BLT,
        CEE_BNE_UN,
        CEE_BGE_UN,
        CEE_BGT_UN,
        CEE_BLE_UN,
        CEE_BLT_UN,
        CEE_SWITCH,
        CEE_LDIND_I1,
        CEE_LDIND_U1,
        CEE_LDIND_I2,
        CEE_LDIND_U2,
        CEE_LDIND_I4,
        CEE_LDIND_U4,
        CEE_LDIND_I8,
        CEE_LDIND_I,
        CEE_LDIND_R4,
        CEE_LDIND_R8,
        CEE_LDIND_REF,
        CEE_STIND_REF,
        CEE_STIND_I1,
        CEE_STIND_I2,
        CEE_STIND_I4,
        CEE_STIND_I8,
        CEE_STIND_R4,
        CEE_STIND_R8,
        CEE_ADD,
        CEE_SUB,
        CEE_MUL,
        CEE_DIV,
        CEE_DIV_UN,
        CEE_REM,
        CEE_REM_UN,
        CEE_AND,
        CEE_OR,
        CEE_XOR,
        CEE_SHL,
        CEE_SHR,
        CEE_SHR_UN,
        CEE_NEG,
        CEE_NOT,
        CEE_CONV_I1,
        CEE_CONV_I2,
        CEE_CONV_I4,
        CEE_CONV_I8,
        CEE_CONV_R4,
        CEE_CONV_R8,
        CEE_CONV_U4,
        CEE_CONV_U8,
        CEE_CALLVIRT,
        CEE_CPOBJ,
        CEE_LDOBJ,
        CEE_LDSTR,
        CEE_NEWOBJ,
        CEE_CASTCLASS,
        CEE_ISINST,
        CEE_CONV_R_UN,
        CEE_UNUSED58,
        CEE_UNUSED1,
        CEE_UNBOX,
        CEE_THROW,
        CEE_LDFLD,
        CEE_LDFLDA,
        CEE_STFLD,
        CEE_LDSFLD,
        CEE_LDSFLDA,
        CEE_STSFLD,
        CEE_STOBJ,
        CEE_CONV_OVF_I1_UN,
        CEE_CONV_OVF_I2_UN,
        CEE_CONV_OVF_I4_UN,
        CEE_CONV_OVF_I8_UN,
        CEE_CONV_OVF_U1_UN,
        CEE_CONV_OVF_U2_UN,
        CEE_CONV_OVF_U4_UN,
        CEE_CONV_OVF_U8_UN,
        CEE_CONV_OVF_I_UN,
        CEE_CONV_OVF_U_UN,
        CEE_BOX,
        CEE_NEWARR,
        CEE_LDLEN,
        CEE_LDELEMA,
        CEE_LDELEM_I1,
        CEE_LDELEM_U1,
        CEE_LDELEM_I2,
        CEE_LDELEM_U2,
        CEE_LDELEM_I4,
        CEE_LDELEM_U4,
        CEE_LDELEM_I8,
        CEE_LDELEM_I,
        CEE_LDELEM_R4,
        CEE_LDELEM_R8,
        CEE_LDELEM_REF,
        CEE_STELEM_I,
        CEE_STELEM_I1,
        CEE_STELEM_I2,
        CEE_STELEM_I4,
        CEE_STELEM_I8,
        CEE_STELEM_R4,
        CEE_STELEM_R8,
        CEE_STELEM_REF,
        CEE_LDELEM,
        CEE_STELEM,
        CEE_UNBOX_ANY,
        CEE_UNUSED5,
        CEE_UNUSED6,
        CEE_UNUSED7,
        CEE_UNUSED8,
        CEE_UNUSED9,
        CEE_UNUSED10,
        CEE_UNUSED11,
        CEE_UNUSED12,
        CEE_UNUSED13,
        CEE_UNUSED14,
        CEE_UNUSED15,
        CEE_UNUSED16,
        CEE_UNUSED17,
        CEE_CONV_OVF_I1,
        CEE_CONV_OVF_U1,
        CEE_CONV_OVF_I2,
        CEE_CONV_OVF_U2,
        CEE_CONV_OVF_I4,
        CEE_CONV_OVF_U4,
        CEE_CONV_OVF_I8,
        CEE_CONV_OVF_U8,
        CEE_UNUSED50,
        CEE_UNUSED18,
        CEE_UNUSED19,
        CEE_UNUSED20,
        CEE_UNUSED21,
        CEE_UNUSED22,
        CEE_UNUSED23,
        CEE_REFANYVAL,
        CEE_CKFINITE,
        CEE_UNUSED24,
        CEE_UNUSED25,
        CEE_MKREFANY,
        CEE_UNUSED59,
        CEE_UNUSED60,
        CEE_UNUSED61,
        CEE_UNUSED62,
        CEE_UNUSED63,
        CEE_UNUSED64,
        CEE_UNUSED65,
        CEE_UNUSED66,
        CEE_UNUSED67,
        CEE_LDTOKEN,
        CEE_CONV_U2,
        CEE_CONV_U1,
        CEE_CONV_I,
        CEE_CONV_OVF_I,
        CEE_CONV_OVF_U,
        CEE_ADD_OVF,
        CEE_ADD_OVF_UN,
        CEE_MUL_OVF,
        CEE_MUL_OVF_UN,
        CEE_SUB_OVF,
        CEE_SUB_OVF_UN,
        CEE_ENDFINALLY,
        CEE_LEAVE,
        CEE_LEAVE_S,
        CEE_STIND_I,
        CEE_CONV_U,
        CEE_UNUSED26,
        CEE_UNUSED27,
        CEE_UNUSED28,
        CEE_UNUSED29,
        CEE_UNUSED30,
        CEE_UNUSED31,
        CEE_UNUSED32,
        CEE_UNUSED33,
        CEE_UNUSED34,
        CEE_UNUSED35,
        CEE_UNUSED36,
        CEE_UNUSED37,
        CEE_UNUSED38,
        CEE_UNUSED39,
        CEE_UNUSED40,
        CEE_UNUSED41,
        CEE_UNUSED42,
        CEE_UNUSED43,
        CEE_UNUSED44,
        CEE_UNUSED45,
        CEE_UNUSED46,
        CEE_UNUSED47,
        CEE_UNUSED48,
        CEE_PREFIX7,
        CEE_PREFIX6,
        CEE_PREFIX5,
        CEE_PREFIX4,
        CEE_PREFIX3,
        CEE_PREFIX2,
        CEE_PREFIX1,
        CEE_PREFIXREF,
        CEE_ARGLIST,
        CEE_CEQ,
        CEE_CGT,
        CEE_CGT_UN,
        CEE_CLT,
        CEE_CLT_UN,
        CEE_LDFTN,
        CEE_LDVIRTFTN,
        CEE_UNUSED56,
        CEE_LDARG,
        CEE_LDARGA,
        CEE_STARG,
        CEE_LDLOC,
        CEE_LDLOCA,
        CEE_STLOC,
        CEE_LOCALLOC,
        CEE_UNUSED57,
        CEE_ENDFILTER,
        CEE_UNALIGNED,
        CEE_VOLATILE,
        CEE_TAILCALL,
        CEE_INITOBJ,
        CEE_CONSTRAINED,
        CEE_CPBLK,
        CEE_INITBLK,
        CEE_UNUSED69,
        CEE_RETHROW,
        CEE_UNUSED51,
        CEE_SIZEOF,
        CEE_REFANYTYPE,
        CEE_READONLY,
        CEE_UNUSED53,
        CEE_UNUSED54,
        CEE_UNUSED55,
        CEE_UNUSED70,

        CEE_BRNULL = CEE_BRFALSE,
        CEE_BRNULL_S = CEE_BRFALSE_S,
        CEE_BRZERO = CEE_BRFALSE,
        CEE_BRZERO_S = CEE_BRFALSE_S,
        CEE_BRINST = CEE_BRTRUE,
        CEE_BRINST_S = CEE_BRTRUE_S,
        CEE_LDIND_U8 = CEE_LDIND_I8,
        CEE_LDELEM_U8 = CEE_LDELEM_I8,
        CEE_LDELEM_ANY = CEE_LDELEM,
        CEE_STELEM_ANY = CEE_STELEM,
        CEE_LDC_I4_M1x = CEE_LDC_I4_M1,
        CEE_ENDFAULT = CEE_ENDFINALLY,
    }
}
