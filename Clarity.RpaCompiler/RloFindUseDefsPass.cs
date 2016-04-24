using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class RloFindUseDefsPass : RloPerNodePass
    {
        private Dictionary<HighSsaRegister, HashSet<ISsaUser>> m_uses;
        private Dictionary<HighSsaRegister, HashSet<ISsaEmitter>> m_defs;
        private HighInstruction.VisitSsaDelegate m_useVisitorDelegate;
        private HighInstruction.VisitSsaDelegate m_defVisitorDelegate;

        private ISsaEmitter m_currentEmitter;
        private ISsaUser m_currentUser;
        private RloUseDefCatalog m_useDefCatalog;

        public RloUseDefCatalog Result { get { return m_useDefCatalog; } }

        public RloFindUseDefsPass(Compiler compiler, RloMethodBody methodBody)
            : base(compiler, methodBody)
        {
            m_uses = new Dictionary<HighSsaRegister, HashSet<ISsaUser>>();
            m_defs = new Dictionary<HighSsaRegister, HashSet<ISsaEmitter>>();
            m_useVisitorDelegate = this.VisitForUses;
            m_defVisitorDelegate = this.VisitForDefs;
        }

        private void VisitForDefs(ref HighSsaRegister highSsaReg)
        {
            HighSsaRegister reg = highSsaReg;

            if (reg == null)
                return;

            HashSet<ISsaEmitter> emitterSet;
            if (!m_defs.TryGetValue(reg, out emitterSet))
            {
                emitterSet = new HashSet<ISsaEmitter>();
                m_defs.Add(reg, emitterSet);
            }
            emitterSet.Add(m_currentEmitter);
        }

        private void VisitForUses(ref HighSsaRegister highSsaReg)
        {
            HighSsaRegister reg = highSsaReg;

            HashSet<ISsaUser> userSet;
            if (!m_uses.TryGetValue(reg, out userSet))
            {
                userSet = new HashSet<ISsaUser>();
                m_uses.Add(reg, userSet);
            }
            userSet.Add(m_currentUser);
        }

        protected override void ProcessNode(HighCfgNode cfgNode)
        {
            foreach (HighPhi phi in cfgNode.Phis)
            {
                m_currentEmitter = phi;
                phi.VisitSsaDests(m_defVisitorDelegate);
                foreach (HighPhiLink link in phi.Links)
                {
                    m_currentUser = link;
                    link.VisitSsaUses(m_useVisitorDelegate);
                }
            }
            foreach (HighInstruction instr in cfgNode.Instructions)
            {
                m_currentEmitter = instr;
                m_currentUser = instr;
                instr.VisitSsaDests(m_defVisitorDelegate);
                instr.VisitSsaUses(m_useVisitorDelegate);
            }
        }

        protected override void FinalizePass()
        {
            m_useDefCatalog = new RloUseDefCatalog(m_uses, m_defs);
        }
    }
}
