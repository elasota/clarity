using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class CliInterface
    {
        private HighTypeDef m_typeDef;
        private TypeSpecTag[] m_genericParameters;
        private HighClassVtableSlot[] m_slots;
        private Dictionary<MethodDeclTag, uint> m_slotTagToCliSlot;
        private Dictionary<MethodDeclTag, uint> m_slotTagToRealSlot;
        private uint m_numRealSlots;    // Excluding fake slots from virtual generics

        public HighClassVtableSlot[] Slots { get { return m_slots; } }

        public uint CliSlotForSlotTag(MethodDeclTag methodDecl)
        {
            uint result;
            if (!m_slotTagToCliSlot.TryGetValue(methodDecl, out result))
                throw new Exception("Couldn't find method decl in interface");
            return result;
        }

        public CliInterface(Compiler compiler, HighTypeDef typeDef)
        {
            m_typeDef = typeDef;
            m_slots = typeDef.NewSlots;
            m_slotTagToCliSlot = new Dictionary<MethodDeclTag, uint>();
            m_slotTagToRealSlot = new Dictionary<MethodDeclTag, uint>();

            uint numGenericParameters = typeDef.NumGenericParameters;
            TypeSpecTag[] genericParameters = new TypeSpecTag[numGenericParameters];
            for (uint i = 0; i < numGenericParameters; i++)
            {
                TypeSpecGenericParamTypeTag paramType = new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.Var);
                TypeSpecTag paramTag = new TypeSpecGenericParamTag(paramType, i);
                paramTag = compiler.TagRepository.InternTypeSpec(paramTag);

                genericParameters[i] = paramTag;
            }

            m_numRealSlots = 0;
            uint cliSlot = 0;
            foreach (HighClassVtableSlot slot in m_slots)
            {
                if (m_slotTagToCliSlot.ContainsKey(slot.SlotTag))
                    throw new Exception("Duplicate interface vtable slot");

                m_slotTagToCliSlot.Add(slot.SlotTag, cliSlot++);
                if (slot.Signature.NumGenericParameters > 0)
                    m_slotTagToRealSlot.Add(slot.SlotTag, m_numRealSlots);
            }
        }
        
        private CliInterface(HighTypeDef typeDef, TypeSpecTag[] genericParameters, HighClassVtableSlot[] slots,
            Dictionary<MethodDeclTag, uint> slotTagToCliSlot, Dictionary<MethodDeclTag, uint> slotTagToRealSlot, uint numRealSlots)
        {
            m_typeDef = typeDef;
            m_genericParameters = genericParameters;
            m_slots = slots;
            m_slotTagToCliSlot = slotTagToCliSlot;
            m_slotTagToRealSlot = slotTagToRealSlot;
            m_numRealSlots = numRealSlots;
        }

        public CliInterface Instantiate(Compiler compiler, TypeSpecTag[] argTypes)
        {
            if (m_typeDef.NumGenericParameters != (uint)argTypes.Length)
                throw new ArgumentException("Interface parameter count doesn't match typedef count");

            if (m_typeDef.NumGenericParameters == 0)
                return this;

            List<HighClassVtableSlot> slots = new List<HighClassVtableSlot>();
            foreach (HighClassVtableSlot slot in m_slots)
            {
                MethodSignatureTag signature = slot.Signature.Instantiate(compiler.TagRepository, argTypes);
                HighClassVtableSlot newSlot = new HighClassVtableSlot(slot.SlotTag, signature, null, true, false);
                slots.Add(newSlot);
            }

            return new CliInterface(m_typeDef, argTypes, slots.ToArray(), m_slotTagToCliSlot, m_slotTagToRealSlot, m_numRealSlots);
        }
    }
}
