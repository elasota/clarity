using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class AllocArrayInstruction : HighInstruction, IExtractableTypesInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister[] m_sizes;
        private TypeSpecTag m_type;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister[] Sizes { get { return m_sizes; } }
        public TypeSpecTag TargetType { get { return m_type; } }

        public AllocArrayInstruction()
        {
        }

        public AllocArrayInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister[] sizes, TypeSpecTag targetType)
            : base(codeLocation)
        {
            m_dest = dest;
            m_sizes = sizes;
            m_type = targetType;
        }

        public override Opcodes Opcode { get { return Opcodes.AllocArray; } }

        protected override HighInstruction CloneImpl()
        {
            return new AllocArrayInstruction(CodeLocation, m_dest, ArrayCloner.Clone<HighSsaRegister>(m_sizes), m_type);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_sizes = new HighSsaRegister[reader.ReadUInt32()];
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            HighSsaRegister[] indexes = m_sizes;
            int len = m_sizes.Length;
            for (int i = 0; i < len; i++)
                visitor(ref indexes[i]);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((uint)m_sizes.Length);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_sizes.Length.ToString());
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }

        void IExtractableTypesInstruction.ExtractSsaTypes()
        {
            m_type = m_dest.Type;
        }

        public override bool MayThrow { get { return true; } }
    }
}
