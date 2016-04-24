using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class CallVirtualMethodInstruction : HighInstruction, IMethodReferencingInstruction
    {
        private MethodSpecTag m_methodSpec;
        private HighSsaRegister m_returnDestReg;
        private HighSsaRegister m_instanceReg;
        private HighSsaRegister[] m_parameters;

        public override Opcodes Opcode { get { return Opcodes.CallVirtualMethod; } }

        public CallVirtualMethodInstruction(CodeLocationTag codeLocation, HighSsaRegister returnDestReg, MethodSpecTag methodSpec, HighSsaRegister instanceReg, HighSsaRegister[] parameters)
            : base(codeLocation)
        {
            m_returnDestReg = returnDestReg;
            m_methodSpec = methodSpec;
            m_instanceReg = instanceReg;
            m_parameters = parameters;
        }

        public CallVirtualMethodInstruction()
        {
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_returnDestReg);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            HighSsaRegister[] parameters = m_parameters;
            int len = parameters.Length;
            for (int i = 0; i < len; i++)
                visitor(ref parameters[i]);
            visitor(ref m_instanceReg);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexMethodSpecTag(m_methodSpec));
            writer.Write((uint)m_parameters.Length);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_methodSpec = catalog.GetMethodSpec(reader.ReadUInt32());
            m_parameters = new HighSsaRegister[reader.ReadUInt32()];
        }

        public override HighInstruction Clone()
        {
            HighSsaRegister[] parameters = ArrayCloner.Clone<HighSsaRegister>(m_parameters);
            return new CallVirtualMethodInstruction(CodeLocation, m_returnDestReg, m_methodSpec, m_instanceReg, parameters);
        }

        void IMethodReferencingInstruction.VisitMethodSpecs(VisitMethodSpecDelegate visitor)
        {
            visitor(ref m_methodSpec);
        }
    }
}
