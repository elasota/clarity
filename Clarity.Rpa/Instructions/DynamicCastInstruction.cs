using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class DynamicCastInstruction : HighInstruction, IExtractableTypesInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;
        private TypeSpecTag m_type;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public TypeSpecTag TargetType { get { return m_type; } }

        public DynamicCastInstruction()
        {
        }

        public DynamicCastInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, TypeSpecTag type)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_type = type;
        }

        public override Opcodes Opcode { get { return Opcodes.DynamicCast; } }

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
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        protected override HighInstruction CloneImpl()
        {
            return new DynamicCastInstruction(CodeLocation, m_dest, m_src, m_type);
        }

        public void VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }

        void IExtractableTypesInstruction.ExtractSsaTypes()
        {
            m_type = m_dest.Type;
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }

        public override bool MayThrow { get { return false; } }
    }
}
