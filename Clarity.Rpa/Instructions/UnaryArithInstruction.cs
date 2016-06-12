using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class UnaryArithInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private NumberUnaryArithOp m_arithOp;
        private NumberArithType m_arithType;
        private HighSsaRegister m_src;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public NumberUnaryArithOp ArithOp { get { return m_arithOp; } }
        public NumberArithType ArithType { get { return m_arithType; } }

        public override Opcodes Opcode { get { return Opcodes.UnaryArith; } }

        public UnaryArithInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, NumberUnaryArithOp arithOp, NumberArithType arithType, HighSsaRegister src)
            : base(codeLocation)
        {
            m_dest = dest;
            m_arithOp = arithOp;
            m_arithType = arithType;
            m_src = src;
        }

        public UnaryArithInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_src);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((byte)m_arithOp);
            writer.Write((byte)m_arithType);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_arithOp.ToString());
            dw.Write(" ");
            dw.Write(m_arithType.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_arithOp = (NumberUnaryArithOp)reader.ReadByte();
            if (m_arithOp < 0 || m_arithOp >= NumberUnaryArithOp.NumHighUnaryArithOpTypes)
                throw new Exception("Invalid unary arithmetic op");
            m_arithType = (NumberArithType)reader.ReadByte();
            if (m_arithType < 0 || m_arithType >= NumberArithType.NumHighArithTypes)
                throw new Exception("Invalid arith type");
        }

        protected override HighInstruction CloneImpl()
        {
            return new UnaryArithInstruction(CodeLocation, m_dest, m_arithOp, m_arithType, m_src);
        }

        public override bool MayThrow { get { return false; } }
    }
}
