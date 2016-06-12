using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class CompareNumbersInstruction : HighInstruction
    {
        private NumberCompareOperation m_operation;
        private int m_trueValue;
        private int m_falseValue;
        private HighSsaRegister m_dest;
        private HighSsaRegister m_left;
        private HighSsaRegister m_right;
        private NumberArithType m_numberType;

        public NumberCompareOperation Operation { get { return m_operation; } }
        public int TrueValue { get { return m_trueValue; } }
        public int FalseValue { get { return m_falseValue; } }
        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Left { get { return m_left; } }
        public HighSsaRegister Right { get { return m_right; } }
        public NumberArithType NumberType { get { return m_numberType; } }

        public override Opcodes Opcode { get { return Opcodes.CompareNumbers; } }

        public CompareNumbersInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, NumberCompareOperation operation, NumberArithType numberType, HighSsaRegister left, HighSsaRegister right, int trueValue, int falseValue)
            : base(codeLocation)
        {
            m_dest = dest;
            m_operation = operation;
            m_trueValue = trueValue;
            m_falseValue = falseValue;
            m_left = left;
            m_right = right;
            m_numberType = numberType;
        }

        public CompareNumbersInstruction()
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
            writer.Write((byte)m_operation);
            writer.Write((byte)m_numberType);
            writer.Write(m_trueValue);
            writer.Write(m_falseValue);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_operation.ToString());
            dw.Write(" ");
            dw.Write(m_numberType.ToString());
            dw.Write(" ");
            dw.Write(m_trueValue.ToString());
            dw.Write(" ");
            dw.Write(m_falseValue.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_operation = (NumberCompareOperation)reader.ReadByte();
            if (m_operation < 0 || m_operation >= NumberCompareOperation.NumHighCompareTypes)
                throw new Exception("Invalid compare operation");
            m_numberType = (NumberArithType)reader.ReadByte();
            if (m_numberType < 0 || m_numberType >= NumberArithType.NumHighArithTypes)
                throw new Exception("Invalid arithmetic type");
            m_trueValue = reader.ReadInt32();
            m_falseValue = reader.ReadInt32();
        }

        protected override HighInstruction CloneImpl()
        {
            return new CompareNumbersInstruction(CodeLocation, m_dest, m_operation, m_numberType, m_left, m_right, m_trueValue, m_falseValue);
        }

        public override bool MayThrow { get { return false; } }
    }
}
