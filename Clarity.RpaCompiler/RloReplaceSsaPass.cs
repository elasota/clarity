using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    // This pass canonicalizes all SSA value types to ValueValue if they're natively value type,
    // ReferenceValue if they're natively reference type, and BoxedValue if they're boxed.
    // It should be run early, before cross-node SSA passes.
    public class RloReplaceSsaPass : RloPerNodePass
    {
        private IDictionary<HighSsaRegister, HighSsaRegister> m_dict;
        private HighInstruction.VisitSsaDelegate m_visitor;

        public RloReplaceSsaPass(Compiler compiler, RloMethodBody methodBody, IDictionary<HighSsaRegister, HighSsaRegister> replacements)
            : base(compiler, methodBody)
        {
            m_dict = replacements;
            m_visitor = VisitSsa;
        }

        private void VisitSsa(ref HighSsaRegister highSsaReg)
        {
            HighSsaRegister reg = highSsaReg;
            if (reg != null && m_dict.TryGetValue(highSsaReg, out reg))
                highSsaReg = reg;
        }

        protected override void ProcessNode(HighCfgNode cfgNode)
        {
            foreach (HighPhi phi in cfgNode.Phis)
            {
                phi.VisitSsaDests(m_visitor);
                phi.VisitSsaUses(m_visitor);
            }
            foreach (HighInstruction instr in cfgNode.Instructions)
            {
                instr.VisitSsaDests(m_visitor);
                instr.VisitSsaUses(m_visitor);
            }
        }
    }
}
