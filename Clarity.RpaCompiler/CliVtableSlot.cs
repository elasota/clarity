using System;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public struct CliVtableSlot
    {
        private CliMethodIndex m_methodIndex;
        private bool m_isSealed;
        private MethodSignatureTag m_methodSignature;

        public bool IsSealed { get { return m_isSealed; } }
        public CliMethodIndex MethodIndex { get { return m_methodIndex; } }
        public MethodSignatureTag MethodSignature { get { return m_methodSignature; } }

        public CliVtableSlot(CliMethodIndex methodIndex, MethodSignatureTag methodSignature, bool isSealed)
        {
            m_methodIndex = methodIndex;
            m_isSealed = isSealed;
            m_methodSignature = methodSignature;
        }

        public CliVtableSlot(CliVtableSlot baseSlot, Compiler compiler, TypeSpecTag[] argTypes)
        {
            m_methodIndex = baseSlot.m_methodIndex;
            m_isSealed = baseSlot.m_isSealed;
            m_methodSignature = baseSlot.m_methodSignature.Instantiate(compiler.TagRepository, argTypes);
        }

        public CliVtableSlot Instantiate(Compiler compiler, TypeSpecTag[] argTypes)
        {
            return new CliVtableSlot(this, compiler, argTypes);
        }
    }
}
