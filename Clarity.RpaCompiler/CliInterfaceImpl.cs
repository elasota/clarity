
using System;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class CliInterfaceImpl
    {
        private TypeSpecClassTag m_interface;
        private CliInterfaceImplSlot[] m_ifcSlotToClassVtableSlot;

        public TypeSpecClassTag Interface { get { return m_interface; } }
        public CliInterfaceImplSlot[] IfcSlotToClassVtableSlot { get { return m_ifcSlotToClassVtableSlot; } }

        public CliInterfaceImpl(TypeSpecClassTag ifc, CliInterfaceImplSlot[] ifcSlotToClassVtableSlot)
        {
            m_interface = ifc;
            m_ifcSlotToClassVtableSlot = ifcSlotToClassVtableSlot;
        }

        private CliInterfaceImpl(CliInterfaceImpl baseImpl, Compiler compiler, TypeSpecTag[] argTypes)
        {
            m_interface = (TypeSpecClassTag)baseImpl.m_interface.Instantiate(compiler.TagRepository, argTypes);
            m_ifcSlotToClassVtableSlot = baseImpl.m_ifcSlotToClassVtableSlot;
        }

        public CliInterfaceImpl Instantiate(Compiler compiler, TypeSpecTag[] argTypes)
        {
            return new CliInterfaceImpl(this, compiler, argTypes);
        }
    }
}
