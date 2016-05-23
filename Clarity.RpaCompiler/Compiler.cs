using System;
using System.IO;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class Compiler
    {
        private Dictionary<string, string> m_strings = new Dictionary<string, string>();

        private HashSet<string> m_registeredAssemblies = new HashSet<string>();

        private Dictionary<TypeNameTag, HighTypeDef> m_typeDefsDict = new Dictionary<TypeNameTag, HighTypeDef>();
        private List<HighTypeDef> m_typeDefsList = new List<HighTypeDef>();

        private UniqueQueue<MethodSpecTag, MethodHandle> m_methodInstances = new UniqueQueue<MethodSpecTag, MethodHandle>();

        private UniqueQueue<TypeNameTag, CliClass> m_openClasses = new UniqueQueue<TypeNameTag, CliClass>();
        private Dictionary<TypeSpecClassTag, CliClass> m_closedClasses = new Dictionary<TypeSpecClassTag, CliClass>();

        private UniqueQueue<TypeNameTag, CliInterface> m_openInterfaces = new UniqueQueue<TypeNameTag, CliInterface>();
        private Dictionary<TypeSpecClassTag, CliInterface> m_closedInterfaces = new Dictionary<TypeSpecClassTag, CliInterface>();
        private Dictionary<RloType, RloType> m_internedRloTypes = new Dictionary<RloType, RloType>();
        private RloTypedRefType m_internedTypedRefType = new RloTypedRefType();
        private TagRepository m_tagRepository = new TagRepository();
        private CompilerConfig m_compilerConfig = new CompilerConfig();
        private AssignabilityResolver m_assignabilityResolver;

        public TagRepository TagRepository { get { return m_tagRepository; } }
        public IEnumerable<HighTypeDef> TypeDefs{ get { return m_typeDefsList; } }
        public RloTypedRefType InternedRloTypedRefType { get { return m_internedTypedRefType; } }
        public CompilerConfig Config { get { return m_compilerConfig; } }
        public AssignabilityResolver AssignabilityResolver { get { return m_assignabilityResolver; } }

        public Compiler()
        {
            m_assignabilityResolver = new AssignabilityResolver(this);
        }

        public void LoadRpa(BinaryReader reader)
        {
            uint magic = reader.ReadUInt32();
            if (magic != 0x41503252)
                throw new Exception("Invalid RPA header");

            CatalogReader catalog = new CatalogReader(m_tagRepository, reader);

            uint numTypeDefs = reader.ReadUInt32();
            for (uint i = 0; i < numTypeDefs; i++)
                LoadTypeDef(catalog, reader);
        }

        private void LoadTypeDef(CatalogReader catalog, BinaryReader reader)
        {
            HighTypeDef typeDef = new HighTypeDef();
            typeDef.Read(m_tagRepository, catalog, reader);

            if (m_typeDefsDict.ContainsKey(typeDef.TypeName))
                throw new RpaLoadException("Type defined multiple times");

            if (catalog.AssemblyName != typeDef.TypeName.AssemblyName)
                throw new RpaLoadException("Type declared outside of its assembly");

            Console.WriteLine(typeDef.TypeName.ToString());

            m_typeDefsDict.Add(typeDef.TypeName, typeDef);
            m_typeDefsList.Add(typeDef);
        }

        public void CompileAllMethods()
        {
            bool anyNew = true;
            while (anyNew)
            {
                anyNew = false;
                while (m_methodInstances.HaveNext)
                {
                    KeyValuePair<MethodSpecTag, MethodHandle> methodInstance = m_methodInstances.GetNext();
                    MethodHandle handle = methodInstance.Value;
                    handle.Value = new RloMethod(this, methodInstance.Key, handle.InstantiationPath);
                    anyNew = true;
                }
            }
        }

        public void CompileOpenClasses()
        {
            Queue<CliInterface> requeuedInterfaces = new Queue<CliInterface>();
            Queue<CliInterface> queuedInterfaces = new Queue<CliInterface>();

            while(true)
            {
                while (m_openInterfaces.HaveNext)
                {
                    KeyValuePair<TypeNameTag, CliInterface> ifcDef = m_openInterfaces.GetNext();

                    HighTypeDef typeDef;
                    if (!m_typeDefsDict.TryGetValue(ifcDef.Key, out typeDef))
                        throw new Exception("Missing interface definition");

                    ifcDef.Value.Initialize(typeDef);
                    queuedInterfaces.Enqueue(ifcDef.Value);
                }

                bool triedAny = false;
                bool completedAny = false;
                while (queuedInterfaces.Count > 0)
                {
                    triedAny = true;
                    CliInterface cls = queuedInterfaces.Dequeue();
                    if (cls.Create(this))
                        completedAny = true;
                    else
                        requeuedInterfaces.Enqueue(cls);
                }

                queuedInterfaces = requeuedInterfaces;
                requeuedInterfaces = new Queue<CliInterface>();

                if (triedAny)
                {
                    if (!completedAny)
                        throw new Exception("Couldn't instantiate any interfaces, probably due to a cycle.");
                }
                else
                    break;
            }

            Queue<CliClass> requeuedClasses = new Queue<CliClass>();
            Queue<CliClass> queuedClasses = new Queue<CliClass>();

            while (true)
            {
                while (m_openClasses.HaveNext)
                {
                    KeyValuePair<TypeNameTag, CliClass> classDef = m_openClasses.GetNext();

                    HighTypeDef typeDef;
                    if (!m_typeDefsDict.TryGetValue(classDef.Key, out typeDef))
                        throw new Exception("Missing class definition");

                    classDef.Value.Initialize(classDef.Key);
                    queuedClasses.Enqueue(classDef.Value);
                }

                bool triedAny = false;
                bool completedAny = false;
                while (queuedClasses.Count > 0)
                {
                    triedAny = true;
                    CliClass cls = queuedClasses.Dequeue();
                    if (cls.Create(this))
                        completedAny = true;
                    else
                        requeuedClasses.Enqueue(cls);
                }

                queuedClasses = requeuedClasses;
                requeuedClasses = new Queue<CliClass>();

                if (triedAny)
                {
                    if (!completedAny)
                        throw new Exception("Couldn't instantiate any types, probably due to a cycle.");
                }
                else
                    break;
            }
        }

        public bool HaveCliOpenClass(TypeNameTag typeName)
        {
            if (m_typeDefsDict[typeName].Semantics == TypeSemantics.Interface)
                throw new ArgumentException();
            return m_openClasses.Lookup(typeName).IsCreated;
        }

        public bool HaveCliOpenInterface(TypeNameTag typeName)
        {
            if (m_typeDefsDict[typeName].Semantics != TypeSemantics.Interface)
                throw new ArgumentException();
            return m_openInterfaces.Lookup(typeName).IsCreated;
        }

        public CliClass GetClosedClass(TypeSpecClassTag typeSpec)
        {
            CliClass cls;
            if (m_closedClasses.TryGetValue(typeSpec, out cls))
                return cls;
            cls = m_openClasses.Lookup(typeSpec.TypeName).Instantiate(this, typeSpec.ArgTypes);
            m_closedClasses.Add(typeSpec, cls);
            return cls;
        }

        public CliInterface GetClosedInterface(TypeSpecClassTag typeSpec)
        {
            CliInterface ifc;
            if (m_closedInterfaces.TryGetValue(typeSpec, out ifc))
                return ifc;

            ifc = m_openInterfaces.Lookup(typeSpec.TypeName).Instantiate(this, typeSpec.ArgTypes);
            m_closedInterfaces.Add(typeSpec, ifc);
            return ifc;
        }

        public MethodHandle InstantiateMethod(MethodSpecTag methodSpecTag, MethodInstantiationPath instantiationPath)
        {
            MethodHandle handle = m_methodInstances.Lookup(methodSpecTag);
            if (handle.InstantiationPath == null)
                handle.InstantiationPath = instantiationPath;
            return handle;
        }

        public void InstantiateOpenClass(TypeNameTag typeName)
        {
            if (m_typeDefsDict[typeName].Semantics == TypeSemantics.Interface)
                throw new ArgumentException();
            m_openClasses.Lookup(typeName);
        }

        public void InstantiateInterface(TypeNameTag typeName)
        {
            if (m_typeDefsDict[typeName].Semantics != TypeSemantics.Interface)
                throw new ArgumentException();
            m_openInterfaces.Lookup(typeName);
        }

        public HighTypeDef GetTypeDef(TypeNameTag typeName)
        {
            HighTypeDef result;
            if (!m_typeDefsDict.TryGetValue(typeName, out result))
                throw new Exception("Unresolved type name");
            return result;
        }

        public bool RegisterAssembly(string assemblyName)
        {
            return m_registeredAssemblies.Add(assemblyName);
        }

        public RloType InternRloType(RloType rloType)
        {
            RloType interned;
            if (m_internedRloTypes.TryGetValue(rloType, out interned))
                return interned;
            m_internedRloTypes.Add(rloType, rloType);
            return rloType;
        }

        public bool TypeIsInterface(TypeSpecTag type)
        {
            if (type is TypeSpecArrayTag)
                return false;
            if (type is TypeSpecClassTag)
            {
                TypeSpecClassTag tsClass = (TypeSpecClassTag)type;
                return GetTypeDef(tsClass.TypeName).Semantics == TypeSemantics.Interface;
            }

            throw new RpaCompileException("Invalid type where a value or reference type is expected");
        }

        public bool TypeIsValueType(TypeSpecTag type)
        {
            if (type is TypeSpecArrayTag)
                return false;
            if (type is TypeSpecClassTag)
            {
                TypeSpecClassTag tsClass = (TypeSpecClassTag)type;
                switch (GetTypeDef(tsClass.TypeName).Semantics)
                {
                    case TypeSemantics.Class:
                    case TypeSemantics.Delegate:
                    case TypeSemantics.Interface:
                        return false;
                    case TypeSemantics.Enum:
                    case TypeSemantics.Struct:
                        return true;
                    default:
                        throw new ArgumentException();
                }
            }

            throw new RpaCompileException("Invalid type where a value or reference type is expected");
        }

        public uint DevirtualizeInterfaceMethod(CliClass cls, TypeSpecClassTag ifcSpec, uint ifcSlotIndex)
        {
            if (!cls.IsSealed)
                throw new ArgumentException("Can't devirtualize a non-sealed class");

            if (GetTypeDef(ifcSpec.TypeName).Semantics != TypeSemantics.Interface)
                throw new ArgumentException("Can't devirtualize an implementation of a non-interface");

            return RecursiveDevirtualizeInterfaceMethod(cls, ifcSpec, ifcSlotIndex);
        }

        private uint RecursiveDevirtualizeInterfaceMethod(CliClass cls, TypeSpecClassTag ifcSpec, uint ifcSlotIndex)
        {
            // First, look for an exact match, but only on this class level
            foreach (CliInterfaceImpl impl in cls.InterfaceImpls2)
            {
                if (impl.Interface == ifcSpec)
                {
                    CliInterfaceImplSlot slot = impl.IfcSlotToClassVtableSlot[ifcSlotIndex];
                    // Only use the exact match if it has a new implementation.
                    // This is non-standard, but reflects .NET's behavior.
                    // See TestInheritedImplementationDeprioritization.
                    if (slot.HaveNewImpl)
                        return slot.ClassVTableSlot;
                    break;
                }
            }

            // Otherwise, check all interfaces
            uint? bestSlotTDO = null;
            CliInterfaceImplSlot? bestSlot = null;
            foreach (CliInterfaceImpl impl in cls.InterfaceImpls2)
            {
                TypeSpecClassTag implInterface = impl.Interface;
                    
                if (m_assignabilityResolver.ResolveGenericVariantAssignableTo(ifcSpec, implInterface) == AssignabilityResolver.ConversionType.InterfaceToInterface)
                {
                    CliInterfaceImplSlot slot = impl.IfcSlotToClassVtableSlot[ifcSlotIndex];
                    if (slot.HaveNewImpl)
                    {
                        uint tdo = cls.TypeDeclarationOrder[implInterface];
                        if (bestSlotTDO.HasValue == false || tdo < bestSlotTDO)
                        {
                            bestSlotTDO = tdo;
                            bestSlot = slot;
                        }
                    }
                }
            }

            if (bestSlot.HasValue)
                return bestSlot.Value.ClassVTableSlot;

            CliClass parentClass = cls.ParentClass;
            if (parentClass == null)
                throw new Exception("Internal error: Unresolvable interface method");

            return RecursiveDevirtualizeInterfaceMethod(parentClass, ifcSpec, ifcSlotIndex);
        }
    }
}
