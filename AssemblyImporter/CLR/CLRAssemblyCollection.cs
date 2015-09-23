using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    public class CLRAssemblyCollection : IEnumerable<CLRAssembly>
    {
        private class InternedSet<T>
            where T : IEquatable<T>
        {
            private List<T> m_list;
            private Dictionary<T, int> m_dict;

            public InternedSet()
            {
                m_list = new List<T>();
                m_dict = new Dictionary<T, int>();
            }

            public T Lookup(T v)
            {
                int index;
                if (m_dict.TryGetValue(v, out index))
                    return m_list[index];
                m_dict[v] = m_list.Count;
                m_list.Add(v);
                return v;
            }
        }

        private List<CLRAssembly> m_assemblies = new List<CLRAssembly>();

        public class AssemblyEnumerator : IEnumerator<CLRAssembly>
        {
            private IEnumerator<CLRAssembly> m_enum;

            public AssemblyEnumerator(CLRAssemblyCollection coll)
            {
                m_enum = coll.m_assemblies.GetEnumerator();
            }

            public void Dispose()
            {
                m_enum.Dispose();
            }

            public void Reset()
            {
                m_enum.Reset();
            }

            public bool MoveNext()
            {
                return m_enum.MoveNext();
            }

            public CLRAssembly Current { get { return m_enum.Current; } }
            object System.Collections.IEnumerator.Current { get { return m_enum.Current; } }
        }

        public IEnumerator<CLRAssembly> GetEnumerator()
        {
            return new AssemblyEnumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new AssemblyEnumerator(this);
        }

        public void Add(CLRAssembly assm)
        {
            m_assemblies.Add(assm);
        }

        public void ResolveAll()
        {
            while (true)
            {
                bool resolvedAll = true;
                bool resolvedAny = false;
                foreach (CLRAssembly assm in m_assemblies)
                    assm.Resolve(this, ref resolvedAny, ref resolvedAll);

                if (!resolvedAll && !resolvedAny)
                    throw new ParseFailedException("Failed to resolve assemblies");
                if (resolvedAll)
                    break;
            }

            foreach (CLRAssembly assm in m_assemblies)
            {
                assm.ResolveNestedClasses();
                assm.ResolveGenericParameters();
                assm.ParseCustomAttributes(this);
                assm.ResolveInterfaceImplementations();
                assm.ResolveMethodImplementations();
            }
        }

        private InternedSet<CLRTypeSpecSZArray> m_internedSZArray = new InternedSet<CLRTypeSpecSZArray>();
        private InternedSet<CLRTypeSpecSimple> m_internedSimple = new InternedSet<CLRTypeSpecSimple>();
        private InternedSet<CLRTypeSpecClass> m_internedClass = new InternedSet<CLRTypeSpecClass>();
        private InternedSet<CLRTypeSpecGenericInstantiation> m_internedGenericInst = new InternedSet<CLRTypeSpecGenericInstantiation>();
        private InternedSet<CLRTypeSpecVarOrMVar> m_internedVarOrMVar = new InternedSet<CLRTypeSpecVarOrMVar>();

        public CLRTypeSpec InternTypeDefOrRefOrSpec(CLRTableRow tableRow)
        {
            if (tableRow is CLRTypeDefRow)
                return m_internedClass.Lookup(new CLRTypeSpecClass((CLRTypeDefRow)tableRow));
            else if (tableRow is CLRTypeRefRow)
            {
                CLRTypeRefRow typeRef = (CLRTypeRefRow)tableRow;
                if (typeRef.Resolution != null)
                    return m_internedClass.Lookup(new CLRTypeSpecClass(typeRef.Resolution));
                return null;
            }
            else if (tableRow is CLRTypeSpecRow)
            {
                CLRTypeSpecRow typeSpec = (CLRTypeSpecRow)tableRow;
                if (typeSpec.Resolution != null)
                    return typeSpec.Resolution;
                return null;
            }
            else
                throw new ArgumentException("Unknown sig type");
        }

        public CLRTypeSpec InternVagueType(CLRSigType sigType)
        {
            if (sigType is CLRSigTypeSZArray)
            {
                CLRSigTypeSZArray szArray = (CLRSigTypeSZArray)sigType;
                CLRTypeSpec contentsType = InternVagueType(szArray.ContainedType);
                if (contentsType == null)
                    return null;
                return m_internedSZArray.Lookup(new CLRTypeSpecSZArray(contentsType));
            }
            else if (sigType is CLRSigTypeArray)
            {
                throw new NotImplementedException();
            }
            else if (sigType is CLRSigTypeFunctionPointer)
            {
                throw new NotImplementedException();
            }
            else if (sigType is CLRSigTypeGenericInstantiation)
            {
                CLRSigTypeGenericInstantiation genericInst = (CLRSigTypeGenericInstantiation)sigType;
                CLRTypeSpec genericType = InternTypeDefOrRefOrSpec(genericInst.GenericType);
                if (genericType == null)
                    return null;
                List<CLRTypeSpec> argTypes = new List<CLRTypeSpec>();
                foreach (CLRSigType argType in genericInst.ArgTypes)
                {
                    CLRTypeSpec argTypeInterned = InternVagueType(argType);
                    if (argTypeInterned == null)
                        return null;
                    argTypes.Add(argTypeInterned);
                }
                if (!(genericType is CLRTypeSpecClass))
                    throw new ArgumentException();
                return m_internedGenericInst.Lookup(new CLRTypeSpecGenericInstantiation(genericInst.InstantiationType, (CLRTypeSpecClass)genericType, argTypes.ToArray()));
            }
            else if (sigType is CLRSigTypeVarOrMVar)
            {
                CLRSigTypeVarOrMVar varOrMVar = (CLRSigTypeVarOrMVar)sigType;
                return m_internedVarOrMVar.Lookup(new CLRTypeSpecVarOrMVar(varOrMVar.BasicType, varOrMVar.Value));
            }
            else if (sigType is CLRSigTypePointer)
            {
                throw new NotImplementedException();
            }
            else if (sigType is CLRSigTypeSimple)
            {
                CLRSigTypeSimple sigSimple = (CLRSigTypeSimple)sigType;
                return m_internedSimple.Lookup(new CLRTypeSpecSimple(sigSimple.BasicType));
            }
            else if (sigType is CLRSigTypeStructured)
            {
                CLRSigTypeStructured sigStructured = (CLRSigTypeStructured)sigType;
                return InternTypeDefOrRefOrSpec(sigStructured.TypeDefOrRefOrSpec);
            }
            else
                throw new ParseFailedException("Strange sig type");
        }

        public CLRTypeSpec InternTypeSpec(CLRSigTypeSpec sigType)
        {
            return InternVagueType(sigType.Type);

        }
    }
}
