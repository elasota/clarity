using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public sealed class CatchOrRouteInstruction : HighInstruction
    {
        private HighSsaRegister m_routeDest;
        private HighSsaRegister m_exceptionDest;

        public HighSsaRegister RouteDest { get { return m_routeDest; } }
        public HighSsaRegister ExceptionDest { get { return m_exceptionDest; } }

        public override Opcodes Opcode { get { return Opcodes.CatchOrRoute; } }

        public CatchOrRouteInstruction()
        {
        }

        public CatchOrRouteInstruction(CodeLocationTag codeLocation, HighSsaRegister routeDest, HighSsaRegister exceptionDest)
            : base(codeLocation)
        {
            m_routeDest = routeDest;
            m_exceptionDest = exceptionDest;
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_exceptionDest);
            visitor(ref m_routeDest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
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
            return new CatchOrRouteInstruction(this.CodeLocation, m_routeDest, m_exceptionDest);
        }
    }
}
