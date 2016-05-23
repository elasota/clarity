using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public sealed class AllocInstanceDelegateInstruction : HighInstruction, ITypeReferencingInstruction
    {
        private TypeSpecTag m_type;
        private HighSsaRegister m_dest;
        private HighSsaRegister m_object;

        public TypeSpecTag Type { get { return m_type; } }
        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Object { get { return m_object; } }
        
        public override Opcodes Opcode { get { return Opcodes.AllocInstanceDelegate; } }

        public AllocInstanceDelegateInstruction(CodeLocationTag codeLocation, TypeSpecTag type, HighSsaRegister dest, HighSsaRegister obj)
            : base(codeLocation)
        {
            m_type = type;
            m_dest = dest;
            m_object = obj;
        }

        public override HighInstruction Clone()
        {
            return new AllocInstanceDelegateInstruction(this.CodeLocation, m_type, m_dest, m_object);
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
            visitor(ref m_object);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }
    }
}
