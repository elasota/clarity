using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class ObjectToInterfaceInstruction : HighInstruction
    {
        private HighSsaRegister m_src;
        private HighSsaRegister m_dest;

        public override Opcodes Opcode { get { return Opcodes.ObjectToInterface; } }

        public ObjectToInterfaceInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
        }

        public override HighInstruction Clone()
        {
            return new ObjectToInterfaceInstruction(this.CodeLocation, m_dest, m_src);
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
            visitor(ref m_src);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        public override bool MayThrow { get { return false; } }
    }
}
