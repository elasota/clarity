using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.GeneratedMethods
{
    public class GMDelegateConvertToMulticast : MethodKey
    {
        private TypeSpecDelegateTag m_dt;
        private VTableGenerationCache m_vtCache;

        public GMDelegateConvertToMulticast(TypeSpecDelegateTag dt, VTableGenerationCache vtCache)
        {
            m_dt = dt;
            m_vtCache = vtCache;
        }

        public override bool Equals(MethodKey other)
        {
            GMDelegateConvertToMulticast tOther = other as GMDelegateConvertToMulticast;
            if (tOther == null)
                return false;

            return m_dt == tOther.m_dt;
        }

        public override RloMethod GenerateMethod(Compiler compiler, MethodInstantiationPath instantiationPath)
        {
            HighLocal[] locals = new HighLocal[1];

            locals[0] = new HighLocal(m_dt, HighLocal.ETypeOfType.Value);

            TypeSpecClassTag delegateClass = m_dt.DelegateType;

            TypeSpecMulticastDelegateTag mdgSpec = new TypeSpecMulticastDelegateTag(delegateClass);
            mdgSpec = (TypeSpecMulticastDelegateTag)compiler.TagRepository.InternTypeSpec(mdgSpec);

            HighSsaRegister thisRef = new HighSsaRegister(HighValueType.ReferenceValue, m_dt, null);
            HighSsaRegister result = new HighSsaRegister(HighValueType.ReferenceValue, mdgSpec, null);
            HighSsaRegister convertedResult = new HighSsaRegister(HighValueType.ReferenceValue, m_vtCache.GetSystemDelegateType(compiler), null);

            HighCfgNodeHandle entryHdl = new HighCfgNodeHandle();
            HighCfgNodeHandle returnResultHdl = new HighCfgNodeHandle();

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                instrs.Add(new Rpa.Instructions.LoadLocalInstruction(null, thisRef, locals[0]));
                Instructions.ConvertDelegateToMulticastInstruction convertInstr = new Instructions.ConvertDelegateToMulticastInstruction(null, result, thisRef, mdgSpec);
                convertInstr.ContinuationEdge = new HighCfgEdge(convertInstr, returnResultHdl);
                instrs.Add(convertInstr);

                entryHdl.Value = new HighCfgNode(instrs.ToArray());
            }

            {
                List<HighInstruction> instrs = new List<HighInstruction>();
                instrs.Add(new Instructions.ObjectToObjectInstruction(null, convertedResult, result));
                instrs.Add(new Rpa.Instructions.ReturnValueInstruction(null, convertedResult));

                returnResultHdl.Value = new HighCfgNode(instrs.ToArray());
            }

            compiler.GetRloVTable(mdgSpec, instantiationPath);

            HighRegion region = new HighRegion(entryHdl);
            RloMethodBody methodBody = new RloMethodBody(locals, m_vtCache.GetSystemDelegateType(compiler), region, instantiationPath);
            return new RloMethod(methodBody);
        }

        public override int GetHashCode()
        {
            return m_dt.GetHashCode();
        }

        public override void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("gm_delegateconverttomulticast(");
            m_dt.WriteDisassembly(dw);
            dw.Write(")");
        }
    }
}
