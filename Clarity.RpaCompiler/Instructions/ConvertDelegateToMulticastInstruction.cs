using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class ConvertDelegateToMulticastInstruction : HighInstruction, ITypeReferencingInstruction
    {
        private TypeSpecTag m_mdType;
        private HighSsaRegister m_dest;
        private HighSsaRegister m_src;

        public HighSsaRegister Dest { get { return m_dest; } }
        public HighSsaRegister Src { get { return m_src; } }
        public TypeSpecTag MulticastDelegateType { get { return m_mdType; } }

        public override Opcodes Opcode { get { return Opcodes.ConvertDelegateToMulticast; } }

        public ConvertDelegateToMulticastInstruction()
        {
        }

        public ConvertDelegateToMulticastInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, TypeSpecMulticastDelegateTag mdgSpec)
            : base(codeLocation)
        {
            m_dest = dest;
            m_src = src;
            m_mdType = mdgSpec;
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
            throw new NotImplementedException();
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            m_mdType.WriteDisassembly(dw);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        protected override HighInstruction CloneImpl()
        {
            return new ConvertDelegateToMulticastInstruction(this.CodeLocation, m_dest, m_src, (TypeSpecMulticastDelegateTag)m_mdType);
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_mdType);
        }

        public override bool MayThrow { get { return true; } }
    }
}
