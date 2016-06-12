using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class CallRloInstanceMethodInstruction : HighInstruction
    {
        private HighSsaRegister m_returnDest;
        private HighSsaRegister m_instanceSrc;
        private MethodHandle m_methodHandle;
        private HighSsaRegister[] m_parameters;

        public CallRloInstanceMethodInstruction(CodeLocationTag codeLocation, MethodHandle methodHandle, HighSsaRegister returnDest, HighSsaRegister instanceSrc, HighSsaRegister[] parameters)
        {
            this.CodeLocation = codeLocation;
            m_methodHandle = methodHandle;
            m_instanceSrc = instanceSrc;
            m_parameters = parameters;
            m_returnDest = returnDest;
        }

        public override Opcodes Opcode { get { return Opcodes.CallRloInstanceMethod; } }

        protected override HighInstruction CloneImpl()
        {
            return new CallRloInstanceMethodInstruction(this.CodeLocation, m_methodHandle, m_returnDest, m_instanceSrc, m_parameters);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_returnDest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_instanceSrc);
            HighSsaRegister[] parameters = this.m_parameters;
            for (int i = 0; i < parameters.Length; i++)
                visitor(ref parameters[i]);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.WriteMethodHandleKey(m_methodHandle);
            dw.Write(" ");
            dw.Write(m_parameters.Length.ToString());
        }

        public override bool MayThrow { get { return true; } }
    }
}