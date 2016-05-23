using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa.Instructions
{
    // The only thing that a CatchInstruction does is return the current exception
    public class CatchInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;

        public HighSsaRegister Dest { get { return m_dest; } }

        public override Opcodes Opcode { get { return Opcodes.Catch; } }

        public CatchInstruction()
        {
        }

        public CatchInstruction(CodeLocationTag codeLocation, HighSsaRegister dest)
            : base(codeLocation)
        {
            m_dest = dest;
        }

        public override HighInstruction Clone()
        {
            return new CatchInstruction(CodeLocation, m_dest);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }
    }
}
