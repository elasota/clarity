using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa.Instructions
{
    public sealed class LeaveRegionInstruction : HighInstruction
    {
        private uint m_routeID;

        public uint RouteID { get { return m_routeID; } }

        public LeaveRegionInstruction()
        {
        }

        public LeaveRegionInstruction(CodeLocationTag codeLocation, uint routeID)
            : base(codeLocation)
        {
            m_routeID = routeID;
        }

        public override Opcodes Opcode { get { return Opcodes.LeaveRegion; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(m_routeID);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_routeID.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_routeID = reader.ReadUInt32();
        }

        protected override HighInstruction CloneImpl()
        {
            return new LeaveRegionInstruction(CodeLocation, m_routeID);
        }

        public override bool MayThrow { get { return false; } }
    }
}
