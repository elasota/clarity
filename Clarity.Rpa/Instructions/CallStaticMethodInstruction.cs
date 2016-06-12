using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class CallStaticMethodInstruction : HighInstruction, IMethodReferencingInstruction
    {
        private MethodSpecTag m_methodSpec;
        private HighSsaRegister[] m_parameters;
        private HighSsaRegister m_returnDestReg;

        public MethodSpecTag MethodSpec { get { return m_methodSpec; } }
        public HighSsaRegister ReturnDest { get { return m_returnDestReg; } }
        public HighSsaRegister[] Parameters { get { return m_parameters; } }

        public override Opcodes Opcode { get { return Opcodes.CallStaticMethod; } }

        public CallStaticMethodInstruction(CodeLocationTag codeLocation, HighSsaRegister returnDestReg, MethodSpecTag methodSpec, HighSsaRegister[] parameters)
            : base(codeLocation)
        {
            m_returnDestReg = returnDestReg;
            m_methodSpec = methodSpec;
            m_parameters = parameters;
        }

        public CallStaticMethodInstruction()
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
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexMethodSpecTag(m_methodSpec));
            writer.Write((uint)m_parameters.Length);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            m_methodSpec.WriteDisassembly(dw);
            dw.Write(" ");
            dw.Write(m_parameters.Length.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_methodSpec = catalog.GetMethodSpec(reader.ReadUInt32());
            m_parameters = new HighSsaRegister[reader.ReadUInt32()];
        }

        protected override HighInstruction CloneImpl()
        {
            HighSsaRegister[] parameters = ArrayCloner.Clone<HighSsaRegister>(m_parameters);
            return new CallStaticMethodInstruction(CodeLocation, m_returnDestReg, m_methodSpec, parameters);
        }

        void IMethodReferencingInstruction.VisitMethodSpecs(VisitMethodSpecDelegate visitor)
        {
            visitor(ref m_methodSpec);
        }

        public override bool MayThrow { get { return true; } }
    }
}
