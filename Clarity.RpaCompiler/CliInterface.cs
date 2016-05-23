using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class CliInterface : CliType
    {
        private HighTypeDef m_typeDef;
        private TypeSpecClassTag m_typeSpec;
        private TypeSpecTag[] m_genericParameters;
        private TypeSpecClassTag[] m_parentInterfaces;
        private HighClassVtableSlot[] m_slots;
        private Dictionary<MethodDeclTag, uint> m_slotTagToCliSlot;
        private Dictionary<MethodDeclTag, uint> m_slotTagToRealSlot;
        private TypeSpecClassTag[] m_interfaceImpls;
        private uint m_numRealSlots;    // Excluding fake slots from virtual generics
        private bool m_isCreated;

        public HighClassVtableSlot[] Slots { get { return m_slots; } }
        public TypeSpecClassTag TypeSpec { get { return m_typeSpec; } }
        public TypeSpecClassTag[] InterfaceImpls2 { get { return m_interfaceImpls; } }

        public bool IsCreated { get { return m_isCreated; } }

        public CliInterface()
        {
        }

        public void Initialize(HighTypeDef typeDef)
        {
            m_typeDef = typeDef;
        }

        private static void AddUniqueInterface(Dictionary<TypeSpecClassTag, int> uniqueDeps, TypeSpecClassTag ifc)
        {
            if (uniqueDeps.ContainsKey(ifc))
                return;
            uniqueDeps.Add(ifc, uniqueDeps.Count);
        }

        public bool Create(Compiler compiler)
        {
            HighTypeDef typeDef = m_typeDef;

            foreach (TypeSpecClassTag ii in typeDef.ParentInterfaces)
            {
                if (!compiler.HaveCliOpenInterface(ii.TypeName))
                    return false;
            }

            Dictionary<TypeSpecClassTag, int> uniqueDeps = new Dictionary<TypeSpecClassTag, int>();
            foreach (TypeSpecClassTag pi in typeDef.ParentInterfaces)
            {
                CliInterface ifc = compiler.GetClosedInterface(pi);
                if (!ifc.IsCreated)
                {
                    ifc = compiler.GetClosedInterface(pi);
                    throw new Exception();
                }

                foreach (TypeSpecClassTag pipi in ifc.InterfaceImpls2)
                    AddUniqueInterface(uniqueDeps, pipi);
                AddUniqueInterface(uniqueDeps, pi);
            }

            m_interfaceImpls = UnrollUniqueInterfaces(uniqueDeps);

            m_parentInterfaces = typeDef.ParentInterfaces;

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

            m_typeSpec = new TypeSpecClassTag(m_typeDef.TypeName, genericParameters);
            m_typeSpec = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(m_typeSpec);

            m_isCreated = true;

            return true;
        }

        private static TypeSpecClassTag[] UnrollUniqueInterfaces(Dictionary<TypeSpecClassTag, int> uniqueDeps)
        {
            TypeSpecClassTag[] interfaceImpls = new TypeSpecClassTag[uniqueDeps.Count];
            foreach (KeyValuePair<TypeSpecClassTag, int> kvp in uniqueDeps)
                interfaceImpls[kvp.Value] = kvp.Key;
            return interfaceImpls;
        }

        private CliInterface(HighTypeDef typeDef, TypeSpecClassTag typeSpec, TypeSpecTag[] genericParameters, TypeSpecClassTag[] parentInterfaces, HighClassVtableSlot[] slots,
            Dictionary<MethodDeclTag, uint> slotTagToCliSlot, Dictionary<MethodDeclTag, uint> slotTagToRealSlot, uint numRealSlots,
            TypeSpecClassTag[] interfaceImpls)
        {
            m_typeDef = typeDef;
            m_typeSpec = typeSpec;
            m_genericParameters = genericParameters;
            m_slots = slots;
            m_slotTagToCliSlot = slotTagToCliSlot;
            m_slotTagToRealSlot = slotTagToRealSlot;
            m_numRealSlots = numRealSlots;
            m_interfaceImpls = interfaceImpls;

            m_isCreated = true;
        }

        public CliInterface Instantiate(Compiler compiler, TypeSpecTag[] argTypes)
        {
            if (!m_isCreated)
                throw new RpaCompileException("Attempted to instantiate an uncreated open interface.");

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

            List<TypeSpecClassTag> parentInterfaces = new List<TypeSpecClassTag>();
            foreach (TypeSpecClassTag classTag in m_parentInterfaces)
                parentInterfaces.Add((TypeSpecClassTag)classTag.Instantiate(compiler.TagRepository, argTypes));

            TypeSpecClassTag typeSpec = (TypeSpecClassTag)m_typeSpec.Instantiate(compiler.TagRepository, argTypes);

            Dictionary<TypeSpecClassTag, int> uniqueDeps = new Dictionary<TypeSpecClassTag, int>();
            foreach (TypeSpecClassTag ifc in m_interfaceImpls)
                AddUniqueInterface(uniqueDeps, (TypeSpecClassTag)ifc.Instantiate(compiler.TagRepository, argTypes));
            TypeSpecClassTag[] interfaceImpls = UnrollUniqueInterfaces(uniqueDeps);

            return new CliInterface(m_typeDef, typeSpec, argTypes, parentInterfaces.ToArray(), slots.ToArray(), m_slotTagToCliSlot, m_slotTagToRealSlot, m_numRealSlots, interfaceImpls);
        }

        public uint CliSlotForSlotTag(MethodDeclTag methodDecl)
        {
            uint result;
            if (!m_slotTagToCliSlot.TryGetValue(methodDecl, out result))
                throw new Exception("Couldn't find method decl in interface");
            return result;
        }
    }
}
