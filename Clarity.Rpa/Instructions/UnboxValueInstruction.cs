﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class UnboxValueInstruction : HighInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }

        public UnboxValueInstruction()
        {
        }

        public UnboxValueInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
        }

        public override Opcodes Opcode { get { return Opcodes.UnboxValue; } }

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
            return new UnboxValueInstruction(CodeLocation, m_dest, m_src);
        }
    }
}
