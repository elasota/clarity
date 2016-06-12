using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class PtrFieldInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;
        private string m_field;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public string FieldName { get { return m_field; } }

        public override Opcodes Opcode { get { return Opcodes.PtrField; } }

        public PtrFieldInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, string field)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_field = field;
        }

        public PtrFieldInstruction()
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
            writer.Write(fileBuilder.IndexString(m_field));
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.WriteToken(m_field);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_field = catalog.GetString(reader.ReadUInt32());
        }

        protected override HighInstruction CloneImpl()
        {
            return new PtrFieldInstruction(CodeLocation, m_dest, m_src, m_field);
        }

        public override bool MayThrow { get { return false; } }
    }
}
