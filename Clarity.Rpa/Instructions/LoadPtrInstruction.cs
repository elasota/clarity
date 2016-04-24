using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class LoadPtrInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;

        public LoadPtrInstruction()
        {
        }

        public LoadPtrInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
        }

        public override Opcodes Opcode { get { return Opcodes.LoadPtr; } }

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

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
        }

        public override HighInstruction Clone()
        {
            return new LoadPtrInstruction(CodeLocation, m_dest, m_src);
        }
    }
}
