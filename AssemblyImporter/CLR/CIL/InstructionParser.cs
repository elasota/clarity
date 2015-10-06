using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.CLR.CIL
{
    public class InstructionParser
    {
        private BinaryReader m_reader;
        private MemoryStream m_stream;
        private CLRMetaDataTables m_tables;
        private CLRMetaStreamBinaryData m_binData;

        public InstructionParser(byte[] bytecode, CLRMetaDataTables tables, CLRMetaStreamBinaryData binData)
        {
            m_stream = new MemoryStream(bytecode, false);
            m_reader = new BinaryReader(m_stream);
            m_tables = tables;
            m_binData = binData;
        }

        public bool AtEnd
        {
            get
            {
                return m_stream.Position == m_stream.Length;
            }
        }

        public long Position
        {
            get
            {
                return m_stream.Position;
            }
        }

        private static HashSet<Type> s_LoadTokenTypes = new HashSet<Type>(
            new Type[] { typeof(CLRMethodDefRow), typeof(CLRMemberRefRow), typeof(CLRMethodSpecRow),
                typeof(CLRTypeDefRow), typeof(CLRTypeRefRow), typeof(CLRTypeSpecRow),
                typeof(CLRFieldRow) }
            );

        private static HashSet<Type> s_MethodDefOrRefTypes = new HashSet<Type>(
            new Type[] { typeof(CLRMethodDefRow), typeof(CLRMemberRefRow) }
            );

        private static HashSet<Type> s_MethodDefOrRefOrSpecTypes = new HashSet<Type>(
            new Type[] { typeof(CLRMethodDefRow), typeof(CLRMemberRefRow), typeof(CLRMethodSpecRow) }
            );

        private static HashSet<Type> s_TypeDefOrRefOrSpecTypes = new HashSet<Type>(
            new Type[] { typeof(CLRTypeDefRow), typeof(CLRTypeRefRow), typeof(CLRTypeSpecRow) }
            );

        private static HashSet<Type> s_FieldDefOrRefTypes = new HashSet<Type>(
            new Type[] { typeof(CLRMemberRefRow), typeof(CLRFieldRow) }
            );

        // III.1.9
        public CLRTableRow ReadMetaToken(ArgEncoding argRestriction)
        {
            uint rawToken = m_reader.ReadUInt32();
            uint tableIndex = rawToken >> 24;
            uint rowIndex = rawToken & 0x00ffffff;
            if (rowIndex == 0)
                return null;

            CLRTableRow row = m_tables.GetTable((int)tableIndex).GetRow(rowIndex - 1);
            switch (argRestriction)
            {
                case ArgEncoding.LoadToken:
                    if (!s_LoadTokenTypes.Contains(row.GetType()))
                        throw new ParseFailedException("Invalid ldtoken token");
                    return row;
                case ArgEncoding.MethodDefOrRef:
                    if (!s_MethodDefOrRefTypes.Contains(row.GetType()))
                        throw new ParseFailedException("Invalid token");
                    if (row.GetType() == typeof(CLRMemberRefRow) && ((CLRMemberRefRow)row).MethodSig == null)
                        throw new ParseFailedException("Invalid token)");
                    return row;
                case ArgEncoding.MethodDefOrRefOrSpec:
                    if (!s_MethodDefOrRefOrSpecTypes.Contains(row.GetType()))
                        throw new ParseFailedException("Invalid token");
                    if (row.GetType() == typeof(CLRMemberRefRow) && ((CLRMemberRefRow)row).MethodSig == null)
                        throw new ParseFailedException("Invalid token)");
                    return row;
                case ArgEncoding.TypeDefOrRefOrSpec:
                    if (!s_TypeDefOrRefOrSpecTypes.Contains(row.GetType()))
                        throw new ParseFailedException("Invalid token");
                    return row;
                case ArgEncoding.Field:
                    if (!s_FieldDefOrRefTypes.Contains(row.GetType()))
                        throw new ParseFailedException("Invalid token");
                    if (row.GetType() == typeof(CLRMemberRefRow) && ((CLRMemberRefRow)row).FieldSig == null)
                        throw new ParseFailedException("Invalid token)");
                    return row;
            }
            throw new NotImplementedException();
        }

        private HLInstruction Decode_FE()
        {
            byte opcode = m_reader.ReadByte();

            switch (opcode)
            {
                case 0x16:
                    return new HLInstruction(HLOpcode.constrained_pfx);
                case 0x19:
                    return new HLInstruction(HLOpcode.no_pfx).Args(DecodeArgs(ArgEncoding.No));
                case 0x1e:
                    return new HLInstruction(HLOpcode.readonly_pfx);
                case 0x14:
                    return new HLInstruction(HLOpcode.tail_pfx);
                case 0x12:
                    return new HLInstruction(HLOpcode.unaligned_pfx).Args(DecodeArgs(ArgEncoding.U8));
                case 0x13:
                    return new HLInstruction(HLOpcode.volatile_pfx);
                case 0x00:
                    return new HLInstruction(HLOpcode.arglist);
                case 0x01:
                    return new HLInstruction(HLOpcode.ceq);
                case 0x02:
                    return new HLInstruction(HLOpcode.cgt);
                case 0x03:
                    return new HLInstruction(HLOpcode.cgt).Fl(HLOpFlags.Un);
                case 0x04:
                    return new HLInstruction(HLOpcode.clt);
                case 0x05:
                    return new HLInstruction(HLOpcode.clt).Fl(HLOpFlags.Un);
                case 0x17:
                    return new HLInstruction(HLOpcode.cpblk);
                case 0x11:
                    return new HLInstruction(HLOpcode.endfilter);
                case 0x18:
                    return new HLInstruction(HLOpcode.initblk);
                case 0x09:
                    return new HLInstruction(HLOpcode.ldarg).Args(DecodeArgs(ArgEncoding.U16));
                case 0x0a:
                    return new HLInstruction(HLOpcode.ldarga).Args(DecodeArgs(ArgEncoding.U16));
                case 0x06:
                    // NOTE: Standard says this is MethodDefOrRef but is actually MethodDefOrRefOrSpec
                    return new HLInstruction(HLOpcode.ldftn).Args(DecodeArgs(ArgEncoding.MethodDefOrRefOrSpec));
                case 0x0c:
                    return new HLInstruction(HLOpcode.ldloc).Args(DecodeArgs(ArgEncoding.U16));
                case 0x0d:
                    return new HLInstruction(HLOpcode.ldloca).Args(DecodeArgs(ArgEncoding.U16));
                case 0x0f:
                    return new HLInstruction(HLOpcode.localloc);
                case 0x0b:
                    return new HLInstruction(HLOpcode.starg).Args(DecodeArgs(ArgEncoding.U16));
                case 0x0e:
                    return new HLInstruction(HLOpcode.stloc).Args(DecodeArgs(ArgEncoding.U16));
                case 0x15:
                    return new HLInstruction(HLOpcode.initobj).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));
                case 0x07:
                    return new HLInstruction(HLOpcode.ldvirtftn).Args(DecodeArgs(ArgEncoding.MethodDefOrRefOrSpec));
                case 0x1d:
                    return new HLInstruction(HLOpcode.refanytype);
                case 0x1a:
                    return new HLInstruction(HLOpcode.rethrow);
                case 0x1c:
                    return new HLInstruction(HLOpcode.@sizeof).Args(DecodeArgs(ArgEncoding.MethodDefOrRefOrSpec));
                default:
                    break;
            }

            throw new ParseFailedException("Unknown opcode");
        }

        public HLInstruction DecodeInstruction()
        {
            byte opcode = m_reader.ReadByte();

            switch (opcode)
            {
                case 0xfe:
                    return Decode_FE();

                // add
                case 0x58:
                    return new HLInstruction(HLOpcode.add);
                case 0xd6:
                    return new HLInstruction(HLOpcode.add).Fl(HLOpFlags.Ovf);
                case 0xd7:
                    return new HLInstruction(HLOpcode.add).Fl(HLOpFlags.Ovf | HLOpFlags.Un);

                // and
                case 0x5f:
                    return new HLInstruction(HLOpcode.and);

                // beq
                case 0x3b:
                    return new HLInstruction(HLOpcode.beq).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x2e:
                    return new HLInstruction(HLOpcode.beq).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));

                // bge
                case 0x3c:
                    return new HLInstruction(HLOpcode.bge).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x2f:
                    return new HLInstruction(HLOpcode.bge).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));
                case 0x41:
                    return new HLInstruction(HLOpcode.bge).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I32));
                case 0x34:
                    return new HLInstruction(HLOpcode.bge).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I8));

                // bgt
                case 0x3d:
                    return new HLInstruction(HLOpcode.bgt).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x30:
                    return new HLInstruction(HLOpcode.bgt).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));
                case 0x42:
                    return new HLInstruction(HLOpcode.bgt).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I32));
                case 0x35:
                    return new HLInstruction(HLOpcode.bgt).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I8));

                // ble
                case 0x3e:
                    return new HLInstruction(HLOpcode.ble).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x31:
                    return new HLInstruction(HLOpcode.ble).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));
                case 0x43:
                    return new HLInstruction(HLOpcode.ble).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I32));
                case 0x36:
                    return new HLInstruction(HLOpcode.ble).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I8));

                // blt
                case 0x3f:
                    return new HLInstruction(HLOpcode.blt).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x32:
                    return new HLInstruction(HLOpcode.blt).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));
                case 0x44:
                    return new HLInstruction(HLOpcode.blt).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I32));
                case 0x37:
                    return new HLInstruction(HLOpcode.blt).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I8));

                // bne
                case 0x40:
                    return new HLInstruction(HLOpcode.bne).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I32));
                case 0x33:
                    return new HLInstruction(HLOpcode.bne).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I8));

                // br
                case 0x38:
                    return new HLInstruction(HLOpcode.br).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I32));
                case 0x2b:
                    return new HLInstruction(HLOpcode.br).Fl(HLOpFlags.Br | HLOpFlags.Un).Args(DecodeArgs(ArgEncoding.I8));

                // break
                case 0x01:
                    return new HLInstruction(HLOpcode.@break);

                // brfalse
                case 0x39:
                    return new HLInstruction(HLOpcode.brfalse).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x2c:
                    return new HLInstruction(HLOpcode.brfalse).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));

                // brtrue
                case 0x3a:
                    return new HLInstruction(HLOpcode.brtrue).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0x2d:
                    return new HLInstruction(HLOpcode.brtrue).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));

                // call
                case 0x28:
                    return new HLInstruction(HLOpcode.call).Args(DecodeArgs(ArgEncoding.MethodDefOrRefOrSpec));

                // calli
                case 0x29:
                    return new HLInstruction(HLOpcode.calli).Args(DecodeArgs(ArgEncoding.MethodDefOrRefOrSpec));

                // ckfinite
                case 0xc3:
                    return new HLInstruction(HLOpcode.ckfinite);

                // conv
                case 0x67:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.I1);
                case 0x68:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.I2);
                case 0x69:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.I4);
                case 0x6a:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.I8);
                case 0x6b:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.R4);
                case 0x6c:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.R8);
                case 0xd2:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.U1);
                case 0xd1:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.U2);
                case 0x6d:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.U4);
                case 0x6e:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.U8);
                case 0xd3:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.I);
                case 0xe0:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.U);
                case 0x76:
                    return new HLInstruction(HLOpcode.conv).TT(HLOpType.R).Fl(HLOpFlags.Un);
                case 0xb3:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.I1);
                case 0xb5:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.I2);
                case 0xb7:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.I4);
                case 0xb9:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.I8);
                case 0xb4:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.U1);
                case 0xb6:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.U2);
                case 0xb8:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.U4);
                case 0xba:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.U8);
                case 0xd4:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.I);
                case 0xd5:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf).TT(HLOpType.U);
                case 0x82:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.I1);
                case 0x83:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.I2);
                case 0x84:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.I4);
                case 0x85:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.I8);
                case 0x86:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.U1);
                case 0x87:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.U2);
                case 0x88:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.U4);
                case 0x89:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.U8);
                case 0x8a:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.I);
                case 0x8b:
                    return new HLInstruction(HLOpcode.conv).Fl(HLOpFlags.Ovf | HLOpFlags.Un).TT(HLOpType.U);

                // div
                case 0x5b:
                    return new HLInstruction(HLOpcode.div);
                case 0x5c:
                    return new HLInstruction(HLOpcode.div).Fl(HLOpFlags.Un);

                // dup
                case 0x25:
                    return new HLInstruction(HLOpcode.dup);

                // endfinally
                case 0xdc:
                    return new HLInstruction(HLOpcode.endfinally);

                // jmp
                case 0x27:
                    return new HLInstruction(HLOpcode.jmp).Args(DecodeArgs(ArgEncoding.MethodDefOrRef));

                // ldarg
                case 0x0e:
                    return new HLInstruction(HLOpcode.ldarg).Args(DecodeArgs(ArgEncoding.U8));
                case 0x02:
                    return new HLInstruction(HLOpcode.ldarg).Args(SimpleArgU(0));
                case 0x03:
                    return new HLInstruction(HLOpcode.ldarg).Args(SimpleArgU(1));
                case 0x04:
                    return new HLInstruction(HLOpcode.ldarg).Args(SimpleArgU(2));
                case 0x05:
                    return new HLInstruction(HLOpcode.ldarg).Args(SimpleArgU(3));

                // ldarga
                case 0x0f:
                    return new HLInstruction(HLOpcode.ldarga).Args(DecodeArgs(ArgEncoding.U8));

                // ldc
                case 0x20:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(DecodeArgs(ArgEncoding.I32));
                case 0x21:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I8).Args(DecodeArgs(ArgEncoding.I64));
                case 0x22:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.R).Args(DecodeArgs(ArgEncoding.F32));
                case 0x23:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.R).Args(DecodeArgs(ArgEncoding.F64));
                case 0x16:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(0));
                case 0x17:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(1));
                case 0x18:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(2));
                case 0x19:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(3));
                case 0x1a:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(4));
                case 0x1b:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(5));
                case 0x1c:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(6));
                case 0x1d:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(7));
                case 0x1e:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(8));
                case 0x15:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(SimpleArgS(-1));
                case 0x1f:
                    return new HLInstruction(HLOpcode.ldc).TT(HLOpType.I4).Args(DecodeArgs(ArgEncoding.I8));

                // ldind
                case 0x46:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.I1);
                case 0x48:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.I2);
                case 0x4a:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.I4);
                case 0x4c:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.I8);
                case 0x47:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.U1);
                case 0x49:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.U2);
                case 0x4b:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.U4);
                case 0x4e:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.R4);
                //case 0x4c:
                //    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.U8);
                case 0x4f:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.R8);
                case 0x4d:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.I);
                case 0x50:
                    return new HLInstruction(HLOpcode.ldind).TT(HLOpType.Ref);

                // ldloc
                case 0x11:
                    return new HLInstruction(HLOpcode.ldloc).Args(DecodeArgs(ArgEncoding.U8));
                case 0x06:
                    return new HLInstruction(HLOpcode.ldloc).Args(SimpleArgU(0));
                case 0x07:
                    return new HLInstruction(HLOpcode.ldloc).Args(SimpleArgU(1));
                case 0x08:
                    return new HLInstruction(HLOpcode.ldloc).Args(SimpleArgU(2));
                case 0x09:
                    return new HLInstruction(HLOpcode.ldloc).Args(SimpleArgU(3));

                // ldloca
                case 0x12:
                    return new HLInstruction(HLOpcode.ldloca).Args(DecodeArgs(ArgEncoding.U8));

                // ldnull
                case 0x14:
                    return new HLInstruction(HLOpcode.ldnull);

                // leave
                case 0xdd:
                    return new HLInstruction(HLOpcode.leave).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I32));
                case 0xde:
                    return new HLInstruction(HLOpcode.leave).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.I8));

                // mul
                case 0x5a:
                    return new HLInstruction(HLOpcode.mul);
                case 0xd8:
                    return new HLInstruction(HLOpcode.mul).Fl(HLOpFlags.Ovf);
                case 0xd9:
                    return new HLInstruction(HLOpcode.mul).Fl(HLOpFlags.Ovf | HLOpFlags.Un);

                // neg
                case 0x65:
                    return new HLInstruction(HLOpcode.neg);

                // nop
                case 0x00:
                    return new HLInstruction(HLOpcode.nop);

                // not
                case 0x66:
                    return new HLInstruction(HLOpcode.not);

                // or
                case 0x60:
                    return new HLInstruction(HLOpcode.or);

                // pop
                case 0x26:
                    return new HLInstruction(HLOpcode.pop);

                // rem
                case 0x5d:
                    return new HLInstruction(HLOpcode.rem);
                case 0x5e:
                    return new HLInstruction(HLOpcode.rem).Fl(HLOpFlags.Un);

                // ret
                case 0x2a:
                    return new HLInstruction(HLOpcode.ret);

                // shl
                case 0x62:
                    return new HLInstruction(HLOpcode.shl);

                // shr
                case 0x63:
                    return new HLInstruction(HLOpcode.shr);
                case 0x64:
                    return new HLInstruction(HLOpcode.shr).Fl(HLOpFlags.Un);

                // starg
                case 0x10:
                    return new HLInstruction(HLOpcode.starg).Args(DecodeArgs(ArgEncoding.U8));

                // stind
                case 0x52:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.I1);
                case 0x53:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.I2);
                case 0x54:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.I4);
                case 0x55:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.I8);
                case 0x56:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.R4);
                case 0x57:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.R8);
                case 0xdf:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.I);
                case 0x51:
                    return new HLInstruction(HLOpcode.stind).TT(HLOpType.Ref);

                // stloc
                case 0x13:
                    return new HLInstruction(HLOpcode.stloc).Args(DecodeArgs(ArgEncoding.U8));
                case 0x0a:
                    return new HLInstruction(HLOpcode.stloc).Args(SimpleArgU(0));
                case 0x0b:
                    return new HLInstruction(HLOpcode.stloc).Args(SimpleArgU(1));
                case 0x0c:
                    return new HLInstruction(HLOpcode.stloc).Args(SimpleArgU(2));
                case 0x0d:
                    return new HLInstruction(HLOpcode.stloc).Args(SimpleArgU(3));

                // sub
                case 0x59:
                    return new HLInstruction(HLOpcode.sub);
                case 0xda:
                    return new HLInstruction(HLOpcode.sub).Fl(HLOpFlags.Ovf);
                case 0xdb:
                    return new HLInstruction(HLOpcode.sub).Fl(HLOpFlags.Ovf | HLOpFlags.Un);

                // switch
                case 0x45:
                    return new HLInstruction(HLOpcode.@switch).Fl(HLOpFlags.Br).Args(DecodeArgs(ArgEncoding.Switch));

                // xor
                case 0x61:
                    return new HLInstruction(HLOpcode.xor);

                // III.4 Object model instructions
                // box
                case 0x8c:
                    return new HLInstruction(HLOpcode.box).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // callvirt
                case 0x6f:
                    return new HLInstruction(HLOpcode.callvirt).Args(DecodeArgs(ArgEncoding.MethodDefOrRefOrSpec));

                // castclass
                case 0x74:
                    return new HLInstruction(HLOpcode.castclass).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // cpobj
                case 0x70:
                    return new HLInstruction(HLOpcode.cpobj).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // isinst
                case 0x75:
                    return new HLInstruction(HLOpcode.isinst).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // ldelem
                case 0xa3:
                    return new HLInstruction(HLOpcode.ldelem).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));
                case 0x90:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.I1);
                case 0x92:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.I2);
                case 0x94:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.I4);
                case 0x96:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.I8);
                case 0x91:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.U1);
                case 0x93:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.U2);
                case 0x95:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.U4);
                //case 0x96:
                //    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.U8);
                case 0x98:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.R4);
                case 0x99:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.R8);
                case 0x97:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.I);
                case 0x9a:
                    return new HLInstruction(HLOpcode.ldelem).TT(HLOpType.Ref);

                // ldelema
                case 0x8f:
                    return new HLInstruction(HLOpcode.ldelema).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // ldfld
                case 0x7b:
                    return new HLInstruction(HLOpcode.ldfld).Args(DecodeArgs(ArgEncoding.Field));

                // ldflda
                case 0x7c:
                    return new HLInstruction(HLOpcode.ldflda).Args(DecodeArgs(ArgEncoding.Field));

                // ldflda
                case 0x8e:
                    return new HLInstruction(HLOpcode.ldlen);

                // ldobj
                case 0x71:
                    return new HLInstruction(HLOpcode.ldobj).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // ldsfld
                case 0x7e:
                    return new HLInstruction(HLOpcode.ldsfld).Args(DecodeArgs(ArgEncoding.Field));

                // ldsflda
                case 0x7f:
                    return new HLInstruction(HLOpcode.ldsflda).Args(DecodeArgs(ArgEncoding.Field));

                // ldstr
                case 0x72:
                    return new HLInstruction(HLOpcode.ldstr).Args(DecodeArgs(ArgEncoding.String));

                // ldtoken
                case 0xd0:
                    return new HLInstruction(HLOpcode.ldtoken).Args(DecodeArgs(ArgEncoding.LoadToken));

                // mkrefany
                case 0xc6:
                    return new HLInstruction(HLOpcode.mkrefany).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // newarr
                case 0x8d:
                    return new HLInstruction(HLOpcode.newarr).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // newobj
                case 0x73:
                    return new HLInstruction(HLOpcode.newobj).Args(DecodeArgs(ArgEncoding.MethodDefOrRef));

                // refanyval
                case 0xc2:
                    return new HLInstruction(HLOpcode.refanyval).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // stelem
                case 0xa4:
                    return new HLInstruction(HLOpcode.stelem).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));
                case 0x9c:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.I1);
                case 0x9d:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.I2);
                case 0x9e:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.I4);
                case 0x9f:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.I8);
                case 0xa0:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.R4);
                case 0xa1:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.R8);
                case 0x9b:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.I);
                case 0xa2:
                    return new HLInstruction(HLOpcode.stelem).TT(HLOpType.Ref);

                // stfld
                case 0x7d:
                    return new HLInstruction(HLOpcode.stfld).Args(DecodeArgs(ArgEncoding.Field));

                // stobj
                case 0x81:
                    return new HLInstruction(HLOpcode.stobj).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // stsfld
                case 0x80:
                    return new HLInstruction(HLOpcode.stsfld).Args(DecodeArgs(ArgEncoding.Field));

                // throw
                case 0x7a:
                    return new HLInstruction(HLOpcode.@throw);

                // unbox
                case 0x79:
                    return new HLInstruction(HLOpcode.unbox).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));

                // unbox.any
                case 0xa5:
                    return new HLInstruction(HLOpcode.unbox_any).Args(DecodeArgs(ArgEncoding.TypeDefOrRefOrSpec));
            }

            throw new ParseFailedException("Unknown opcode");
        }

        public HLArguments DecodeArgs(ArgEncoding encoding)
        {
            switch (encoding)
            {
                case ArgEncoding.None:
                    throw new ArgumentException();
                case ArgEncoding.I8:
                    return new HLArguments((int)m_reader.ReadSByte());
                case ArgEncoding.I16:
                    return new HLArguments((int)m_reader.ReadInt16());
                case ArgEncoding.I32:
                    return new HLArguments(m_reader.ReadInt32());
                case ArgEncoding.I64:
                    return new HLArguments(m_reader.ReadInt64());
                case ArgEncoding.U8:
                    return new HLArguments((uint)m_reader.ReadByte());
                case ArgEncoding.U16:
                    return new HLArguments((uint)m_reader.ReadUInt16());
                case ArgEncoding.U32:
                    return new HLArguments(m_reader.ReadUInt32());
                case ArgEncoding.U64:
                    return new HLArguments(m_reader.ReadUInt64());
                case ArgEncoding.F32:
                    return new HLArguments(m_reader.ReadSingle());
                case ArgEncoding.F64:
                    return new HLArguments(m_reader.ReadDouble());
                case ArgEncoding.No:
                    return HLArguments.CreateNo(m_reader.ReadByte());
                case ArgEncoding.MethodDefOrRefOrSpec:
                case ArgEncoding.MethodDefOrRef:
                case ArgEncoding.TypeDefOrRefOrSpec:
                case ArgEncoding.LoadToken:
                case ArgEncoding.Field:
                    return new HLArguments(ReadMetaToken(encoding));
                case ArgEncoding.Switch:
                    // III.3.66
                    {
                        uint numCases = m_reader.ReadUInt32();
                        int[] caseTargets = new int[numCases];
                        for (uint i=0; i<numCases; i++)
                            caseTargets[i] = m_reader.ReadInt32();
                        return HLArguments.CreateSwitch(caseTargets);
                    }
                case ArgEncoding.String:
                    {
                        // III.1.9
                        List<char> chars = new List<char>();
                        uint rawToken = m_reader.ReadUInt32();
                        if (rawToken >> 24 != 0x70)
                            throw new ParseFailedException("Unexpected string token");
                        ArraySegment<byte> utf16string = CLRMetaDataParser.ReadBlobOrUS(m_binData.USData, rawToken & 0x00ffffff);

                        // II.24.2.4
                        // US strings are UTF-16, terminator is 1 if any code point is non-ASCII
                        byte terminator = utf16string.Array[utf16string.Offset + utf16string.Count - 1];
                        if ((utf16string.Count & 1) != 1 || (terminator != 0 && terminator != 1))
                            throw new ParseFailedException("Invalid inline string");

                        for (int offset = 0; offset < utf16string.Count - 1; offset += 2)
                        {
                            int trueOffset = offset + utf16string.Offset;
                            int codePoint = utf16string.Array[trueOffset] | (utf16string.Array[trueOffset + 1] << 8);
                            chars.Add((char)codePoint);
                        }

                        return new HLArguments(new string(chars.ToArray()));
                    }
                default:
                    break;
            }

            throw new NotImplementedException();
        }

        public HLArguments SimpleArgU(uint value)
        {
            return new HLArguments(value);
        }

        public HLArguments SimpleArgS(int value)
        {
            return new HLArguments(value);
        }
    }
}
