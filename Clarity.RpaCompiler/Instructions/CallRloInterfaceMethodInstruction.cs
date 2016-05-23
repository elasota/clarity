using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class CallRloInterfaceMethodInstruction : HighInstruction
    {
        private uint m_vtableSlotIndex;
        private HighSsaRegister m_instanceSrc;
        private HighSsaRegister[] m_parameters;
        private HighSsaRegister m_returnDest;

        public override Opcodes Opcode { get { return Opcodes.CallRloInterfaceMethod; } }

        public CallRloInterfaceMethodInstruction(CodeLocationTag codeLocation, uint vtableSlotIndex, HighSsaRegister returnDest, HighSsaRegister instanceSrc, HighSsaRegister[] parameters)
        {
            CodeLocation = codeLocation;
            m_vtableSlotIndex = vtableSlotIndex;
            m_returnDest = returnDest;
            m_instanceSrc = instanceSrc;
            m_parameters = parameters;
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_returnDest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_instanceSrc);
            HighSsaRegister[] parameters = m_parameters;
            for (int i = 0; i < parameters.Length; i++)
                visitor(ref parameters[i]);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public override HighInstruction Clone()
        {
            return new CallRloVirtualMethodInstruction(this.CodeLocation, m_vtableSlotIndex, m_returnDest, m_instanceSrc, m_parameters);
        }
    }
}