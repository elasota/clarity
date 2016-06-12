using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class EnterProtectedBlockInstruction : HighInstruction
    {
        private HighEHCluster m_cluster;

        public HighEHCluster EHCluster { get { return m_cluster; } }

        public EnterProtectedBlockInstruction()
        {
        }

        public EnterProtectedBlockInstruction(CodeLocationTag codeLocation, HighEHCluster cluster)
            : base(codeLocation)
        {
            m_cluster = cluster;
        }

        public override Opcodes Opcode { get { return Opcodes.EnterProtectedBlock; } }
        public override bool TerminatesControlFlow { get { return true; } }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            m_cluster.Write(fileBuilder, methodBuilder, regionBuilder, haveDebugInfo, writer);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write("ENTERPROTECTEDBLOCK");
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_cluster = HighEHCluster.Read(rpa, catalog, methodBody, cfgNodes, baseLocation, haveDebugInfo, reader);
        }

        protected override HighInstruction CloneImpl()
        {
            return new EnterProtectedBlockInstruction(CodeLocation, m_cluster);
        }

        public override bool MayThrow { get { return false; } }
    }
}
