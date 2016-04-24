using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.Rpa
{
    public class HighCfgNode
    {
        private HighPhi[] m_phis;
        private HighInstruction[] m_instructions;

        public HighPhi[] Phis { get { return m_phis; } }
        public HighInstruction[] Instructions { get { return m_instructions; } }

        public HighCfgNode(HighPhi[] phis, HighInstruction[] instructions)
        {
            m_phis = phis;
            m_instructions = instructions;
        }

        public void Write(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            HighCfgNodeBuilder cfgNodeBuilder = new HighCfgNodeBuilder();

            HashSet<HighSsaRegister> gatheredConstants = new HashSet<HighSsaRegister>();
            List<HighSsaRegister> constants = new List<HighSsaRegister>();

            HighInstruction.VisitSsaDelegate constVisitor = delegate (ref HighSsaRegister use)
            {
                if (use.IsConstant && !gatheredConstants.Contains(use))
                {
                    gatheredConstants.Add(use);
                    constants.Add(use);
                }
            };

            foreach (HighInstruction instr in m_instructions)
                instr.VisitSsaUses(constVisitor);

            writer.Write((uint)m_phis.Length);
            writer.Write((uint)m_instructions.Length);
            writer.Write((uint)constants.Count);

            foreach (HighPhi phi in m_phis)
            {
                cfgNodeBuilder.AddSsa(phi.Dest);
                phi.Write(fileBuilder, regionBuilder, writer);
            }

            foreach (HighSsaRegister constant in constants)
            {
                cfgNodeBuilder.AddSsa(constant);
                constant.WriteConstant(fileBuilder, regionBuilder, writer);
            }

            foreach (HighInstruction instr in m_instructions)
                instr.Write(fileBuilder, methodBuilder, regionBuilder, cfgNodeBuilder, haveDebugInfo, writer);
        }

        public static HighCfgNode Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            uint numPhis = reader.ReadUInt32();
            uint numInstructions = reader.ReadUInt32();
            uint numConstants = reader.ReadUInt32();

            List<HighPhi> phis = new List<HighPhi>();
            List<HighInstruction> instructions = new List<HighInstruction>();

            List<HighSsaRegister> ssaRegisters = new List<HighSsaRegister>();

            // WARNING: SSA indexes from phis and constants must match CollectRegsFromNode

            for (uint i = 0; i < numPhis; i++)
            {
                HighPhi phi = HighPhi.Read(rpa, catalog, methodBody, cfgNodes, reader);
                phis.Add(phi);

                ssaRegisters.Add(phi.Dest);
            }

            for (uint i = 0; i < numConstants; i++)
            {
                HighSsaRegister constant = HighSsaRegister.ReadConstant(rpa, catalog, reader);
                ssaRegisters.Add(constant);
            }

            for (uint i = 0; i < numInstructions; i++)
            {
                HighInstruction instr = HighInstruction.Read(rpa, catalog, methodBody, cfgNodes, ssaRegisters, baseLocation, haveDebugInfo, reader);
                instructions.Add(instr);
            }

            return new HighCfgNode(phis.ToArray(), instructions.ToArray());
        }

        public void SetAll(HighPhi[] phis, HighInstruction[] instrs)
        {
            m_instructions = instrs;
            m_phis = phis;
        }
    }
}
