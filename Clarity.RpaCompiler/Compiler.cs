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

        private Dictionary<TypeNameTag, CliInterface> m_openInterfaces = new Dictionary<TypeNameTag, CliInterface>();
        private Dictionary<TypeSpecClassTag, CliInterface> m_closedInterfaces = new Dictionary<TypeSpecClassTag, CliInterface>();

        private Dictionary<RloType, RloType> m_internedRloTypes = new Dictionary<RloType, RloType>();

        private RloTypedRefType m_internedTypedRefType = new RloTypedRefType();

        private TagRepository m_tagRepository = new TagRepository();

        public TagRepository TagRepository { get { return m_tagRepository; } }

        public IEnumerable<HighTypeDef> TypeDefs{ get { return m_typeDefsList; } }

        public RloTypedRefType InternedRloTypedRefType { get { return m_internedTypedRefType; } }

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
                throw new Exception("Type defined multiple times");

            if (catalog.AssemblyName != typeDef.TypeName.AssemblyName)
                throw new Exception("Type declared outside of its assembly");

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
                    methodInstance.Value.Value = new RloMethod(this, methodInstance.Key);
                    anyNew = true;
                }
            }
        }

        public void CompileOpenClasses()
        {
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

                    uint numParams = typeDef.NumGenericParameters;
                    TypeSpecTag[] genericParams = new TypeSpecTag[numParams];
                    TypeSpecGenericParamTypeTag varType = new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.Var);
                    for (uint i = 0; i < numParams; i++)
                    {
                        TypeSpecTag genParam = new TypeSpecGenericParamTag(varType, i);
                        genParam = this.TagRepository.InternTypeSpec(genParam);
                        genericParams[i] = genParam;
                    }

                    TypeSpecClassTag clsTag = new TypeSpecClassTag(classDef.Key, genericParams);
                    clsTag = (TypeSpecClassTag)this.TagRepository.InternTypeSpec(clsTag);

                    classDef.Value.Initialize(clsTag.TypeName);
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

        public CliClass GetClosedClass(TypeSpecClassTag typeSpec)
        {
            CliClass cls;
            if (m_closedClasses.TryGetValue(typeSpec, out cls))
                return cls;
            return m_openClasses.Lookup(typeSpec.TypeName).Instantiate(this, typeSpec.ArgTypes);
        }

        public CliInterface GetClosedInterface(TypeSpecClassTag typeSpec)
        {
            CliInterface closedIfc;
            if (m_closedInterfaces.TryGetValue(typeSpec, out closedIfc))
                return closedIfc;

            CliInterface openIfc;
            if (!m_openInterfaces.TryGetValue(typeSpec.TypeName, out openIfc))
                throw new Exception("Type spec does not reference interface");

            closedIfc = openIfc.Instantiate(this, typeSpec.ArgTypes);
            m_closedInterfaces.Add(typeSpec, closedIfc);
            return closedIfc;
        }

        public void InstantiateMethod(MethodSpecTag methodSpecTag)
        {
            m_methodInstances.Lookup(methodSpecTag);
        }

        public void InstantiateOpenClass(TypeNameTag typeName)
        {
            if (m_typeDefsDict[typeName].Semantics == TypeSemantics.Interface)
                throw new ArgumentException();
            m_openClasses.Lookup(typeName);
        }

        public void InstantiateInterface(TypeNameTag typeName)
        {
            if (m_openInterfaces.ContainsKey(typeName))
                throw new Exception("CLI interface declared multiple times");

            m_openInterfaces.Add(typeName, new CliInterface(this, m_typeDefsDict[typeName]));
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
    }
}
