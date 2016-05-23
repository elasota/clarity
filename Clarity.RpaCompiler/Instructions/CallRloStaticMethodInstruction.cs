using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    internal class CallRloStaticMethodInstruction : HighInstruction
    {
        private MethodHandle m_methodHandle;
        private HighSsaRegister[] m_parameters;
        private HighSsaRegister m_returnDest;

        public CallRloStaticMethodInstruction(CodeLocationTag codeLocation, MethodHandle methodHandle, HighSsaRegister returnDest, HighSsaRegister[] parameters)
        {
            CodeLocation = codeLocation;
            m_methodHandle = methodHandle;
            m_returnDest = returnDest;
            m_parameters = parameters;
        }

        public override Opcodes Opcode { get { return Opcodes.CallRloStaticMethod; } }

        public override HighInstruction Clone()
        {
            return new CallRloStaticMethodInstruction(this.CodeLocation, m_methodHandle, m_returnDest, m_parameters);
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
            HighSsaRegister[] parameters = m_parameters;
            for (int i = 0; i < parameters.Length; i++)
                visitor(ref parameters[i]);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}