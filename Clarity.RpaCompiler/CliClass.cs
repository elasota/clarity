using System;
using System.Collections.Generic;
using System.Linq;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public sealed class CliClass
    {
        private enum RestrictedExtensionType
        {
            None,
            Enum,
            ValueType,
            Delegate,
            MulticastDelegate,
            Object,
            Array,
            RefSZArray,
            NullableSZArray,
            ValueSZArray,
        }

        private TypeNameTag m_typeName;
        private TypeSpecClassTag m_typeSpec;
        private TypeSpecClassTag m_parentClassSpec;
        private CliClass m_parentClass;
        private bool m_isSealed;
        private bool m_isStruct;

        private bool m_isCreated;
        private uint m_numGenericParameters;
        private HighField[] m_staticFields;
        private HighMethod[] m_methods;
        private HighField[] m_instanceFields;

        private CliVtableSlot[] m_vtable;
        private CliInterfaceImpl[] m_interfaceImpls;

        private Dictionary<MethodDeclTag, uint> m_declTagToMethod;
        private Dictionary<MethodDeclTag, uint> m_declTagToVTableSlot;
        private Dictionary<TypeSpecClassTag, uint> m_ifcToIfcSlot;

        public TypeNameTag TypeName { get { return m_typeName; } }
        public TypeSpecClassTag TypeSpec { get { return m_typeSpec; } }
        public TypeSpecClassTag ParentClassSpec { get { return m_parentClassSpec; } }
        public CliClass ParentClass { get { return m_parentClass; } }
        public bool IsSealed { get { return m_isSealed; } }
        public bool IsStruct { get { return m_isStruct; } }

        public uint NumGenericParameters { get { return m_numGenericParameters; } }
        public HighField[] StaticFields { get { return m_staticFields; } }
        public HighMethod[] Methods { get { return m_methods; } }
        public HighField[] InstanceFields { get { return m_instanceFields; } }

        public CliVtableSlot[] VTable { get { return m_vtable; } }
        public CliInterfaceImpl[] InterfaceImpls { get { return m_interfaceImpls; } }

        public IDictionary<MethodDeclTag, uint> DeclTagToMethod { get { return m_declTagToMethod; } }
        public IDictionary<MethodDeclTag, uint> DeclTagToVTableSlot { get { return m_declTagToVTableSlot; } }
        public IDictionary<TypeSpecClassTag, uint> IfcToIfcSlot { get { return m_ifcToIfcSlot; } }

        public CliClass()
        {
        }

        private CliClass(CliClass baseClass, Compiler compiler, TypeSpecTag[] argTypes)
        {
            if (!baseClass.m_isCreated)
                throw new Exception("Can't instantiate an open class that hasn't been processed");

            m_typeName = baseClass.m_typeName;
            m_parentClassSpec = (TypeSpecClassTag)baseClass.m_parentClassSpec.Instantiate(compiler.TagRepository, argTypes);
            m_parentClass = compiler.GetClosedClass(m_parentClassSpec);
            m_isSealed = baseClass.m_isSealed;
            m_isStruct = baseClass.m_isStruct;

            m_isCreated = baseClass.m_isCreated;

            m_numGenericParameters = baseClass.m_numGenericParameters;

            List<HighField> staticFields = new List<HighField>();
            foreach (HighField fld in baseClass.m_staticFields)
                staticFields.Add(fld.Instantiate(compiler.TagRepository, argTypes));

            m_staticFields = staticFields.ToArray();

            List<HighMethod> methods = new List<HighMethod>();
            foreach (HighMethod method in baseClass.m_methods)
                methods.Add(method.Instantiate(compiler.TagRepository, argTypes));
            m_methods = methods.ToArray();

            List<HighField> instanceFields = new List<HighField>();
            foreach (HighField fld in baseClass.m_instanceFields)
                instanceFields.Add(fld.Instantiate(compiler.TagRepository, argTypes));
            m_instanceFields = instanceFields.ToArray();

            List<CliVtableSlot> vtableSlots = new List<CliVtableSlot>();
            foreach (CliVtableSlot slot in baseClass.m_vtable)
                vtableSlots.Add(slot.Instantiate(compiler, argTypes));
            m_vtable = vtableSlots.ToArray();

            List<CliInterfaceImpl> interfaceImpls = new List<CliInterfaceImpl>();
            foreach (CliInterfaceImpl ifcImpl in baseClass.m_interfaceImpls)
                interfaceImpls.Add(ifcImpl.Instantiate(compiler, argTypes));
            m_interfaceImpls = interfaceImpls.ToArray();

            m_declTagToMethod = baseClass.m_declTagToMethod;
            m_declTagToVTableSlot = baseClass.m_declTagToVTableSlot;
            m_ifcToIfcSlot = baseClass.m_ifcToIfcSlot;
        }

        public bool IsCreated { get { return m_isCreated; } }

        public void Initialize(TypeNameTag nameTag)
        {
            m_typeName = nameTag;
        }

        private static RestrictedExtensionType GetRestrictedExtensionType(TypeNameTag typeName)
        {
            if (typeName.ContainerType == null && typeName.AssemblyName == "mscorlib" && typeName.TypeNamespace == "System")
            {
                if (typeName.TypeName == "Enum")
                    return RestrictedExtensionType.Enum;
                if (typeName.TypeName == "Delegate")
                    return RestrictedExtensionType.Delegate;
                if (typeName.TypeName == "MulticastDelegate")
                    return RestrictedExtensionType.MulticastDelegate;
                if (typeName.TypeName == "ValueType")
                    return RestrictedExtensionType.ValueType;
                if (typeName.TypeName == "Object")
                    return RestrictedExtensionType.Object;
                if (typeName.TypeName == "Array")
                    return RestrictedExtensionType.Array;
            }
            if (typeName.ContainerType == null && typeName.AssemblyName == "mscorlib" && typeName.TypeNamespace == "Clarity")
            {
                if (typeName.TypeName == "RefSZArray")
                    return RestrictedExtensionType.RefSZArray;
                if (typeName.TypeName == "NullableSZArray`1")
                    return RestrictedExtensionType.NullableSZArray;
                if (typeName.TypeName == "ValueSZArray`1")
                    return RestrictedExtensionType.ValueSZArray;
            }

            return RestrictedExtensionType.None;
        }

        public bool Create(Compiler compiler)
        {
            HighTypeDef typeDef = compiler.GetTypeDef(m_typeName);

            TypeSpecClassTag parentClassSpec = null;
            HighClassVtableSlot[] newSlots;
            HighClassVtableSlot[] replacedSlots;
            HighInterfaceImplementation[] typeInterfaceImpls;

            if (typeDef.Semantics == TypeSemantics.Struct || typeDef.Semantics == TypeSemantics.Class)
            {
                if (typeDef.Semantics == TypeSemantics.Struct)
                {
                    m_isStruct = true;
                    TypeNameTag valueTypeName = new TypeNameTag("mscorlib", "System", "ValueType", null);
                    valueTypeName = compiler.TagRepository.InternTypeName(valueTypeName);
                    TypeSpecClassTag vtClassTag = new TypeSpecClassTag(valueTypeName, new TypeSpecTag[0]);
                    vtClassTag = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(vtClassTag);

                    parentClassSpec = vtClassTag;
                }
                else if (typeDef.Semantics == TypeSemantics.Class)
                {
                    m_isStruct = false;
                    parentClassSpec = typeDef.ParentClass;

                    RestrictedExtensionType thisRet = GetRestrictedExtensionType(m_typeName);

                    if (parentClassSpec == null)
                    {
                        if (thisRet != RestrictedExtensionType.Object)
                            throw new Exception("Parentless class is not [mscorlib]System.Object");
                    }
                    else
                    {
                        if (thisRet == RestrictedExtensionType.Object)
                            throw new Exception("[mscorlib]System.Object has a parent");

                        RestrictedExtensionType parentRet = GetRestrictedExtensionType(parentClassSpec.TypeName);

                        bool isExtensionOK = false;
                        switch (parentRet)
                        {
                            case RestrictedExtensionType.ValueType:
                                if (thisRet == RestrictedExtensionType.Enum)
                                    isExtensionOK = true;
                                break;
                            case RestrictedExtensionType.Delegate:
                                if (thisRet == RestrictedExtensionType.MulticastDelegate)
                                    isExtensionOK = true;
                                break;
                            case RestrictedExtensionType.Enum:
                            case RestrictedExtensionType.MulticastDelegate:
                            case RestrictedExtensionType.NullableSZArray:
                            case RestrictedExtensionType.RefSZArray:
                            case RestrictedExtensionType.ValueSZArray:
                                break;
                            case RestrictedExtensionType.Object:
                            case RestrictedExtensionType.None:
                                isExtensionOK = true;
                                break;
                            case RestrictedExtensionType.Array:
                                if (thisRet == RestrictedExtensionType.NullableSZArray || thisRet == RestrictedExtensionType.ValueSZArray || thisRet == RestrictedExtensionType.RefSZArray)
                                    isExtensionOK = true;
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        if (!isExtensionOK)
                            throw new Exception("Invalid extension of a restricted class");
                    }
                }

                m_instanceFields = typeDef.InstanceFields;
                m_methods = typeDef.Methods;
                m_staticFields = typeDef.StaticFields;
                m_isSealed = typeDef.IsSealed;

                replacedSlots = typeDef.ReplacedSlots;
                newSlots = typeDef.NewSlots;
                typeInterfaceImpls = typeDef.InterfaceImpls;
            }
            else if (typeDef.Semantics == TypeSemantics.Delegate)
            {
                m_isStruct = false;

                TypeNameTag dgTypeName = new TypeNameTag("mscorlib", "System", typeDef.IsMulticastDelegate ? "MulticastDelegate" : "Delegate", null);
                dgTypeName = compiler.TagRepository.InternTypeName(dgTypeName);
                TypeSpecClassTag dgClassTag = new TypeSpecClassTag(dgTypeName, new TypeSpecTag[0]);
                dgClassTag = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(dgClassTag);

                parentClassSpec = dgClassTag;

                m_instanceFields = new HighField[0];
                m_methods = new HighMethod[0];
                m_staticFields = new HighField[0];
                m_isSealed = false;

                replacedSlots = new HighClassVtableSlot[0];


                MethodDeclTag invokeTag = new MethodDeclTag("Invoke", typeDef.DelegateSignature, m_typeName);
                invokeTag = compiler.TagRepository.InternMethodDeclTag(invokeTag);

                HighClassVtableSlot invokeSlot = new HighClassVtableSlot(invokeTag, typeDef.DelegateSignature, null, true, false);

                newSlots = new HighClassVtableSlot[1] { invokeSlot };

                typeInterfaceImpls = new HighInterfaceImplementation[0];
            }
            else if (typeDef.Semantics == TypeSemantics.Enum)
            {
                m_isStruct = false;

                TypeNameTag dgTypeName = new TypeNameTag("mscorlib", "System", "Enum", null);
                dgTypeName = compiler.TagRepository.InternTypeName(dgTypeName);
                TypeSpecClassTag dgClassTag = new TypeSpecClassTag(dgTypeName, new TypeSpecTag[0]);
                dgClassTag = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(dgClassTag);

                parentClassSpec = dgClassTag;

                string underlyingTypeName;
                switch (typeDef.UnderlyingType)
                {
                    case HighTypeDef.EnumUnderlyingType.Int8:
                        underlyingTypeName = "SByte";
                        break;
                    case HighTypeDef.EnumUnderlyingType.Int16:
                        underlyingTypeName = "Int16";
                        break;
                    case HighTypeDef.EnumUnderlyingType.Int32:
                        underlyingTypeName = "Int32";
                        break;
                    case HighTypeDef.EnumUnderlyingType.Int64:
                        underlyingTypeName = "Int64";
                        break;
                    case HighTypeDef.EnumUnderlyingType.UInt8:
                        underlyingTypeName = "Byte";
                        break;
                    case HighTypeDef.EnumUnderlyingType.UInt16:
                        underlyingTypeName = "Int16";
                        break;
                    case HighTypeDef.EnumUnderlyingType.UInt32:
                        underlyingTypeName = "UInt32";
                        break;
                    case HighTypeDef.EnumUnderlyingType.UInt64:
                        underlyingTypeName = "UInt64";
                        break;
                    default:
                        throw new Exception();
                }

                TypeNameTag underlyingTypeNameTag = new TypeNameTag("mscorlib", "System", underlyingTypeName, null);
                underlyingTypeNameTag = compiler.TagRepository.InternTypeName(underlyingTypeNameTag);

                TypeSpecClassTag underlyingTypeSpec = new TypeSpecClassTag(underlyingTypeNameTag, new TypeSpecTag[0]);
                underlyingTypeSpec = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(underlyingTypeSpec);

                HighField fld = new HighField("value__", underlyingTypeSpec);

                m_instanceFields = new HighField[1] { fld };
                m_methods = new HighMethod[0];
                m_staticFields = new HighField[0];
                m_isSealed = true;

                replacedSlots = new HighClassVtableSlot[0];
                newSlots = new HighClassVtableSlot[0];
                typeInterfaceImpls = new HighInterfaceImplementation[0];
            }
            else
                throw new ArgumentException();

            if (parentClassSpec != null)
            {
                if (compiler.GetTypeDef(parentClassSpec.TypeName).Semantics != TypeSemantics.Class)
                    throw new Exception("Can't extend class with non-class semantics");

                if (!compiler.HaveCliOpenClass(parentClassSpec.TypeName))
                    return false;

                CliClass parentClass = compiler.GetClosedClass(parentClassSpec);

                if (parentClass.m_isSealed)
                    throw new Exception("Can't extend sealed CLI class");

                m_parentClass = parentClass;
            }

            m_numGenericParameters = typeDef.NumGenericParameters;

            // Update vtable
            {
                Dictionary<MethodDeclTag, uint> declTagToMethod = new Dictionary<MethodDeclTag, uint>();
                uint methodIndex = 0;
                foreach (HighMethod method in m_methods)
                {
                    if (declTagToMethod.ContainsKey(method.MethodDeclTag))
                        throw new Exception("Duplicate method declaration");

                    declTagToMethod.Add(method.MethodDeclTag, methodIndex++);
                }
                m_declTagToMethod = declTagToMethod;
            }

            m_declTagToVTableSlot = new Dictionary<MethodDeclTag, uint>();
            m_ifcToIfcSlot = new Dictionary<TypeSpecClassTag, uint>();
            List<CliVtableSlot> slots = new List<CliVtableSlot>();

            if (m_parentClass != null)
            {
                foreach (KeyValuePair<MethodDeclTag, uint> dttvs in m_parentClass.m_declTagToVTableSlot)
                    m_declTagToVTableSlot.Add(dttvs.Key, dttvs.Value);
                slots.AddRange(m_parentClass.m_vtable);
            }

            foreach (HighClassVtableSlot slot in replacedSlots)
            {
                uint index;
                if (!m_declTagToVTableSlot.TryGetValue(slot.SlotTag, out index))
                    throw new Exception("Unmatched vtable slot replacement");
                CliVtableSlot existingSlot = slots[(int)index];
                if (existingSlot.IsSealed)
                    throw new Exception("Can't replace sealed vtable slot");

                if (existingSlot.MethodSignature != slot.Signature)
                    throw new Exception("VTable slot override signature doesn't match");

                CliMethodIndex methodIndex = null;
                if (!slot.IsAbstract)
                {
                    HighMethod method;
                    methodIndex = this.FindMethod(slot.ImplementingMethodTag, out method);
                    if (method.MethodSignature != slot.Signature)
                        throw new Exception("Signature of method used by vtable slot doesn't match vtable slot's signature");
                }

                slots[(int)index] = new CliVtableSlot(methodIndex, slot.Signature, slot.IsFinal);
            }

            foreach (HighClassVtableSlot slot in newSlots)
            {
                CliMethodIndex methodIndex = null;
                if (!slot.IsAbstract)
                {
                    HighMethod method;
                    methodIndex = this.FindMethod(slot.ImplementingMethodTag, out method);
                    if (method.MethodSignature != slot.Signature)
                        throw new Exception("Signature of method used by vtable slot doesn't match vtable slot's signature");
                }

                m_declTagToVTableSlot.Add(slot.SlotTag, (uint)slots.Count);
                slots.Add(new CliVtableSlot(methodIndex, slot.Signature, slot.IsFinal));
            }

            m_vtable = slots.ToArray();

            HashSet<TypeSpecClassTag> explicitImpls = new HashSet<TypeSpecClassTag>();
            List<CliInterfaceImpl> interfaceImpls = new List<CliInterfaceImpl>();
            foreach (HighInterfaceImplementation ifcImpl in typeInterfaceImpls)
            {
                if (!explicitImpls.Add(ifcImpl.Interface))
                    throw new Exception("Duplicate interface implementation");

                interfaceImpls.Add(ResolveInterfaceImpl(compiler, ifcImpl));
            }

            m_interfaceImpls = interfaceImpls.ToArray();
            m_parentClassSpec = parentClassSpec;

            List<TypeSpecTag> thisGenericParameters = new List<TypeSpecTag>();
            for (uint i = 0; i < m_numGenericParameters; i++)
            {
                TypeSpecGenericParamTypeTag gptt = new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.Var);
                TypeSpecGenericParamTag gpTag = new TypeSpecGenericParamTag(gptt, i);
                gpTag = (TypeSpecGenericParamTag)compiler.TagRepository.InternTypeSpec(gpTag);

                thisGenericParameters.Add(gpTag);
            }

            TypeSpecClassTag thisClass = new TypeSpecClassTag(m_typeName, thisGenericParameters.ToArray());
            thisClass = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(thisClass);

            m_isCreated = true;

            return true;
        }

        private static void CheckSlotCanImpl(CliVtableSlot slot, HighMethod method)
        {
            if (method.IsStatic)
                throw new Exception("Can't implement a vtable slot using a static method");
            if (slot.MethodSignature != method.MethodSignature)
                throw new Exception("VTable slot signature doesn't match the signature of an implementation");
            throw new NotImplementedException();
        }

        private CliMethodIndex FindMethod(MethodDeclTag methodDecl, out HighMethod matchedMethod)
        {
            CliClass cls = this;
            uint depth = 0;
            while (cls != null)
            {
                uint index = 0;
                foreach (HighMethod method in cls.m_methods)
                {
                    if (method.MethodDeclTag == methodDecl)
                    {
                        matchedMethod = method;
                        return new CliMethodIndex(depth, index);
                    }
                    index++;
                }
                cls = cls.m_parentClass;
                depth++;
            }
            throw new Exception("Couldn't find matching method decl");
        }

        private CliInterfaceImpl ResolveInterfaceImpl(Compiler compiler, HighInterfaceImplementation highIfcImpl)
        {
            CliInterface ifc = compiler.GetClosedInterface(highIfcImpl.Interface);

            uint[] slotMappings = new uint[ifc.Slots.Length];
            bool[] slotIsMapped = new bool[slotMappings.Length];

            foreach (HighInterfaceMethodImplementation methodImpl in highIfcImpl.MethodImpls)
            {
                uint classIndex;
                if (!m_declTagToVTableSlot.TryGetValue(methodImpl.ClassSlot, out classIndex))
                    throw new Exception("Couldn't map class vtable slot to interface slot");

                uint ifcIndex = ifc.CliSlotForSlotTag(methodImpl.InterfaceSlot);
                if (slotIsMapped[ifcIndex])
                    throw new Exception("Interface method implemented multiple times");
                slotIsMapped[ifcIndex] = true;
                slotMappings[ifcIndex] = classIndex;
            }

            return new CliInterfaceImpl(highIfcImpl.Interface, slotMappings.ToArray());
        }

        public CliClass Instantiate(Compiler compiler, TypeSpecTag[] argTypes)
        {
            if (!this.m_isCreated)
                throw new Exception("Can't instantiate an uncreated closed class");

            if (m_numGenericParameters != (uint)argTypes.Length)
                throw new Exception("Class instantiation doesn't match generic parameter count");

            if (m_numGenericParameters == 0)
                return this;

            return new CliClass(this, compiler, argTypes);
        }
    }
}
