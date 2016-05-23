using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public sealed class RloConvertNumberInstruction : HighInstruction
    {
        public enum NumConversionType
        {
            ZeroExtend,
            SignExtend,

            ZeroTruncate,
            ZeroTruncate_Checked,
            SignTruncate,
            SignTruncate_Checked,

            FloatToInt,
            FloatToInt_Checked,
            FloatToUInt,
            FloatToUInt_Checked,

            FloatToFloat,
            IntToFloat,
            UIntToFloat,

            Count
        }

        private NumConversionType m_conversionType;
        private uint m_destPrecision;
        private uint m_sourcePrecision;
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;

        public NumConversionType ConversionType { get { return m_conversionType; } }
        public uint DestPrecision { get { return m_destPrecision; } }
        public uint SrcPrecision { get { return m_sourcePrecision; } }
        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }

        public override Opcodes Opcode { get { return Opcodes.RloConvertNumber; } }

        public RloConvertNumberInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, NumConversionType conversionType, uint destPrecision, uint sourcePrecision)
            : base(codeLocation)
        {
            m_conversionType = conversionType;
            m_dest = dest;
            m_src = src;
            m_destPrecision = destPrecision;
            m_sourcePrecision = sourcePrecision;
        }

        public override HighInstruction Clone()
        {
            return new RloConvertNumberInstruction(this.CodeLocation, m_dest, m_src, m_conversionType, m_destPrecision, m_sourcePrecision);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_conversionType = (NumConversionType)reader.ReadByte();
            m_destPrecision = reader.ReadUInt32();
            m_sourcePrecision = reader.ReadUInt32();
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
            writer.Write((byte)m_conversionType);
            writer.Write(m_destPrecision);
            writer.Write(m_sourcePrecision);
        }
    }
}
