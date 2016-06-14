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
        public HighLocal InstanceLocal { get { return m_instanceLocal; } }
        public HighLocal[] Locals2 { get { return m_locals; } }
        public HighLocal[] Args { get { return m_args; } }
        public TypeSpecTag ReturnType { get { return m_returnType; } }

        private Dictionary<HighSsaRegister, HighSsaRegister> m_translatedSsaRegs = new Dictionary<HighSsaRegister, HighSsaRegister>();
        private Dictionary<HighLocal, HighLocal> m_translatedLocals = new Dictionary<HighLocal, HighLocal>();

        private TagRepository m_tagRepo;
        private RloInstantiationParameters m_instParams;
        private HighLocal m_instanceLocal;
        private HighLocal[] m_locals;
        private HighLocal[] m_args;
        private TypeSpecTag m_returnType;

        public RloMethodConverter(TagRepository tagRepo, RloInstantiationParameters instParams, TypeSpecTag returnType, HighLocal instanceLocal, HighLocal[] args, HighLocal[] locals)
        {
            m_tagRepo = tagRepo;
            m_instParams = instParams;

            m_returnType = InstantiateType(returnType);

            List<HighLocal> mergedLocals = new List<HighLocal>();
            if (instanceLocal != null)
                mergedLocals.Add(instanceLocal);
            mergedLocals.AddRange(args);
            mergedLocals.AddRange(locals);

            foreach (HighLocal local in mergedLocals)
            {
                HighLocal newLocal = new HighLocal(this.InstantiateType(local.Type), local.TypeOfType);
                m_translatedLocals.Add(local, newLocal);
            }

            if (instanceLocal != null)
                m_instanceLocal = m_translatedLocals[instanceLocal];

            List<HighLocal> newArgs = new List<HighLocal>();
            foreach (HighLocal arg in args)
                newArgs.Add(m_translatedLocals[arg]);

            List<HighLocal> newLocals = new List<HighLocal>();
            foreach (HighLocal local in locals)
                newLocals.Add(m_translatedLocals[local]);

            m_args = newArgs.ToArray();
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

        public MethodSpecTag InstantiateMethodSpec(MethodSpecTag methodSpec)
        {
            if (m_instParams != null)
                return methodSpec.Instantiate(m_tagRepo, m_instParams.TypeParams, m_instParams.MethodParams);
            return methodSpec;
        }
    }
}
