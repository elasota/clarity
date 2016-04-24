﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa.Instructions
{
    public sealed class AllocObjInstruction : HighInstruction, ITypeReferencingInstruction
    {
        private TypeSpecTag m_type;
        private HighSsaRegister m_dest;

        public TypeSpecTag Type { get { return m_type; } }
        public HighSsaRegister Dest { get { return m_dest; } }

        public override Opcodes Opcode { get { return Opcodes.AllocObj; } }

        public AllocObjInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, TypeSpecTag type)
            : base(codeLocation)
        {
            m_dest = dest;
            m_type = type;
        }

        public AllocObjInstruction()
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
            writer.Write(fileBuilder.IndexTypeSpecTag(m_type));
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_type = catalog.GetTypeSpec(reader.ReadUInt32());
        }

        public override HighInstruction Clone()
        {
            return new AllocObjInstruction(CodeLocation, m_dest, m_type);
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }
    }
}