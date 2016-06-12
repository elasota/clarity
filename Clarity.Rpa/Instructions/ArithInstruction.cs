using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa.Instructions
{
    public sealed class ArithInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private NumberArithOp m_arithOp;
        private NumberArithType m_arithType;
        private HighSsaRegister m_left;
        private HighSsaRegister m_right;
        private bool m_checkOverflow;

        public HighSsaRegister Dest { get { return m_dest; } }
        public NumberArithOp ArithOp { get { return m_arithOp; } }
        public NumberArithType ArithType { get { return m_arithType; } }
        public HighSsaRegister Left { get { return m_left; } }
        public HighSsaRegister Right { get { return m_right; } }
        public bool CheckOverflow { get { return m_checkOverflow; } }

        public override Opcodes Opcode { get { return Opcodes.Arith; } }

        public ArithInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, NumberArithOp arithOp, NumberArithType arithType, HighSsaRegister left, HighSsaRegister right, bool checkOverflow)
            : base(codeLocation)
        {
            m_dest = dest;
            m_arithOp = arithOp;
            m_arithType = arithType;
            m_left = left;
            m_right = right;
            m_checkOverflow = checkOverflow;
        }

        public ArithInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_left);
            visitor(ref m_right);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((byte)m_arithOp);
            writer.Write((byte)m_arithType);
            writer.Write(m_checkOverflow);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_arithOp.ToString());
            dw.Write(" ");
            dw.Write(m_arithType.ToString());
            dw.Write(" ");
            dw.Write(m_checkOverflow.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_arithOp = (NumberArithOp)reader.ReadByte();
            if (m_arithOp < 0 || m_arithOp >= NumberArithOp.NumHighOpTypes)
                throw new Exception("Invalid arith op");

            m_arithType = (NumberArithType)reader.ReadByte();
            if (m_arithType < 0 || m_arithType >= NumberArithType.NumHighArithTypes)
                throw new Exception("Invalid arith type");

            m_checkOverflow = reader.ReadBoolean();
        }

        protected override HighInstruction CloneImpl()
        {
            return new ArithInstruction(CodeLocation, m_dest, m_arithOp, m_arithType, m_left, m_right, m_checkOverflow);
        }

        public override bool MayThrow
        {
            get
            {
                switch (m_arithType)
                {
                    case NumberArithType.Float32:
                    case NumberArithType.Float64:
                        return false;
                    case NumberArithType.Int32:
                    case NumberArithType.Int64:
                    case NumberArithType.NativeInt:
                    case NumberArithType.NativeUInt:
                    case NumberArithType.UInt32:
                    case NumberArithType.UInt64:
                        break;
                    default:
                        throw new Exception();
                }

                if (m_checkOverflow)
                    return true;

                switch (m_arithOp)
                {
                    case NumberArithOp.Divide:
                    case NumberArithOp.Modulo:
                        return true;
                    case NumberArithOp.Add:
                    case NumberArithOp.BitAnd:
                    case NumberArithOp.BitOr:
                    case NumberArithOp.BitXor:
                    case NumberArithOp.Multiply:
                    case NumberArithOp.ShiftLeft:
                    case NumberArithOp.ShiftRight:
                    case NumberArithOp.Subtract:
                        return false;
                    default:
                        throw new Exception();
                }
            }
        }
    }
}
