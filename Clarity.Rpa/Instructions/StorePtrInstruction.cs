﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class StorePtrInstruction : HighInstruction
    {
        private HighSsaRegister m_ptr;
        private HighSsaRegister m_value;

        public HighSsaRegister Ptr { get { return m_ptr; } }
        public HighSsaRegister Value { get { return m_value; } }

        public StorePtrInstruction()
        {
        }

        public StorePtrInstruction(CodeLocationTag codeLocation, HighSsaRegister ptr, HighSsaRegister value)
            : base(codeLocation)
        {
            m_ptr = ptr;
            m_value = value;
        }

        public override Opcodes Opcode { get { return Opcodes.StorePtr; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_ptr);
            visitor(ref m_value);
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
            return new StorePtrInstruction(CodeLocation, m_ptr, m_value);
        }

        public override bool MayThrow { get { return false; } }
    }
}
