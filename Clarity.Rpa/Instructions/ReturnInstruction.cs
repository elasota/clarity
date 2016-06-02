using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class ReturnInstruction : HighInstruction
    {
        public ReturnInstruction()
        {
        }

        public ReturnInstruction(CodeLocationTag codeLocation)
            : base(codeLocation)
        {
        }

        public override Opcodes Opcode { get { return Opcodes.Return; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override HighInstruction Clone()
        {
            return new ReturnInstruction(this.CodeLocation);
        }

        public override bool MayThrow { get { return false; } }
    }
}
