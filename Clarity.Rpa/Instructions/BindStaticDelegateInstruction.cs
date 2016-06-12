using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public class BindStaticDelegateInstruction : HighInstruction, IMethodReferencingInstruction
    {
        private HighSsaRegister m_dest;
        private MethodSpecTag m_methodSpec;

        public HighSsaRegister Dest { get { return m_dest; } }
        public MethodSpecTag MethodSpec { get { return m_methodSpec; } }

        public BindStaticDelegateInstruction()
        {
        }

        public BindStaticDelegateInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, MethodSpecTag methodSpec)
            : base(codeLocation)
        {
            m_dest = dest;
            m_methodSpec = methodSpec;
        }

        public override Opcodes Opcode { get { return Opcodes.BindStaticDelegate; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexMethodSpecTag(m_methodSpec));
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            m_methodSpec.WriteDisassembly(dw);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_methodSpec = catalog.GetMethodSpec(reader.ReadUInt32());
        }

        protected override HighInstruction CloneImpl()
        {
            return new BindStaticDelegateInstruction(CodeLocation, m_dest, m_methodSpec);
        }

        void IMethodReferencingInstruction.VisitMethodSpecs(VisitMethodSpecDelegate visitor)
        {
            visitor(ref m_methodSpec);
        }

        public override bool MayThrow { get { return true; } }
    }
}
