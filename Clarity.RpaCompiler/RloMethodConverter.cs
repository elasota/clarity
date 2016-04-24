using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloMethodConverter
    {
        private Dictionary<HighSsaRegister, HighSsaRegister> m_translatedSsaRegs = new Dictionary<HighSsaRegister, HighSsaRegister>();
        private Dictionary<HighLocal, HighLocal> m_translatedLocals = new Dictionary<HighLocal, HighLocal>();

        private TagRepository m_tagRepo;
        private RloInstantiationParameters m_instParams;
        private HighLocal[] m_locals;

        public RloMethodConverter(TagRepository tagRepo, RloInstantiationParameters instParams, HighLocal[] locals)
        {
            m_tagRepo = tagRepo;
            m_instParams = instParams;

            List<HighLocal> newLocals = new List<HighLocal>();
            foreach (HighLocal local in locals)
            {
                HighLocal newLocal = new HighLocal(this.InstantiateType(local.Type), local.TypeOfType);
                m_translatedLocals.Add(local, newLocal);
                newLocals.Add(newLocal);
            }
            m_locals = newLocals.ToArray();
        }

        public HighSsaRegister GetReg(HighSsaRegister srcReg)
        {
            HighSsaRegister reg;
            if (m_translatedSsaRegs.TryGetValue(srcReg, out reg))
                return reg;

            reg = new HighSsaRegister(srcReg.ValueType, this.InstantiateType(srcReg.Type), srcReg.ConstantValue);
            m_translatedSsaRegs.Add(srcReg, reg);

            return reg;
        }

        public TypeSpecTag InstantiateType(TypeSpecTag type)
        {
            if (m_instParams != null)
                return type.Instantiate(m_tagRepo, m_instParams.TypeParams, m_instParams.MethodParams);
            return type;
        }

        public HighLocal GetLocal(HighLocal srcLocal)
        {
            return m_translatedLocals[srcLocal];
        }
    }
}
