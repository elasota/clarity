using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using Clarity.Rpa;

namespace AssemblyImporter.CppExport
{
    public class MidInstruction
    {
        public enum ArithEnum
        {
            Nothing = 0,

            Flags_Mask = 0x3,
            Flags_Ovf = 0x1,
            Flags_Un = 0x2,

            ArithType_Mask = 0x1c,
            ArithType_Int32 = 0x4,
            ArithType_Int64 = 0x8,
            ArithType_NativeInt = 0xc,
            ArithType_Float32 = 0x10,
            ArithType_Float64 = 0x14,
        }

        public enum OpcodeEnum
        {
            AllocObject,
            CallConstructor,
            CallMethod,
            KillReg,
            LivenReg,
            Return,
            ReturnValue,
            LoadReg_ManagedPtr,
            LoadReg_Value,
            StoreReg_ManagedPtr,
            StoreReg_Value,
            beq_ref,
            beq_val,
            bne_ref,
            bne_val,
            bge,
            bgt,
            ble,
            blt,
            clt,
            cgt,
            cne_ref,
            ceq_numeric,
            ceq_ref,
            LoadArgA_Value,
            CallVirtualMethod,
            brzero,
            brnotzero,
            brnull,
            brnotnull,
            LeakReg,
            EntryReg,
            Throw,
            NewSZArray,
            LoadField_ManagedPtr,
            LoadFieldA_ManagedPtr,
            LoadField_Object,
            LoadFieldA_Object,
            LoadField_Value,
            LoadRegA,
            LoadArrayElem,
            LoadArrayElemAddr,
            StoreField_ManagedPtr,
            StoreField_Object,
            add,
            sub,
            mul,
            div,
            rem,
            and,
            or,
            xor,
            shl,
            shr,
            neg,
            not,
            TryConvertObj,
            Leave,
            DuplicateReg,
            StoreStaticField,
            LoadIndirect,
            LoadStaticField,
            LoadStaticFieldAddr,
            Box,
            ConvertNumber,
            LoadArrayLength,
            LoadTypeInfoHandle,
            ConvertObj,
            StoreArrayElem,
            Switch,
            StoreIndirect,
            LoadFieldInfoHandle,
            UnboxPtr,
            UnboxValue,
            ZeroFillPtr,
            EnterProtectedBlock,
            ExitFinally,
            ConstrainedCallVirtualMethod,
            ConstrainedCallMethod,
            BindDelegate,
        }

        public OpcodeEnum Opcode { get; private set; }
        public CodeLocationTag CodeLocation { get; private set; }
        public CLRTypeSpec TypeSpecArg { get; private set; }
        public CLRTypeSpec TypeSpecArg2 { get; private set; }
        public SsaRegister RegArg { get; private set; }
        public SsaRegister RegArg2 { get; private set; }
        public SsaRegister RegArg3 { get; private set; }
        public SsaRegister[] RegArgs { get; private set; }
        public bool FlagArg { get; private set; }
        public uint UIntArg { get; private set; }
        public VReg VRegArg { get; private set; }
        // If you add more CFG edges here, update CfgBuilder.CreateSuccessionGraph
        public CfgOutboundEdge CfgEdgeArg { get; private set; }
        public CfgOutboundEdge CfgEdgeArg2 { get; private set; }
        public CfgOutboundEdge[] CfgEdgesArg { get; private set; }
        public string StrArg { get; private set; }
        public ArithEnum ArithArg { get; private set; }
        public CppMethodSpec MethodSpecArg { get; private set; }
        public ExceptionHandlingCluster EhClusterArg { get; private set; }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, bool flagArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            FlagArg = flagArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, uint uintArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            UIntArg = uintArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, ExceptionHandlingCluster ehClusterArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            EhClusterArg = ehClusterArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, CfgOutboundEdge cfgEdgeArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            CfgEdgeArg = cfgEdgeArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CfgOutboundEdge cfgEdgeArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            CfgEdgeArg = cfgEdgeArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CfgOutboundEdge[] cfgEdgesArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            CfgEdgesArg = cfgEdgesArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, string strArg, CLRTypeSpec typeSpecArg, CLRTypeSpec typeSpecArg2)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            StrArg = strArg;
            TypeSpecArg = typeSpecArg;
            TypeSpecArg2 = typeSpecArg2;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, string strArg, CLRTypeSpec typeSpecArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            StrArg = strArg;
            TypeSpecArg = typeSpecArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, uint uintArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            UIntArg = uintArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, VReg vRegArg, SsaRegister regArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            VRegArg = vRegArg;
            RegArg = regArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CLRTypeSpec typeSpec)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            TypeSpecArg = typeSpec;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CLRTypeSpec typeSpec, string strArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            TypeSpecArg = typeSpec;
            StrArg = strArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CLRTypeSpec typeSpec, string strArg, bool flagArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            TypeSpecArg = typeSpec;
            StrArg = strArg;
            FlagArg = flagArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CLRTypeSpec typeSpec, CLRTypeSpec typeSpec2, string strArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            TypeSpecArg = typeSpec;
            TypeSpecArg2 = typeSpec2;
            StrArg = strArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister[] regArgs)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArgs = regArgs;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, SsaRegister[] regArgs)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            RegArgs = regArgs;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, CppMethodSpec methodSpecArg, SsaRegister regArg, SsaRegister[] regArgs)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            MethodSpecArg = methodSpecArg;
            RegArg = regArg;
            RegArgs = regArgs;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, CppMethodSpec methodSpecArg, CLRTypeSpec typeSpecArg, SsaRegister regArg, SsaRegister regArg2, SsaRegister[] regArgs)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            TypeSpecArg = typeSpecArg;
            MethodSpecArg = methodSpecArg;
            RegArg = regArg;
            RegArg2 = regArg2;
            RegArgs = regArgs;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, CfgOutboundEdge cfgEdgeArg, bool flagArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            CfgEdgeArg = cfgEdgeArg;
            FlagArg = flagArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, SsaRegister regArg3, bool flagArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            RegArg3 = regArg3;
            FlagArg = FlagArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, SsaRegister regArg3, ArithEnum arithArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            RegArg3 = regArg3;
            ArithArg = arithArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, ArithEnum arithArg)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            ArithArg = arithArg;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, SsaRegister regArg2, SsaRegister regArg3)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            RegArg2 = regArg2;
            RegArg3 = regArg3;
        }

        public MidInstruction(OpcodeEnum opcode, CodeLocationTag codeLocation, SsaRegister regArg, CfgOutboundEdge cfgEdgeArg, CfgOutboundEdge cfgEdgeArg2)
        {
            Opcode = opcode;
            CodeLocation = codeLocation;
            RegArg = regArg;
            CfgEdgeArg = cfgEdgeArg;
            CfgEdgeArg2 = cfgEdgeArg2;
        }
    }
}
