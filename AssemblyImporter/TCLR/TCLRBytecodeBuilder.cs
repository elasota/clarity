using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;
using AssemblyImporter.CLR.CIL;

namespace AssemblyImporter.TCLR
{
    public class TCLRBytecodeBuilder : IDisposable
    {

        private struct ConvTag : IEquatable<ConvTag>
        {
            private bool m_isUn;
            private bool m_isOvf;
            private HLOpType m_type;

            public ConvTag(bool isOvf, HLOpType type, bool isUn)
            {
                m_isUn = isUn;
                m_isOvf = isOvf;
                m_type = type;
            }

            public override int GetHashCode()
            {
                return m_isUn.GetHashCode() + m_isOvf.GetHashCode() + m_type.GetHashCode();
            }

            bool IEquatable<ConvTag>.Equals(ConvTag other)
            {
                return m_isUn == other.m_isUn && m_isOvf == other.m_isOvf && m_type == other.m_type;
            }

            public override bool Equals(object other)
            {
                return other != null && other.GetType() == typeof(ConvTag) && this.Equals((ConvTag)other);
            }
        }

        private struct ArithTag : IEquatable<ArithTag>
        {
            private HLOpcode m_opcode;
            private bool m_isUn;
            private bool m_isOvf;

            public ArithTag(HLOpcode opcode, bool isUn, bool isOvf)
            {
                m_opcode = opcode;
                m_isUn = isUn;
                m_isOvf = isOvf;
            }

            public override int GetHashCode()
            {
                return m_opcode.GetHashCode() + m_isUn.GetHashCode();
            }

            public bool Equals(ArithTag other)
            {
                return m_opcode == other.m_opcode && m_isUn == other.m_isUn && m_isOvf == other.m_isOvf;
            }

            public override bool Equals(object other)
            {
                return other != null && other.GetType() == typeof(ArithTag) && this.Equals((ArithTag)other);
            }
        }

        private struct BranchTag : IEquatable<BranchTag>
        {
            private HLOpcode m_opcode;
            private bool m_isUn;
            private bool m_isShort;

            public BranchTag(HLOpcode opcode, bool isUn, bool isShort)
            {
                m_opcode = opcode;
                m_isUn = isUn;
                m_isShort = isShort;
            }

            public override int GetHashCode()
            {
                return m_opcode.GetHashCode() + m_isUn.GetHashCode() + m_isShort.GetHashCode();
            }

            bool IEquatable<BranchTag>.Equals(BranchTag other)
            {
                return m_opcode == other.m_opcode && m_isUn == other.m_isUn && m_isShort == other.m_isShort;
            }

            public override bool Equals(object other)
            {
                return other != null && other.GetType() == typeof(BranchTag) && this.Equals((BranchTag)other);
            }
        }

        private CLRMethodDefRow m_methodDef;
        private bool m_bigEndian;
        private MemoryStream m_memoryStream;
        private BinaryWriter m_writer;
        private HashSet<uint> m_longFormJumps;
        private HLInstruction[] m_instructions;
        private uint[] m_instructionLocations;
        private Dictionary<HLOpcode, TCLROpcode> m_simpleOpConversions;
        private Dictionary<BranchTag, TCLROpcode> m_branchConversions;
        private Dictionary<ArithTag, TCLROpcode> m_arithConversions;
        private Dictionary<ConvTag, TCLROpcode> m_convConversions;
        private bool m_anyJumpsInvalid;
        private bool m_switchInvalid;
        private TCLRAssemblyBuilder m_assemblyBuilder;

        private TCLRBytecodeBuilder(CLRMethodDefRow methodDef, TCLRAssemblyBuilder assemblyBuilder, bool bigEndian)
        {
            m_methodDef = methodDef;
            m_bigEndian = bigEndian;
            m_longFormJumps = new HashSet<uint>();
            m_instructions = methodDef.Method.Instructions;
            m_instructionLocations = new uint[m_instructions.Length];
            m_assemblyBuilder = assemblyBuilder;

            m_simpleOpConversions = new Dictionary<HLOpcode, TCLROpcode>();
            m_simpleOpConversions.Add(HLOpcode.nop, TCLROpcode.CEE_NOP);
            m_simpleOpConversions.Add(HLOpcode.ldnull, TCLROpcode.CEE_LDNULL);
            m_simpleOpConversions.Add(HLOpcode.dup, TCLROpcode.CEE_DUP);
            m_simpleOpConversions.Add(HLOpcode.pop, TCLROpcode.CEE_POP);
            m_simpleOpConversions.Add(HLOpcode.ret, TCLROpcode.CEE_RET);
            m_simpleOpConversions.Add(HLOpcode.@break, TCLROpcode.CEE_BREAK);
            m_simpleOpConversions.Add(HLOpcode.neg, TCLROpcode.CEE_NEG);
            m_simpleOpConversions.Add(HLOpcode.not, TCLROpcode.CEE_NOT);
            m_simpleOpConversions.Add(HLOpcode.endfinally, TCLROpcode.CEE_ENDFINALLY);
            m_simpleOpConversions.Add(HLOpcode.endfilter, TCLROpcode.CEE_ENDFILTER);
            m_simpleOpConversions.Add(HLOpcode.unaligned_pfx, TCLROpcode.CEE_UNALIGNED);
            m_simpleOpConversions.Add(HLOpcode.volatile_pfx, TCLROpcode.CEE_VOLATILE);
            m_simpleOpConversions.Add(HLOpcode.constrained_pfx, TCLROpcode.CEE_CONSTRAINED);
            m_simpleOpConversions.Add(HLOpcode.tail_pfx, TCLROpcode.CEE_TAILCALL);
            m_simpleOpConversions.Add(HLOpcode.rethrow, TCLROpcode.CEE_CONSTRAINED);
            m_simpleOpConversions.Add(HLOpcode.and, TCLROpcode.CEE_AND);
            m_simpleOpConversions.Add(HLOpcode.or, TCLROpcode.CEE_OR);
            m_simpleOpConversions.Add(HLOpcode.xor, TCLROpcode.CEE_XOR);
            m_simpleOpConversions.Add(HLOpcode.shl, TCLROpcode.CEE_SHL);

            m_branchConversions = new Dictionary<BranchTag, TCLROpcode>();
            m_branchConversions.Add(new BranchTag(HLOpcode.br, false, false), TCLROpcode.CEE_BR);
            m_branchConversions.Add(new BranchTag(HLOpcode.br, false, true), TCLROpcode.CEE_BR_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.brtrue, false, false), TCLROpcode.CEE_BRTRUE);
            m_branchConversions.Add(new BranchTag(HLOpcode.brtrue, false, true), TCLROpcode.CEE_BRTRUE_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.brfalse, false, false), TCLROpcode.CEE_BRFALSE);
            m_branchConversions.Add(new BranchTag(HLOpcode.brfalse, false, true), TCLROpcode.CEE_BRFALSE_S);

            m_branchConversions.Add(new BranchTag(HLOpcode.beq, false, false), TCLROpcode.CEE_BEQ);
            //m_branchConversions.Add(new BranchTag(HLOpcode.bne, false, false), TCLROpcode.CEE_BNE);   // Does not exist
            m_branchConversions.Add(new BranchTag(HLOpcode.bgt, false, false), TCLROpcode.CEE_BGT);
            m_branchConversions.Add(new BranchTag(HLOpcode.bge, false, false), TCLROpcode.CEE_BGE);
            m_branchConversions.Add(new BranchTag(HLOpcode.blt, false, false), TCLROpcode.CEE_BLT);
            m_branchConversions.Add(new BranchTag(HLOpcode.ble, false, false), TCLROpcode.CEE_BLE);

            m_branchConversions.Add(new BranchTag(HLOpcode.beq, false, true), TCLROpcode.CEE_BEQ_S);
            //m_branchConversions.Add(new BranchTag(HLOpcode.bne, false, true), TCLROpcode.CEE_BNE_S);   // Does not exist
            m_branchConversions.Add(new BranchTag(HLOpcode.bgt, false, true), TCLROpcode.CEE_BGT_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.bge, false, true), TCLROpcode.CEE_BGE_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.blt, false, true), TCLROpcode.CEE_BLT_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.ble, false, true), TCLROpcode.CEE_BLE_S);

            //m_branchConversions.Add(new BranchTag(HLOpcode.beq, true, false), TCLROpcode.CEE_BEQ_UN);   // Does not exist
            m_branchConversions.Add(new BranchTag(HLOpcode.bne, true, false), TCLROpcode.CEE_BNE_UN);
            m_branchConversions.Add(new BranchTag(HLOpcode.bgt, true, false), TCLROpcode.CEE_BGT_UN);
            m_branchConversions.Add(new BranchTag(HLOpcode.bge, true, false), TCLROpcode.CEE_BGE_UN);
            m_branchConversions.Add(new BranchTag(HLOpcode.blt, true, false), TCLROpcode.CEE_BLT_UN);
            m_branchConversions.Add(new BranchTag(HLOpcode.ble, true, false), TCLROpcode.CEE_BLE_UN);

            //m_branchConversions.Add(new BranchTag(HLOpcode.beq, true, true), TCLROpcode.CEE_BEQ_UN_S);   // Does not exist
            m_branchConversions.Add(new BranchTag(HLOpcode.bne, true, true), TCLROpcode.CEE_BNE_UN_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.bgt, true, true), TCLROpcode.CEE_BGT_UN_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.bge, true, true), TCLROpcode.CEE_BGE_UN_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.blt, true, true), TCLROpcode.CEE_BLT_UN_S);
            m_branchConversions.Add(new BranchTag(HLOpcode.ble, true, true), TCLROpcode.CEE_BLE_UN_S);

            m_branchConversions.Add(new BranchTag(HLOpcode.leave, false, false), TCLROpcode.CEE_LEAVE);
            m_branchConversions.Add(new BranchTag(HLOpcode.leave, false, true), TCLROpcode.CEE_LEAVE_S);

            m_arithConversions = new Dictionary<ArithTag, TCLROpcode>();
            m_arithConversions.Add(new ArithTag(HLOpcode.add, false, false), TCLROpcode.CEE_ADD);
            m_arithConversions.Add(new ArithTag(HLOpcode.add, false, true), TCLROpcode.CEE_ADD_OVF);
            m_arithConversions.Add(new ArithTag(HLOpcode.add, true, true), TCLROpcode.CEE_ADD_OVF_UN);
            m_arithConversions.Add(new ArithTag(HLOpcode.sub, false, false), TCLROpcode.CEE_SUB);
            m_arithConversions.Add(new ArithTag(HLOpcode.sub, false, true), TCLROpcode.CEE_SUB_OVF);
            m_arithConversions.Add(new ArithTag(HLOpcode.sub, true, true), TCLROpcode.CEE_SUB_OVF_UN);
            m_arithConversions.Add(new ArithTag(HLOpcode.mul, false, false), TCLROpcode.CEE_MUL);
            m_arithConversions.Add(new ArithTag(HLOpcode.mul, false, true), TCLROpcode.CEE_MUL_OVF);
            m_arithConversions.Add(new ArithTag(HLOpcode.mul, true, true), TCLROpcode.CEE_MUL_OVF_UN);
            m_arithConversions.Add(new ArithTag(HLOpcode.div, false, false), TCLROpcode.CEE_DIV);
            m_arithConversions.Add(new ArithTag(HLOpcode.div, true, false), TCLROpcode.CEE_DIV_UN);
            m_arithConversions.Add(new ArithTag(HLOpcode.rem, false, false), TCLROpcode.CEE_REM);
            m_arithConversions.Add(new ArithTag(HLOpcode.rem, true, false), TCLROpcode.CEE_REM_UN);
            m_arithConversions.Add(new ArithTag(HLOpcode.shr, false, false), TCLROpcode.CEE_SHR);
            m_arithConversions.Add(new ArithTag(HLOpcode.shr, true, false), TCLROpcode.CEE_SHR_UN);

            m_convConversions = new Dictionary<ConvTag,TCLROpcode>();
            m_convConversions.Add(new ConvTag(true, HLOpType.I1, true), TCLROpcode.CEE_CONV_OVF_I1_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.I2, true), TCLROpcode.CEE_CONV_OVF_I2_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.I4, true), TCLROpcode.CEE_CONV_OVF_I4_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.I8, true), TCLROpcode.CEE_CONV_OVF_I8_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.U1, true), TCLROpcode.CEE_CONV_OVF_U1_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.U2, true), TCLROpcode.CEE_CONV_OVF_U2_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.U4, true), TCLROpcode.CEE_CONV_OVF_U4_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.U8, true), TCLROpcode.CEE_CONV_OVF_U8_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.I, true), TCLROpcode.CEE_CONV_OVF_I_UN);
            m_convConversions.Add(new ConvTag(true, HLOpType.U, true), TCLROpcode.CEE_CONV_OVF_U_UN);

            m_convConversions.Add(new ConvTag(true, HLOpType.I1, false), TCLROpcode.CEE_CONV_OVF_I1);
            m_convConversions.Add(new ConvTag(true, HLOpType.I2, false), TCLROpcode.CEE_CONV_OVF_I2);
            m_convConversions.Add(new ConvTag(true, HLOpType.I4, false), TCLROpcode.CEE_CONV_OVF_I4);
            m_convConversions.Add(new ConvTag(true, HLOpType.I8, false), TCLROpcode.CEE_CONV_OVF_I8);
            m_convConversions.Add(new ConvTag(true, HLOpType.U1, false), TCLROpcode.CEE_CONV_OVF_U1);
            m_convConversions.Add(new ConvTag(true, HLOpType.U2, false), TCLROpcode.CEE_CONV_OVF_U2);
            m_convConversions.Add(new ConvTag(true, HLOpType.U4, false), TCLROpcode.CEE_CONV_OVF_U4);
            m_convConversions.Add(new ConvTag(true, HLOpType.U8, false), TCLROpcode.CEE_CONV_OVF_U8);
            m_convConversions.Add(new ConvTag(true, HLOpType.I, false), TCLROpcode.CEE_CONV_OVF_I);
            m_convConversions.Add(new ConvTag(true, HLOpType.U, false), TCLROpcode.CEE_CONV_OVF_U);
            
            m_convConversions.Add(new ConvTag(false, HLOpType.U1, false), TCLROpcode.CEE_CONV_U1);
            m_convConversions.Add(new ConvTag(false, HLOpType.U2, false), TCLROpcode.CEE_CONV_U2);
            m_convConversions.Add(new ConvTag(false, HLOpType.U4, false), TCLROpcode.CEE_CONV_U4);
            m_convConversions.Add(new ConvTag(false, HLOpType.U8, false), TCLROpcode.CEE_CONV_U8);
            m_convConversions.Add(new ConvTag(false, HLOpType.I1, false), TCLROpcode.CEE_CONV_I1);
            m_convConversions.Add(new ConvTag(false, HLOpType.I2, false), TCLROpcode.CEE_CONV_I2);
            m_convConversions.Add(new ConvTag(false, HLOpType.I4, false), TCLROpcode.CEE_CONV_I4);
            m_convConversions.Add(new ConvTag(false, HLOpType.I8, false), TCLROpcode.CEE_CONV_I8);
            m_convConversions.Add(new ConvTag(false, HLOpType.R4, false), TCLROpcode.CEE_CONV_R4);
            m_convConversions.Add(new ConvTag(false, HLOpType.R8, false), TCLROpcode.CEE_CONV_R8);
            m_convConversions.Add(new ConvTag(false, HLOpType.I, false), TCLROpcode.CEE_CONV_I);
            m_convConversions.Add(new ConvTag(false, HLOpType.U, false), TCLROpcode.CEE_CONV_U);

            m_convConversions.Add(new ConvTag(false, HLOpType.R, true), TCLROpcode.CEE_CONV_R_UN);

            m_simpleOpConversions.Add(HLOpcode.cpobj, TCLROpcode.CEE_CPOBJ);
            m_simpleOpConversions.Add(HLOpcode.ldobj, TCLROpcode.CEE_LDOBJ);
            m_simpleOpConversions.Add(HLOpcode.castclass, TCLROpcode.CEE_CASTCLASS);
            m_simpleOpConversions.Add(HLOpcode.call, TCLROpcode.CEE_CALL);
            m_simpleOpConversions.Add(HLOpcode.callvirt, TCLROpcode.CEE_CALLVIRT);
            m_simpleOpConversions.Add(HLOpcode.newobj, TCLROpcode.CEE_NEWOBJ);
            m_simpleOpConversions.Add(HLOpcode.isinst, TCLROpcode.CEE_ISINST);
            m_simpleOpConversions.Add(HLOpcode.unbox, TCLROpcode.CEE_UNBOX);
            m_simpleOpConversions.Add(HLOpcode.unbox_any, TCLROpcode.CEE_UNBOX_ANY);
            m_simpleOpConversions.Add(HLOpcode.stobj, TCLROpcode.CEE_STOBJ);
            m_simpleOpConversions.Add(HLOpcode.box, TCLROpcode.CEE_BOX);
            m_simpleOpConversions.Add(HLOpcode.newarr, TCLROpcode.CEE_NEWARR);
            m_simpleOpConversions.Add(HLOpcode.ldelema, TCLROpcode.CEE_LDELEMA);
            m_simpleOpConversions.Add(HLOpcode.initobj, TCLROpcode.CEE_INITOBJ);
            m_simpleOpConversions.Add(HLOpcode.@sizeof, TCLROpcode.CEE_SIZEOF);
            m_simpleOpConversions.Add(HLOpcode.ldftn, TCLROpcode.CEE_LDFTN);
            m_simpleOpConversions.Add(HLOpcode.ldvirtftn, TCLROpcode.CEE_LDVIRTFTN);
			m_simpleOpConversions.Add(HLOpcode.ldfld, TCLROpcode.CEE_LDFLD);
            m_simpleOpConversions.Add(HLOpcode.ldflda, TCLROpcode.CEE_LDFLDA);
            m_simpleOpConversions.Add(HLOpcode.stfld, TCLROpcode.CEE_STFLD);
            m_simpleOpConversions.Add(HLOpcode.ldsfld, TCLROpcode.CEE_LDSFLD);
            m_simpleOpConversions.Add(HLOpcode.ldsflda, TCLROpcode.CEE_LDSFLDA);
            m_simpleOpConversions.Add(HLOpcode.stsfld, TCLROpcode.CEE_STSFLD);
            m_simpleOpConversions.Add(HLOpcode.@throw, TCLROpcode.CEE_THROW);
            m_simpleOpConversions.Add(HLOpcode.ldlen, TCLROpcode.CEE_LDLEN);
            m_simpleOpConversions.Add(HLOpcode.ceq, TCLROpcode.CEE_CEQ);
        }


        private void WriteOpcode(TCLROpcode op)
        {
            int intOp = (int)op;
            if (intOp > 256)
            {
                m_writer.Write((byte)TCLROpcode.CEE_PREFIX1);
                intOp -= 256;
            }
            m_writer.Write((byte)intOp);
        }

        private void EmitBytecode(bool onlyOffsets)
        {
            if (m_writer != null)
                ((IDisposable)m_writer).Dispose();
            if (m_memoryStream != null)
                m_memoryStream.Dispose();
            m_memoryStream = new MemoryStream();
            if (m_bigEndian)
                m_writer = new BigEndianBinaryWriter(m_memoryStream);
            else
                m_writer = new BinaryWriter(m_memoryStream);

            uint instrNum = 0;
            uint nextInstr = 0;
            foreach (HLInstruction instr in m_instructions)
            {
                instrNum = nextInstr;
                nextInstr++;

                m_instructionLocations[instrNum] = (uint)m_memoryStream.Position;

                switch (instr.Opcode)
                {
                    case HLOpcode.ldarg:
                        {
                            uint arg = instr.Arguments.U32Value;
                            if (arg == 0)
                                WriteOpcode(TCLROpcode.CEE_LDARG_0);
                            else if (arg == 1)
                                WriteOpcode(TCLROpcode.CEE_LDARG_1);
                            else if (arg == 2)
                                WriteOpcode(TCLROpcode.CEE_LDARG_2);
                            else if (arg == 3)
                                WriteOpcode(TCLROpcode.CEE_LDARG_3);
                            else if (arg <= 255)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDARG_S);
                                m_writer.Write((byte)arg);
                            }
                            else if (arg <= 65535)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDARG);
                                m_writer.Write((ushort)arg);
                            }
                            else
                                throw new OverflowException("Too many args");
                        }
                        break;
                    case HLOpcode.ldarga:
                        {
                            uint arg = instr.Arguments.U32Value;
                            if (arg <= 255)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDARGA_S);
                                m_writer.Write((byte)arg);
                            }
                            else if (arg <= 65535)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDARGA);
                                m_writer.Write((ushort)arg);
                            }
                            else
                                throw new OverflowException("Too many args");
                        }
                        break;
                    case HLOpcode.ldloc:
                        {
                            uint arg = instr.Arguments.U32Value;
                            if (arg == 0)
                                WriteOpcode(TCLROpcode.CEE_LDLOC_0);
                            else if (arg == 1)
                                WriteOpcode(TCLROpcode.CEE_LDLOC_1);
                            else if (arg == 2)
                                WriteOpcode(TCLROpcode.CEE_LDLOC_2);
                            else if (arg == 3)
                                WriteOpcode(TCLROpcode.CEE_LDLOC_3);
                            else if (arg <= 255)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDLOC_S);
                                m_writer.Write((byte)arg);
                            }
                            else if (arg <= 65535)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDLOC);
                                m_writer.Write((ushort)arg);
                            }
                            else
                                throw new OverflowException("Too many locals");
                        }
                        break;
                    case HLOpcode.stloc:
                        {
                            uint arg = instr.Arguments.U32Value;
                            if (arg == 0)
                                WriteOpcode(TCLROpcode.CEE_STLOC_0);
                            else if (arg == 1)
                                WriteOpcode(TCLROpcode.CEE_STLOC_1);
                            else if (arg == 2)
                                WriteOpcode(TCLROpcode.CEE_STLOC_2);
                            else if (arg == 3)
                                WriteOpcode(TCLROpcode.CEE_STLOC_3);
                            else if (arg <= 255)
                            {
                                WriteOpcode(TCLROpcode.CEE_STLOC_S);
                                m_writer.Write((byte)arg);
                            }
                            else if (arg <= 65535)
                            {
                                WriteOpcode(TCLROpcode.CEE_STLOC);
                                m_writer.Write((ushort)arg);
                            }
                            else
                                throw new OverflowException("Too many locals");
                        }
                        break;
                    case HLOpcode.ldc:
                        if (instr.Arguments.ArgsType == HLArguments.ArgsTypeEnum.U32 || instr.Arguments.ArgsType == HLArguments.ArgsTypeEnum.I32)
                        {
                            int arg = instr.Arguments.S32Value;
                            if (arg == -1)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_M1);
                            else if (arg == 0)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_0);
                            else if (arg == 1)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_1);
                            else if (arg == 2)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_2);
                            else if (arg == 3)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_3);
                            else if (arg == 4)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_4);
                            else if (arg == 5)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_5);
                            else if (arg == 6)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_6);
                            else if (arg == 7)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_7);
                            else if (arg == 8)
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_8);
                            else if (arg >= -128 && arg <= 127)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDC_I4_S);
                                m_writer.Write((sbyte)arg);
                            }
                            else
                            {
                                WriteOpcode(TCLROpcode.CEE_LDC_I4);
                                m_writer.Write(arg);
                            }
                        }
                        else if (instr.Arguments.ArgsType == HLArguments.ArgsTypeEnum.I64 || instr.Arguments.ArgsType == HLArguments.ArgsTypeEnum.U64)
                        {
                            WriteOpcode(TCLROpcode.CEE_LDC_I8);
                            m_writer.Write(instr.Arguments.S64Value);
                        }
                        else if (instr.Arguments.ArgsType == HLArguments.ArgsTypeEnum.F32)
                        {
                            WriteOpcode(TCLROpcode.CEE_LDC_R4);
                            m_writer.Write(instr.Arguments.F32Value);
                        }
                        else if (instr.Arguments.ArgsType == HLArguments.ArgsTypeEnum.F64)
                        {
                            WriteOpcode(TCLROpcode.CEE_LDC_R8);
                            m_writer.Write(instr.Arguments.F64Value);
                        }
                        break;
                    case HLOpcode.@switch:
                        {
                            WriteOpcode(TCLROpcode.CEE_SWITCH);
                            uint[] switchInstrs = (uint[])instr.Arguments.ObjValue;

                            uint ip = m_instructionLocations[instrNum + 1];
                            foreach (uint targetInstr in switchInstrs)
                            {
                                int offset = (int)m_instructionLocations[targetInstr] - (int)ip;
                                if (offset < -32768 || offset > 32767)
                                {
                                    m_switchInvalid = true;
                                    offset = 0;
                                }
                                m_writer.Write((short)offset);
                            }
                        }
                        break;
                    case HLOpcode.ldind:
                        if (instr.TypeToken == HLOpType.I1)
                            WriteOpcode(TCLROpcode.CEE_LDIND_I1);
                        else if (instr.TypeToken == HLOpType.I2)
                            WriteOpcode(TCLROpcode.CEE_LDIND_I2);
                        else if (instr.TypeToken == HLOpType.I4)
                            WriteOpcode(TCLROpcode.CEE_LDIND_I4);
                        else if (instr.TypeToken == HLOpType.I8)
                            WriteOpcode(TCLROpcode.CEE_LDIND_I8);
                        else if (instr.TypeToken == HLOpType.U1)
                            WriteOpcode(TCLROpcode.CEE_LDIND_U1);
                        else if (instr.TypeToken == HLOpType.U2)
                            WriteOpcode(TCLROpcode.CEE_LDIND_U2);
                        else if (instr.TypeToken == HLOpType.U4)
                            WriteOpcode(TCLROpcode.CEE_LDIND_U4);
                        else if (instr.TypeToken == HLOpType.U8)
                            WriteOpcode(TCLROpcode.CEE_LDIND_U8);
                        else if (instr.TypeToken == HLOpType.R4)
                            WriteOpcode(TCLROpcode.CEE_LDIND_R4);
                        else if (instr.TypeToken == HLOpType.R8)
                            WriteOpcode(TCLROpcode.CEE_LDIND_R8);
                        else if (instr.TypeToken == HLOpType.I)
                            WriteOpcode(TCLROpcode.CEE_LDIND_I);
                        else if (instr.TypeToken == HLOpType.Ref)
                            WriteOpcode(TCLROpcode.CEE_LDIND_REF);
                        else
                            throw new ParseFailedException("Unknown ldind type token");
                        break;
                    case HLOpcode.stind:
                        if (instr.TypeToken == HLOpType.I1)
                            WriteOpcode(TCLROpcode.CEE_STIND_I1);
                        else if (instr.TypeToken == HLOpType.I2)
                            WriteOpcode(TCLROpcode.CEE_STIND_I2);
                        else if (instr.TypeToken == HLOpType.I4)
                            WriteOpcode(TCLROpcode.CEE_STIND_I4);
                        else if (instr.TypeToken == HLOpType.I8)
                            WriteOpcode(TCLROpcode.CEE_STIND_I8);
                        else if (instr.TypeToken == HLOpType.R4)
                            WriteOpcode(TCLROpcode.CEE_STIND_R4);
                        else if (instr.TypeToken == HLOpType.R8)
                            WriteOpcode(TCLROpcode.CEE_STIND_R8);
                        else if (instr.TypeToken == HLOpType.I)
                            WriteOpcode(TCLROpcode.CEE_STIND_I);
                        else if (instr.TypeToken == HLOpType.Ref)
                            WriteOpcode(TCLROpcode.CEE_STIND_REF);
                        else
                            throw new ParseFailedException("Unknown stind type token");
                        break;
                    case HLOpcode.add:
                    case HLOpcode.sub:
                    case HLOpcode.mul:
                    case HLOpcode.div:
                    case HLOpcode.rem:
                    case HLOpcode.shr:
                        {
                            bool isUn = ((instr.Flags & HLOpFlags.Un) != 0);
                            bool isOvf = ((instr.Flags & HLOpFlags.Ovf) != 0);
                            WriteOpcode(m_arithConversions[new ArithTag(instr.Opcode, isUn, isOvf)]);
                        }
                        break;
                    case HLOpcode.conv:
                        {
                            bool isUn = ((instr.Flags & HLOpFlags.Un) != 0);
                            bool isOvf = ((instr.Flags & HLOpFlags.Ovf) != 0);
                            HLOpType type = instr.TypeToken;
                            WriteOpcode(m_convConversions[new ConvTag(isOvf, type, isUn)]);
                        }
                        break;
                    case HLOpcode.ldstr:
                        {
                            uint index = m_assemblyBuilder.IndexString((string)instr.Arguments.ObjValue);
                            if (index > 65535)
                                throw new OverflowException("Too many strings");
                            WriteOpcode(TCLROpcode.CEE_LDSTR);
                            m_writer.Write((ushort)index);
                        }
                        break;
                    case HLOpcode.cpobj:
                    case HLOpcode.ldobj:
                    case HLOpcode.castclass:
                    case HLOpcode.isinst:
                    case HLOpcode.unbox:
                    case HLOpcode.unbox_any:
                    case HLOpcode.stobj:
                    case HLOpcode.box:
                    case HLOpcode.newarr:
                    case HLOpcode.ldelema:
                    case HLOpcode.initobj:
                    case HLOpcode.@sizeof:
                        WriteOpcode(m_simpleOpConversions[instr.Opcode]);
                        m_writer.Write(CompressTypeToken((CLRTableRow)instr.Arguments.ObjValue));
                        break;
                    case HLOpcode.call:
                    case HLOpcode.callvirt:
                    case HLOpcode.newobj:
                    case HLOpcode.ldftn:
                    case HLOpcode.ldvirtftn:
                        WriteOpcode(m_simpleOpConversions[instr.Opcode]);
                        m_writer.Write(CompressMethodToken((CLRTableRow)instr.Arguments.ObjValue));
                        break;
                    case HLOpcode.ldfld:
                    case HLOpcode.ldflda:
                    case HLOpcode.stfld:
                    case HLOpcode.ldsfld:
                    case HLOpcode.ldsflda:
                    case HLOpcode.stsfld:
                        WriteOpcode(m_simpleOpConversions[instr.Opcode]);
                        m_writer.Write(CompressFieldToken((CLRTableRow)instr.Arguments.ObjValue));
                        break;
                    case HLOpcode.nop:
                    case HLOpcode.ldnull:
                    case HLOpcode.dup:
                    case HLOpcode.pop:
                    case HLOpcode.ret:
                    case HLOpcode.@break:
                    case HLOpcode.neg:
                    case HLOpcode.not:
                    case HLOpcode.endfinally:
                    case HLOpcode.endfilter:
                    case HLOpcode.unaligned_pfx:
                    case HLOpcode.volatile_pfx:
                    case HLOpcode.constrained_pfx:
                    case HLOpcode.tail_pfx:
                    case HLOpcode.rethrow:
                    case HLOpcode.and:
                    case HLOpcode.or:
                    case HLOpcode.xor:
                    case HLOpcode.shl:
                    case HLOpcode.@throw:
                    case HLOpcode.ldlen:
                    case HLOpcode.ceq:
                        WriteOpcode(m_simpleOpConversions[instr.Opcode]);
                        break;
                    case HLOpcode.ldelem:
                        if (instr.TypeToken == HLOpType.I1)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_I1);
                        else if (instr.TypeToken == HLOpType.I2)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_I2);
                        else if (instr.TypeToken == HLOpType.I4)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_I4);
                        else if (instr.TypeToken == HLOpType.I8)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_I8);
                        else if (instr.TypeToken == HLOpType.U1)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_U1);
                        else if (instr.TypeToken == HLOpType.U2)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_U2);
                        else if (instr.TypeToken == HLOpType.U4)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_U4);
                        else if (instr.TypeToken == HLOpType.U8)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_U8);
                        else if (instr.TypeToken == HLOpType.R4)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_R4);
                        else if (instr.TypeToken == HLOpType.R8)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_R8);
                        else if (instr.TypeToken == HLOpType.I)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_I);
                        else if (instr.TypeToken == HLOpType.Ref)
                            WriteOpcode(TCLROpcode.CEE_LDELEM_REF);
                        else
                            throw new ParseFailedException("Unsupported ldelem type");
                        break;
                    case HLOpcode.stelem:
                        if (instr.TypeToken == HLOpType.I1)
                            WriteOpcode(TCLROpcode.CEE_STELEM_I1);
                        else if (instr.TypeToken == HLOpType.I2)
                            WriteOpcode(TCLROpcode.CEE_STELEM_I2);
                        else if (instr.TypeToken == HLOpType.I4)
                            WriteOpcode(TCLROpcode.CEE_STELEM_I4);
                        else if (instr.TypeToken == HLOpType.I8)
                            WriteOpcode(TCLROpcode.CEE_STELEM_I8);
                        else if (instr.TypeToken == HLOpType.R4)
                            WriteOpcode(TCLROpcode.CEE_STELEM_R4);
                        else if (instr.TypeToken == HLOpType.R8)
                            WriteOpcode(TCLROpcode.CEE_STELEM_R8);
                        else if (instr.TypeToken == HLOpType.I)
                            WriteOpcode(TCLROpcode.CEE_STELEM_I);
                        else if (instr.TypeToken == HLOpType.Ref)
                            WriteOpcode(TCLROpcode.CEE_STELEM_REF);
                        else
                            throw new ParseFailedException("Unsupported stelem type");
                        break;
                    case HLOpcode.ldtoken:
                        {
                            TCLRTablesEnum tblIndex;
                            uint rowNum = 0;
                            CLRTableRow tableRow = (CLRTableRow)instr.Arguments.ObjValue;
                            if (tableRow is CLRMethodDefRow)
                                tblIndex = TCLRTablesEnum.TBL_MethodDef;
                            else if (tableRow is CLRMemberRefRow)
                            {
                                CLRMemberRefRow memberRefRow = (CLRMemberRefRow)tableRow;
                                if (memberRefRow.MethodSig != null)
                                {
                                    tblIndex = TCLRTablesEnum.TBL_MethodRef;
                                    rowNum = m_assemblyBuilder.IndexMethodRef(memberRefRow);
                                }
                                else if (memberRefRow.FieldSig != null)
                                {
                                    tblIndex = TCLRTablesEnum.TBL_FieldRef;
                                    rowNum = m_assemblyBuilder.IndexFieldRef(memberRefRow);
                                }
                                else
                                    throw new ParseFailedException("Unknown member ref type");
                            }
                            else if (tableRow is CLRMethodSpecRow)
                                // CLARITYTODO: Generic support?
                                throw new ParseFailedException("Can't export method spec references");
                            else if (tableRow is CLRTypeDefRow)
                            {
                                tblIndex = TCLRTablesEnum.TBL_TypeDef;
                                rowNum = m_assemblyBuilder.IndexTypeDef((CLRTypeDefRow)tableRow);
                            }
                            else if (tableRow is CLRTypeRefRow)
                            {
                                tblIndex = TCLRTablesEnum.TBL_TypeRef;
                                rowNum = m_assemblyBuilder.IndexTypeRef((CLRTypeRefRow)tableRow);
                            }
                            else if (tableRow is CLRFieldRow)
                            {
                                tblIndex = TCLRTablesEnum.TBL_FieldDef;
                                rowNum = m_assemblyBuilder.IndexFieldDef((CLRFieldRow)tableRow);
                            }
                            else
                                throw new ParseFailedException("Unknown ldtoken type");

                            if (rowNum > 0x00ffffff)
                                throw new OverflowException("Token overflow");

                            uint token = rowNum | ((uint)tblIndex << 24);

                            WriteOpcode(TCLROpcode.CEE_LDTOKEN);
                            m_writer.Write(token);
                        }
                        break;
                    case HLOpcode.cgt:
                        if ((instr.Flags & HLOpFlags.Un) != 0)
                            WriteOpcode(TCLROpcode.CEE_CGT_UN);
                        else
                            WriteOpcode(TCLROpcode.CEE_CGT);
                        break;
                    case HLOpcode.clt:
                        if ((instr.Flags &HLOpFlags.Un) != 0)
                            WriteOpcode(TCLROpcode.CEE_CLT_UN);
                        else
                            WriteOpcode(TCLROpcode.CEE_CLT);
                        break;
                    case HLOpcode.starg:
                        {
                            uint index = instr.Arguments.U32Value;
                            if (index <= 255)
                            {
                                WriteOpcode(TCLROpcode.CEE_STARG_S);
                                m_writer.Write((byte)index);
                            }
                            else if (index <= 65535)
                            {
                                WriteOpcode(TCLROpcode.CEE_STARG);
                                m_writer.Write((ushort)index);
                            }
                            else
                                throw new OverflowException("Oversized starg");
                        }
                        break;
                    case HLOpcode.ldloca:
                        {
                            uint index = instr.Arguments.U32Value;
                            if (index <= 255)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDLOCA_S);
                                m_writer.Write((byte)index);
                            }
                            else if (index <= 65535)
                            {
                                WriteOpcode(TCLROpcode.CEE_LDLOCA);
                                m_writer.Write((ushort)index);
                            }
                            else
                                throw new OverflowException("Oversized ldloca");
                        }
                        break;
                    case HLOpcode.br:
                    case HLOpcode.brfalse:
                    case HLOpcode.brtrue:
                    case HLOpcode.beq:
                    case HLOpcode.bne:
                    case HLOpcode.bge:
                    case HLOpcode.bgt:
                    case HLOpcode.ble:
                    case HLOpcode.blt:
                    case HLOpcode.leave:
                        {
                            bool isUn = ((instr.Flags & HLOpFlags.Un) != 0);
                            bool isShort = m_longFormJumps.Contains(instrNum);

                            int pc = (int)m_memoryStream.Position;
                            if (isShort)
                                pc += 2;
                            else
                                pc += 3;
                            int offset = (int)instr.Arguments.U32Value - pc;
                            BranchTag brtag = new BranchTag(instr.Opcode, isUn, isShort);

                            if (offset < -128 || offset > 127)
                            {
                                if (!onlyOffsets)
                                {
                                    m_longFormJumps.Add(instrNum);
                                    m_anyJumpsInvalid = true;
                                }
                                offset = 0;
                            }
                            WriteOpcode(m_branchConversions[brtag]);
                            if (isShort)
                                m_writer.Write((sbyte)offset);
                            else
                                m_writer.Write((short)offset);
                        }
                        break;
                    case HLOpcode.arglist:
                    case HLOpcode.cpblk:
                    case HLOpcode.jmp:
                    case HLOpcode.initblk:
                    case HLOpcode.calli:
                    case HLOpcode.ckfinite:
                    case HLOpcode.readonly_pfx:
                    case HLOpcode.localloc:
                    case HLOpcode.mkrefany:
                    case HLOpcode.refanytype:
                    case HLOpcode.refanyval:
                    case HLOpcode.no_pfx:
                        throw new ParseFailedException("Unsupported opcode");
                    default:
                        throw new NotImplementedException("Unrecognized opcode");
                }
            }
        }

        private ushort CompressMethodToken(CLRTableRow row)
        {
            uint rowNumber;
            ushort token;
            if (row is CLRMethodDefRow)
            {
                token = 0;
                rowNumber = m_assemblyBuilder.IndexMethodDef((CLRMethodDefRow)row);
            }
            else if (row is CLRMemberRefRow && ((CLRMemberRefRow)row).MethodSig != null)
            {
                token = 0x8000;
                rowNumber = m_assemblyBuilder.IndexMethodRef((CLRMemberRefRow)row);
            }
            else
                throw new ParseFailedException("Unexpected method token");

            if (rowNumber > 0x7fff)
                throw new ParseFailedException("Too many method defs/refs");
            token |= (ushort)rowNumber;
            return token;
        }

        private ushort CompressTypeToken(CLRTableRow row)
        {
            ushort token;
            uint rowNumber;
            if (row is CLRTypeDefRow)
            {
                token = 0;
                rowNumber = m_assemblyBuilder.IndexTypeDef((CLRTypeDefRow)row);
            }
            else if (row is CLRTypeRefRow)
            {
                token = 0x4000;
                rowNumber = m_assemblyBuilder.IndexTypeRef((CLRTypeRefRow)row);
            }
            else if (row is CLRTypeSpecRow)
            {
                token = 0xc000;
                rowNumber = m_assemblyBuilder.IndexTypeSpec((CLRTypeSpecRow)row);
            }
            else
                throw new ParseFailedException("Unexpected type token");

            if (rowNumber > 0x3fff)
                throw new ParseFailedException("Too many type defs/refs/specs");

            token |= (ushort)rowNumber;
            return token;
        }

        private ushort CompressFieldToken(CLRTableRow row)
        {
            ushort token;
            uint rowNumber;
            if (row is CLRFieldRow)
            {
                token = 0;
                rowNumber = m_assemblyBuilder.IndexFieldDef((CLRFieldRow)row);
            }
            else if (row is CLRMemberRefRow && ((CLRMemberRefRow)row).FieldSig != null)
            {
                token = 0x8000;
                rowNumber = m_assemblyBuilder.IndexFieldRef((CLRMemberRefRow)row);
            }
            else
                throw new ParseFailedException("Unexpected method token");

            if (rowNumber > 0x7fff)
                throw new ParseFailedException("Too many field defs/refs");
            token |= (ushort)rowNumber;
            return token;
        }

        private byte[] Work()
        {
            m_anyJumpsInvalid = true;
            while (m_anyJumpsInvalid)
            {
                EmitBytecode(false);
                m_anyJumpsInvalid = false;
                m_switchInvalid = false;
                EmitBytecode(true);
            }
            if (m_switchInvalid)
                throw new ParseFailedException("Switch jumps too far");
            return m_memoryStream.ToArray();
        }

        public static byte[] ImportMethod(CLRMethodDefRow methodDef, TCLRAssemblyBuilder assemblyBuilder, bool bigEndian)
        {
            byte[] result;
            using (TCLRBytecodeBuilder builder = new TCLRBytecodeBuilder(methodDef, assemblyBuilder, bigEndian))
            {
                result = builder.Work();
            }
            return result;
        }

        void IDisposable.Dispose()
        {
            m_memoryStream.Dispose();
        }
    }
}
