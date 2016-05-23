using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class ReturnValueInstruction : HighInstruction
    {
        private HighSsaRegister m_value;

        public HighSsaRegister Value { get { return m_value; } }

        public ReturnValueInstruction()
        {
        }

        public ReturnValueInstruction(CodeLocationTag codeLocation, HighSsaRegister value)
            : base(codeLocation)
        {
            m_value = value;
        }

        public override Opcodes Opcode { get { return Opcodes.ReturnValue; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_value);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override HighInstruction Clone()
        {
            return new ReturnValueInstruction(CodeLocation, m_value);
        }
    }
}
