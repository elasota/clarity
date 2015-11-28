using System;
using System.Collections.Generic;

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
            MapBasicTypesToTypeDefs();

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
                assm.ResolveGenericConstraints();
                assm.ParseCustomAttributes(this);
                assm.ResolveInterfaceImplementations();
                assm.ResolveMethodImplementations();
                assm.ResolveCustomAttributes();
            }
        }

        private InternedSet<CLRTypeSpecSZArray> m_internedSZArray = new InternedSet<CLRTypeSpecSZArray>();
        private InternedSet<CLRTypeSpecComplexArray> m_internedComplexArray = new InternedSet<CLRTypeSpecComplexArray>();
        private InternedSet<CLRTypeSpecClass> m_internedClass = new InternedSet<CLRTypeSpecClass>();
        private InternedSet<CLRTypeSpecGenericInstantiation> m_internedGenericInst = new InternedSet<CLRTypeSpecGenericInstantiation>();
        private InternedSet<CLRTypeSpecVarOrMVar> m_internedVarOrMVar = new InternedSet<CLRTypeSpecVarOrMVar>();
        private Dictionary<CLRSigType.ElementType, CLRTypeDefRow> m_simpleToConcrete = new Dictionary<CLRSigType.ElementType, CLRTypeDefRow>();
        private CLRTypeSpecVoid m_internedVoid = new CLRTypeSpecVoid();
        private CLRTypeDefRow m_runtimeTypeHandle;
        private CLRTypeDefRow m_runtimeFieldHandle;
        private CLRTypeDefRow m_runtimeMethodHandle;

        public CLRTypeDefRow RuntimeTypeHandleDef { get { return m_runtimeTypeHandle; } }
        public CLRTypeDefRow RuntimeFieldHandleDef { get { return m_runtimeFieldHandle; } }
        public CLRTypeDefRow RuntimeMethodHandleDef { get { return m_runtimeMethodHandle; } }

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
                CLRSigTypeArray cplxArray = (CLRSigTypeArray)sigType;
                CLRTypeSpec contentsType = InternVagueType(cplxArray.ContainedType);
                if (cplxArray.LowBounds == null || cplxArray.LowBounds.Length != cplxArray.Rank)
                    throw new NotSupportedException("Multidimensional arrays with unspecified lower bounds are not supported");
                if (cplxArray.Sizes.Length > 0)
                    throw new NotSupportedException("Fixed-size arrays are not supported");
                return m_internedComplexArray.Lookup(new CLRTypeSpecComplexArray(contentsType, cplxArray.Rank, cplxArray.LowBounds));
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
                if (sigSimple.BasicType == CLRSigType.ElementType.VOID)
                    return m_internedVoid;
                return InternTypeDefOrRefOrSpec(m_simpleToConcrete[sigSimple.BasicType]);
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

        public void MapBasicTypesToTypeDefs()
        {
            foreach (CLRAssembly assm in m_assemblies)
            {
                CLRAssemblyRow assmRow = (CLRAssemblyRow)assm.MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.Assembly).GetRow(0);

                if (assmRow.Name != "mscorlib")
                    continue;

                ICLRTable table = assm.MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.TypeDef);
                for (uint rowNum = 0; rowNum < table.NumRows; rowNum++)
                {
                    CLRTypeDefRow typeDef = (CLRTypeDefRow)table.GetRow(rowNum);
                    if (typeDef.TypeNamespace == "System")
                    {
                        if (typeDef.TypeName == "String")
                            m_simpleToConcrete[CLRSigType.ElementType.STRING] = typeDef;
                        else if (typeDef.TypeName == "Boolean")
                            m_simpleToConcrete[CLRSigType.ElementType.BOOLEAN] = typeDef;
                        else if (typeDef.TypeName == "Char")
                            m_simpleToConcrete[CLRSigType.ElementType.CHAR] = typeDef;
                        else if (typeDef.TypeName == "Single")
                            m_simpleToConcrete[CLRSigType.ElementType.R4] = typeDef;
                        else if (typeDef.TypeName == "Double")
                            m_simpleToConcrete[CLRSigType.ElementType.R8] = typeDef;
                        else if (typeDef.TypeName == "SByte")
                            m_simpleToConcrete[CLRSigType.ElementType.I1] = typeDef;
                        else if (typeDef.TypeName == "Int16")
                            m_simpleToConcrete[CLRSigType.ElementType.I2] = typeDef;
                        else if (typeDef.TypeName == "Int32")
                            m_simpleToConcrete[CLRSigType.ElementType.I4] = typeDef;
                        else if (typeDef.TypeName == "Int64")
                            m_simpleToConcrete[CLRSigType.ElementType.I8] = typeDef;
                        else if (typeDef.TypeName == "Byte")
                            m_simpleToConcrete[CLRSigType.ElementType.U1] = typeDef;
                        else if (typeDef.TypeName == "UInt16")
                            m_simpleToConcrete[CLRSigType.ElementType.U2] = typeDef;
                        else if (typeDef.TypeName == "UInt32")
                            m_simpleToConcrete[CLRSigType.ElementType.U4] = typeDef;
                        else if (typeDef.TypeName == "UInt64")
                            m_simpleToConcrete[CLRSigType.ElementType.U8] = typeDef;
                        else if (typeDef.TypeName == "Object")
                            m_simpleToConcrete[CLRSigType.ElementType.OBJECT] = typeDef;
                        else if (typeDef.TypeName == "String")
                            m_simpleToConcrete[CLRSigType.ElementType.STRING] = typeDef;
                        else if (typeDef.TypeName == "IntPtr")
                            m_simpleToConcrete[CLRSigType.ElementType.I] = typeDef;
                        else if (typeDef.TypeName == "UIntPtr")
                            m_simpleToConcrete[CLRSigType.ElementType.U] = typeDef;
                        else if (typeDef.TypeName == "Array")
                            m_simpleToConcrete[CLRSigType.ElementType.ARRAY] = typeDef;
                        else if (typeDef.TypeName == "ValueType")
                            m_simpleToConcrete[CLRSigType.ElementType.VALUETYPE] = typeDef;
                        else if (typeDef.TypeName == "RuntimeTypeHandle")
                            m_runtimeTypeHandle = typeDef;
                        else if (typeDef.TypeName == "RuntimeFieldHandle")
                            m_runtimeFieldHandle = typeDef;
                        else if (typeDef.TypeName == "RuntimeMethodHandle")
                            m_runtimeMethodHandle = typeDef;
                    }
                }

                break;
            }
        }
    }
}
