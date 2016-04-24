using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloUseDefCatalog
    {
        private Dictionary<HighSsaRegister, HashSet<ISsaUser>> m_uses;
        private Dictionary<HighSsaRegister, HashSet<ISsaEmitter>> m_defs;

        public RloUseDefCatalog(Dictionary<HighSsaRegister, HashSet<ISsaUser>> uses, Dictionary<HighSsaRegister, HashSet<ISsaEmitter>> defs)
        {
            m_uses = uses;
            m_defs = defs;
        }

        private IEnumerable<ISsaUser> UsesForSsaReg(HighSsaRegister reg)
        {
            HashSet<ISsaUser> result;
            if (m_uses.TryGetValue(reg, out result))
                return result;
            return new ISsaUser[0];
        }

        private IEnumerable<ISsaEmitter> DefsForSsaReg(HighSsaRegister reg)
        {
            HashSet<ISsaEmitter> result;
            if (m_defs.TryGetValue(reg, out result))
                return result;
            return new ISsaEmitter[0];
        }
    }
}
