using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class ThrowInstruction : HighInstruction
    {
        private HighSsaRegister m_exception;

        public ThrowInstruction()
        {
        }

        public ThrowInstruction(CodeLocationTag codeLocation, HighSsaRegister exception)
            : base(codeLocation)
        {
            m_exception = exception;
        }

        public override Opcodes Opcode { get { return Opcodes.Throw; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_exception);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override HighInstruction Clone()
        {
            return new ThrowInstruction(CodeLocation, m_exception);
        }
    }
}
