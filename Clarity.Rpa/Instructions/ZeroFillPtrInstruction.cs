using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class ZeroFillPtrInstruction : HighInstruction
    {
        private HighSsaRegister m_target;

        public HighSsaRegister Target { get { return m_target; } }

        public ZeroFillPtrInstruction()
        {
        }

        public ZeroFillPtrInstruction(CodeLocationTag codeLocation, HighSsaRegister target)
            : base(codeLocation)
        {
            m_target = target;
        }

        public override Opcodes Opcode { get { return Opcodes.ZeroFillPtr; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_target);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override HighInstruction Clone()
        {
            return new ZeroFillPtrInstruction(CodeLocation, m_target);
        }
    }
}
