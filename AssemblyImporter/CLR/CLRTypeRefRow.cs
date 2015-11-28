using System;

namespace AssemblyImporter.CLR
{
    // II.22.38
    public class CLRTypeRefRow : CLRTableRow, ICLRResolvable, ICLRHasCustomAttributes
    {
        public CLRTableRow ResolutionScope { get; private set; }
        public string TypeName { get; private set; }
        public string TypeNamespace { get; private set; }

        public CLRTypeDefRow Resolution { get; private set; }

        public bool IsResolved { get { return Resolution != null; } }

        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }

        public override void Parse(CLRMetaDataParser parser)
        {
            ResolutionScope = parser.ReadResolutionScope();
            TypeName = parser.ReadString();
            TypeNamespace = parser.ReadString();
        }

        public void Resolve(CLRAssemblyCollection assemblies)
        {
            if (ResolutionScope == null)
                throw new NotImplementedException();    // Search ExportedType table
            else if (ResolutionScope is CLRTypeRefRow)
            {
                // Nested type
                CLRTypeRefRow typeRef = (CLRTypeRefRow)ResolutionScope;
                if (typeRef.Resolution == null)
                    return;
                if (TypeNamespace != "")
                    throw new ParseFailedException("Strange type namespace in nested type");
                CLRTypeDefRow typeDef = typeRef.Resolution;
                CLRMetaData assmMetaData = typeDef.Table.MetaData;
                ICLRTable nestedClassTable = assmMetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.NestedClass);
                for (uint i = 0; i < nestedClassTable.NumRows; i++)
                {
                    CLRNestedClassRow nestedClass = (CLRNestedClassRow)nestedClassTable.GetRow(i);
                    if (nestedClass.EnclosingClass == typeDef)
                    {
                        CLRTypeDefRow childClass = nestedClass.NestedClass;
                        if (childClass.TypeName == TypeName)
                        {
                            Resolution = childClass;
                            return;
                        }
                    }
                }
                throw new ParseFailedException("Failed to resolve reference to nested type");
            }
            else if (ResolutionScope is CLRModuleRefRow)
                throw new NotImplementedException();
            else if (ResolutionScope is CLRModuleRow)
                throw new NotImplementedException();
            else if (ResolutionScope is CLRAssemblyRefRow)
            {
                CLRAssemblyRefRow assemblyRef = (CLRAssemblyRefRow)ResolutionScope;
                if (assemblyRef.Resolution == null)
                    return;
                CLRAssembly externAssm = assemblyRef.Resolution;
                ICLRTable typeDefTable = externAssm.MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.TypeDef);
                for (uint i = 0; i < typeDefTable.NumRows; i++)
                {
                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefTable.GetRow(i);
                    if (typeDef.TypeNamespace == TypeNamespace && typeDef.TypeName == TypeName)
                    {
                        Resolution = typeDef;
                        return;
                    }
                }
                throw new ParseFailedException("Failed to resolve external symbol " + TypeNamespace + "." + TypeName + " in external assembly " + assemblyRef.Name);
            }
            else
                throw new ParseFailedException("Strange resolution scope");
        }
    }
}
