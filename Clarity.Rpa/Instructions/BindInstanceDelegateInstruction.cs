using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public class BindInstanceDelegateInstruction : HighInstruction, IMethodReferencingInstruction
    {
        private HighSsaRegister m_dest;
        private HighSsaRegister m_object;
        private MethodSpecTag m_methodSpec;

        public BindInstanceDelegateInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister obj, MethodSpecTag methodSpec)
            : base(codeLocation)
        {
            m_dest = dest;
            m_object = obj;
            m_methodSpec = methodSpec;
        }

        public BindInstanceDelegateInstruction()
        {
        }

        public override Opcodes Opcode { get { return Opcodes.BindInstanceDelegate; } }

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
            writer.Write(fileBuilder.IndexMethodSpecTag(m_methodSpec));
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_methodSpec = catalog.GetMethodSpec(reader.ReadUInt32());
        }

        public override HighInstruction Clone()
        {
            return new BindInstanceDelegateInstruction(CodeLocation, m_dest, m_object, m_methodSpec);
        }

        void IMethodReferencingInstruction.VisitMethodSpecs(VisitMethodSpecDelegate visitor)
        {
            visitor(ref m_methodSpec);
        }
    }
}
