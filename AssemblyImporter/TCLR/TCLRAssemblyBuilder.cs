using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;

namespace AssemblyImporter.TCLR
{
    public class TCLRAssemblyBuilder
    {
        public class BiDictionary<TV1, TV2>
        {
            private Dictionary<TV1, TV2> m_1to2;
            private Dictionary<TV2, TV1> m_2to1;

            public TV1 this[TV2 index]
            {
                get
                {
                    return m_2to1[index];
                }
            }

            public TV2 this[TV1 index]
            {
                get
                {
                    return m_1to2[index];
                }
            }

            public BiDictionary()
            {
                m_1to2 = new Dictionary<TV1, TV2>();
                m_2to1 = new Dictionary<TV2, TV1>();
            }

            public void Link(TV1 v1, TV2 v2)
            {
                if (m_1to2.ContainsKey(v1))
                    throw new Exception("Duplicate key");
                m_1to2[v1] = v2;
                if (m_2to1.ContainsKey(v2))
                    throw new Exception("Duplicate key");
                m_2to1[v2] = v1;
            }

            public bool HaveLink(TV1 v1)
            {
                return m_1to2.ContainsKey(v1);
            }

            public bool HaveLink(TV2 v2)
            {
                return m_2to1.ContainsKey(v2);
            }
        }

        public class ImportedObjectSet<TStandard, TCompact>
            where TCompact : TCLRMetaItem, new()
            where TStandard : CLRTableRow
        {
            private List<TCompact> m_compactVersion;
            private BiDictionary<TStandard, TCompact> m_biMap;
            private int m_numConverted;
            private TCLRAssemblyBuilder m_builder;

            public delegate void ConvertDelegate(TCompact cpt, TStandard std);

            public ImportedObjectSet(TCLRAssemblyBuilder builder)
            {
                m_compactVersion = new List<TCompact>();
                m_biMap = new BiDictionary<TStandard, TCompact>();
                m_numConverted = 0;
                m_builder = builder;
            }

            public TCompact Add(TStandard std)
            {
                TCompact cpt = new TCompact();
                cpt.RowNumber = (uint)m_compactVersion.Count;
                m_compactVersion.Add(cpt);
                m_biMap.Link(std, cpt);

                m_builder.ImportLinkedAttributes(std);
                
                return cpt;
            }

            public TCompact Index(TStandard std)
            {
                if (m_biMap.HaveLink(std))
                    return m_biMap[std];
                return Add(std);
            }

            public bool ConvertAny(ConvertDelegate convertDelegate)
            {
                bool convertedAny = false;
                while (m_numConverted < m_compactVersion.Count)
                {
                    TCompact cpt = m_compactVersion[m_numConverted];
                    TStandard std = m_biMap[cpt];
                    convertDelegate(cpt, std);
                    m_numConverted++;
                    convertedAny = true;
                }
                return convertedAny;
            }

            public int Count
            {
                get
                {
                    return m_compactVersion.Count;
                }
            }

            public void WriteAll(BinaryWriter writer)
            {
                foreach (TCompact cpt in m_compactVersion)
                    cpt.Write(writer);
            }
        }

        ImportedObjectSet<CLRTypeDefRow, TCLRTypeDef> m_typeDefs;
        ImportedObjectSet<CLRTypeRefRow, TCLRTypeRef> m_typeRefs;
        ImportedObjectSet<CLRTypeSpecRow, TCLRTypeSpec> m_typeSpecs;
        ImportedObjectSet<CLRFieldRow, TCLRFieldDef> m_fieldDefs;
        ImportedObjectSet<CLRMemberRefRow, TCLRFieldRef> m_fieldRefs;
        ImportedObjectSet<CLRMethodDefRow, TCLRMethodDef> m_methodDefs;
        ImportedObjectSet<CLRMemberRefRow, TCLRMethodRef> m_methodRefs;
        ImportedObjectSet<CLRCustomAttributeRow, TCLRAttribute> m_attributes;

        private Dictionary<CLRMethodDefRow, CLRTypeDefRow> m_methodOwners;
        private Dictionary<CLRFieldRow, CLRTypeDefRow> m_fieldOwners;
        private Dictionary<string, TCLRDataType> m_knownDataTypes;

        private BinaryBlobRepository m_stringRepo;
        private BinaryBlobRepository m_sigRepo;
        private BinaryBlobRepository m_byteCodeRepo;
        private CLRMetaDataTables m_tables;
        private TCLRVersion m_version;
        private TCLRString m_assemblyName;

        private bool m_bigEndian;

        public void InitTable<TSource, TDest>(CLRMetaDataTables tables, CLRMetaDataTables.TableIndex tableIndex, out TDest[] resultArray, out BiDictionary<TSource, TDest> resultDict)
            where TSource : CLRTableRow
            where TDest : TCLRMetaItem, new()
        {
            BiDictionary<TSource, TDest> dict = new BiDictionary<TSource, TDest>();
            resultDict = dict;

            ICLRTable table = tables.GetTable((int)tableIndex);
            TDest[] arr = new TDest[table.NumRows];
            for (uint i = 0; i < table.NumRows; i++)
            {
                TDest destItem = new TDest();
                destItem.RowNumber = i;
                arr[i] = destItem;
                dict.Link((TSource)table.GetRow(i), destItem);
            }
            resultArray = arr;
        }

        public void ImportAssembly(CLR.CLRAssembly assembly, bool bigEndian)
        {
            CLRMetaData metaData = assembly.MetaData;
            CLRMetaDataTables tables = metaData.MetaDataTables;

            m_stringRepo = new BinaryBlobRepository();
            m_sigRepo = new BinaryBlobRepository();
            m_byteCodeRepo = new BinaryBlobRepository();
            m_bigEndian = bigEndian;

            m_methodOwners = new Dictionary<CLRMethodDefRow, CLRTypeDefRow>();
            m_fieldOwners = new Dictionary<CLRFieldRow, CLRTypeDefRow>();
            m_knownDataTypes = new Dictionary<string,TCLRDataType>();

            m_typeDefs = new ImportedObjectSet<CLRTypeDefRow, TCLRTypeDef>(this);
            m_typeRefs = new ImportedObjectSet<CLRTypeRefRow, TCLRTypeRef>(this);
            m_typeSpecs = new ImportedObjectSet<CLRTypeSpecRow, TCLRTypeSpec>(this);
            m_fieldDefs = new ImportedObjectSet<CLRFieldRow, TCLRFieldDef>(this);
            m_fieldRefs = new ImportedObjectSet<CLRMemberRefRow, TCLRFieldRef>(this);
            m_methodDefs = new ImportedObjectSet<CLRMethodDefRow, TCLRMethodDef>(this);
            m_methodRefs = new ImportedObjectSet<CLRMemberRefRow, TCLRMethodRef>(this);
            m_attributes = new ImportedObjectSet<CLRCustomAttributeRow, TCLRAttribute>(this);

            m_knownDataTypes.Add("System.Boolean", TCLRDataType.DATATYPE_BOOLEAN);
            m_knownDataTypes.Add("System.SByte", TCLRDataType.DATATYPE_I1);
            m_knownDataTypes.Add("System.Byte", TCLRDataType.DATATYPE_U1);
            m_knownDataTypes.Add("System.Char", TCLRDataType.DATATYPE_CHAR);
            m_knownDataTypes.Add("System.Int16", TCLRDataType.DATATYPE_I2);
            m_knownDataTypes.Add("System.UInt16", TCLRDataType.DATATYPE_U2);
            m_knownDataTypes.Add("System.Int32", TCLRDataType.DATATYPE_I4);
            m_knownDataTypes.Add("System.UInt32", TCLRDataType.DATATYPE_U4);
            m_knownDataTypes.Add("System.Single", TCLRDataType.DATATYPE_R4);
            m_knownDataTypes.Add("System.Int64", TCLRDataType.DATATYPE_I8);
            m_knownDataTypes.Add("System.UInt64", TCLRDataType.DATATYPE_U8);
            m_knownDataTypes.Add("System.Double", TCLRDataType.DATATYPE_R8);
            m_knownDataTypes.Add("System.DateTime", TCLRDataType.DATATYPE_DATETIME);
            m_knownDataTypes.Add("System.TimeSpan", TCLRDataType.DATATYPE_TIMESPAN);
            m_knownDataTypes.Add("System.String", TCLRDataType.DATATYPE_STRING);
            m_knownDataTypes.Add("System.Object", TCLRDataType.DATATYPE_OBJECT);
            m_knownDataTypes.Add("System.Array", TCLRDataType.DATATYPE_SZARRAY);

            m_tables = tables;

            {
                CLRAssemblyRow assemblyRow = (CLRAssemblyRow)(tables.GetTable(CLRMetaDataTables.TableIndex.Assembly).GetRow(0));
                m_version.iBuildNumber = assemblyRow.BuildNumber;
                m_version.iMajorVersion = assemblyRow.MajorVersion;
                m_version.iMinorVersion = assemblyRow.MinorVersion;
                m_version.iRevisionNumber = assemblyRow.RevisionNumber;

                m_assemblyName = new TCLRString(IndexString(assemblyRow.Name));
            }

            {
                ICLRTable typeDefs = tables.GetTable((int)CLRMetaDataTables.TableIndex.TypeDef);
                for (uint i = 0; i < typeDefs.NumRows; i++)
                {
                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefs.GetRow(i);
                    foreach (CLRMethodDefRow methodDef in typeDef.MethodDefs)
                        m_methodOwners[methodDef] = typeDef;
                    foreach (CLRFieldRow fieldDef in typeDef.Fields)
                        m_fieldOwners[fieldDef] = typeDef;
                }
            }

            // Seed all type defs
            {
                ICLRTable typeDefs = tables.GetTable((int)CLRMetaDataTables.TableIndex.TypeDef);
                for (uint i = 0; i < typeDefs.NumRows; i++)
                {
                    CLRTypeDefRow typeDef = (CLRTypeDefRow)typeDefs.GetRow(i);
                    m_typeDefs.Index(typeDef);
                }
            }


            while (true)
            {
                bool worked = false;
                worked = m_typeDefs.ConvertAny(ImportTypeDef) || worked;
                worked = m_typeRefs.ConvertAny(ImportTypeRef) || worked;
                worked = m_typeSpecs.ConvertAny(ImportTypeSpec) || worked;
                worked = m_fieldDefs.ConvertAny(ImportFieldDef) || worked;
                worked = m_fieldRefs.ConvertAny(ImportFieldRef) || worked;
                worked = m_methodDefs.ConvertAny(ImportMethodDef) || worked;
                worked = m_methodRefs.ConvertAny(ImportMethodRef) || worked;
                worked = m_attributes.ConvertAny(ImportAttribute) || worked;

                if (!worked)
                    break;
            }

            // Need to figure out:
            // Resources
            // ResourcesData
            // Strings (index or offset?)
            // Signatures
            // ByteCode
            // ResourcesFiles
            // EOA
        }

        private void ImportAssemblyRef(TCLRAssemblyRef tAssm, CLRAssemblyRefRow assm)
        {
            throw new NotImplementedException();
        }

        private void ImportTypeRef(TCLRTypeRef tTRef, CLRTypeRefRow tref)
        {
            throw new NotImplementedException();
        }

        private static void GetTypeDefParent(CLRTypeDefRow typeDef, out string typeNamespace, out string typeName)
        {
            CLRTableRow extends = typeDef.Extends;
            if (extends is CLRTypeDefRow)
            {
                CLRTypeDefRow extendsDef = (CLRTypeDefRow)extends;
                typeNamespace = extendsDef.TypeNamespace;
                typeName = extendsDef.TypeName;
            }
            else if (extends is CLRTypeRefRow)
            {
                CLRTypeRefRow extendsRef = (CLRTypeRefRow)extends;
                typeNamespace = extendsRef.TypeNamespace;
                typeName = extendsRef.TypeName;
            }
            else
            {
                typeNamespace = null;
                typeName = null;
            }
        }

        private static bool MethodIsFinalizer(CLRMethodDefRow mdef)
        {
            if (mdef.Name != "Finalize")
                return false;

            if (mdef.Signature.ParamTypes != null)
                return false;
            if (mdef.Signature.RetType.Type.BasicType != CLRSigType.ElementType.VOID)
                throw new NotSupportedException("Finalize returns a value?");

            return true;
        }

        private void ImportTypeDef(TCLRTypeDef tTDef, CLRTypeDefRow tdef)
        {
            uint flags = 0;
            switch (tdef.Visibility)
            {
                case CLRTypeDefRow.TypeVisibility.NotPublic:
                    flags |= TCLRTypeDef.TD_Scope_NotPublic;
                    break;
                case CLRTypeDefRow.TypeVisibility.Public:
                    flags |= TCLRTypeDef.TD_Scope_Public;
                    break;
                case CLRTypeDefRow.TypeVisibility.NestedPublic:
                    flags |= TCLRTypeDef.TD_Scope_NestedPublic;
                    break;
                case CLRTypeDefRow.TypeVisibility.NestedPrivate:
                    flags |= TCLRTypeDef.TD_Scope_NestedPrivate;
                    break;
                case CLRTypeDefRow.TypeVisibility.NestedFamily:
                    flags |= TCLRTypeDef.TD_Scope_NestedFamily;
                    break;
                case CLRTypeDefRow.TypeVisibility.NestedAssembly:
                    flags |= TCLRTypeDef.TD_Scope_NestedAssembly;
                    break;
                case CLRTypeDefRow.TypeVisibility.NestedFamilyAndAssembly:
                    flags |= TCLRTypeDef.TD_Scope_NestedFamANDAssem;
                    break;
                case CLRTypeDefRow.TypeVisibility.NestedFamilyOrAssembly:
                    flags |= TCLRTypeDef.TD_Scope_NestedFamORAssem;
                    break;
                default:
                    throw new NotSupportedException("Unknown type visibility");
            }

            if (tdef.IsSerializable)
                flags |= TCLRTypeDef.TD_Serializable;

            TCLRDataType simpleType;
            if (tdef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
            {
                flags |= TCLRTypeDef.TD_Semantics_Interface;
                simpleType = TCLRDataType.DATATYPE_CLASS;
            }
            else if (tdef.Semantics == CLRTypeDefRow.TypeSemantics.Class)
            {
                string parentNS, parentT;
                GetTypeDefParent(tdef, out parentNS, out parentT);
                if (parentNS == "System" && parentT == "ValueType")
                {
                    flags |= TCLRTypeDef.TD_Semantics_ValueType;
                    simpleType = TCLRDataType.DATATYPE_VALUETYPE;
                }
                else if (parentNS == "System" && parentT == "Enum")
                {
                    flags |= TCLRTypeDef.TD_Semantics_Enum;
                    simpleType = TCLRDataType.DATATYPE_VALUETYPE;
                }
                else
                {
                    flags |= TCLRTypeDef.TD_Semantics_Class;
                    simpleType = TCLRDataType.DATATYPE_CLASS;
                }

                if (parentNS == "System" && parentT == "Delegate")
                    flags |= TCLRTypeDef.TD_Delegate;
                if (parentNS == "System" && parentT == "MulticastDelegate")
                    flags |= TCLRTypeDef.TD_MulticastDelegate;
            }
            else
                throw new NotSupportedException();

            if (tdef.IsAbstract)
                flags |= TCLRTypeDef.TD_Abstract;
            if (tdef.IsSealed)
                flags |= TCLRTypeDef.TD_Sealed;
            if (tdef.IsSpecialName)
                flags |= TCLRTypeDef.TD_SpecialName;
            if (tdef.TypeNamespace == "System" && tdef.TypeName == "MulticastDelegate")
                flags |= TCLRTypeDef.TD_MulticastDelegate;
            if (tdef.TypeNamespace == "System" && tdef.TypeName == "Delegate")
                flags |= TCLRTypeDef.TD_Delegate;

            if (tdef.IsBeforeFieldInit)
                flags |= TCLRTypeDef.TD_BeforeFieldInit;
            if (tdef.HasSecurity)
                flags |= TCLRTypeDef.TD_HasSecurity;

            if (HasAttributes(tdef))
                flags |= TCLRTypeDef.TD_HasAttributes;
            if (tdef.MethodDefs != null)
            {
                foreach (CLRMethodDefRow methodDef in tdef.MethodDefs)
                {
                    if (MethodIsFinalizer(methodDef))
                    {
                        flags |= TCLRTypeDef.TD_HasFinalizer;
                        break;
                    }
                }
            }

            if (tdef.Fields != null)
            {
                tTDef.sFields_First = new TCLRIndex((uint)m_fieldDefs.Count, "static field");
                uint numStaticFields = 0;
                foreach (CLRFieldRow field in tdef.Fields)
                {
                    if (field.Static)
                    {
                        m_fieldDefs.Add(field);
                        numStaticFields++;
                    }
                }

                tTDef.iFields_First = new TCLRIndex((uint)m_fieldDefs.Count, "instance field");
                uint numInstanceFields = 0;
                foreach (CLRFieldRow field in tdef.Fields)
                {
                    if (!field.Static)
                    {
                        m_fieldDefs.Add(field);
                        numInstanceFields++;
                    }
                }

                if (numStaticFields > 255)
                    throw new OverflowException("Too many static fields");
                if (numInstanceFields > 255)
                    throw new OverflowException("Too many static fields");

                tTDef.iFields_Num = (byte)numInstanceFields;
                tTDef.sFields_Num = (byte)numStaticFields;
            }

            tTDef.name = new TCLRString(IndexString(tdef.TypeName));
            tTDef.nameSpace = new TCLRString(IndexString(tdef.TypeNamespace));

            if (tdef.Extends == null)
                tTDef.extends = TCLRIndex.Empty;
            else
            {
                uint tableIndex, tableRow;
                if (tdef.Extends is CLRTypeDefRow)
                {
                    tableIndex = 0x0000;
                    tableRow = m_typeDefs.Index((CLRTypeDefRow)tdef.Extends).RowNumber;
                }
                else if (tdef.Extends is CLRTypeRefRow)
                {
                    tableIndex = 0x8000;
                    tableRow = m_typeRefs.Index((CLRTypeRefRow)tdef.Extends).RowNumber;
                }
                else
                    throw new NotSupportedException("Unsupported extension (generic?)");    // CLARITYTODO
            }

            bool haveEnclosingType = false;
            {
                ICLRTable table = m_tables.GetTable(CLRMetaDataTables.TableIndex.NestedClass);
                for (uint i = 0; i < table.NumRows; i++)
                {
                    CLRNestedClassRow nestedClass = (CLRNestedClassRow)table.GetRow(i);
                    if (nestedClass.NestedClass == tdef)
                    {
                        tTDef.enclosingType = new TCLRIndex(m_typeDefs.Index(nestedClass.EnclosingClass).RowNumber, "type def");
                        haveEnclosingType = true;
                    }
                }
            }

            if (!haveEnclosingType)
                tTDef.enclosingType = TCLRIndex.Empty;

            {
                ICLRTable table = m_tables.GetTable(CLRMetaDataTables.TableIndex.InterfaceImpl);
                List<CLRTableRow> interfaces = new List<CLRTableRow>();
                for (uint i = 0; i < table.NumRows; i++)
                {
                    CLRInterfaceImplRow interfaceImpl = (CLRInterfaceImplRow)table.GetRow(i);
                    if (interfaceImpl.Class == tdef)
                        interfaces.Add(interfaceImpl.Interface);
                }

                if (interfaces.Count == 0)
                    tTDef.interfaces = TCLRSig.Empty;
                else
                {
                    if (interfaces.Count > 255)
                        throw new OverflowException("Too many implemented interfaces");

                    using (TCLRSignatureBuilder sigBuilder = new TCLRSignatureBuilder(this, m_bigEndian))
                    {
                        sigBuilder.WriteByte((byte)interfaces.Count);
                        foreach (CLRTableRow intf in interfaces)
                            sigBuilder.WriteStructuredType(new CLRSigTypeStructured(CLRSigType.ElementType.CLASS, intf), false);
                        tTDef.interfaces = new TCLRSig(m_sigRepo.Index(new BinaryBlob(sigBuilder.Finish())));
                    }
                }
            }

            if (tdef.MethodDefs != null)
            {
                tTDef.methods_First = new TCLRIndex((uint)m_methodDefs.Count, "method def");

                List<CLRMethodDefRow>[] methodBuckets = new List<CLRMethodDefRow>[3];
                for (int i = 0; i < 3; i++)
                    methodBuckets[i] = new List<CLRMethodDefRow>();

                foreach (CLRMethodDefRow methodDef in tdef.MethodDefs)
                {
                    int bucket;
                    if (methodDef.Virtual)
                        bucket = 0;
                    else if (methodDef.Static)
                        bucket = 2;
                    else
                        bucket = 1;

                    methodBuckets[bucket].Add(methodDef);
                }

                for (int i = 0; i < 3; i++)
                {
                    List<CLRMethodDefRow> bucket = methodBuckets[i];
                    if (bucket.Count > 255)
                        throw new OverflowException("Too many method defs");
                    foreach (CLRMethodDefRow methodDef in bucket)
                        m_methodDefs.Add(methodDef);
                }
                tTDef.vMethods_Num = (byte)methodBuckets[0].Count;
                tTDef.iMethods_Num = (byte)methodBuckets[1].Count;
                tTDef.sMethods_Num = (byte)methodBuckets[2].Count;
            }

            {
                TCLRDataType knownDataType;
                if (m_knownDataTypes.TryGetValue(tdef.TypeNamespace + "." + tdef.TypeName, out knownDataType))
                    tTDef.dataType = (byte)knownDataType;
                else
                    tTDef.dataType = (byte)simpleType;
            }

            tTDef.flags = (ushort)flags;
        }

        private void ImportFieldDef(TCLRFieldDef tFDef, CLRFieldRow fdef)
        {
            tFDef.name = new TCLRString(IndexString(fdef.Name));

            using (TCLRSignatureBuilder sigBuilder = new TCLRSignatureBuilder(this, m_bigEndian))
            {
                // CLARITYTODO: Is this correct?
                sigBuilder.WriteByte((byte)TCLRCallingConvention.PIMAGE_CEE_CS_CALLCONV_FIELD);
                sigBuilder.WriteType(fdef.Signature.Type);
                tFDef.sig = new TCLRSig(m_sigRepo.Index(new BinaryBlob(sigBuilder.Finish())));
            }

            uint flags = 0;

            switch (fdef.FieldAccess)
            {
                case CLRFieldRow.TypeFieldAccess.Assembly:
                    flags |= TCLRFieldDef.FD_Scope_Assembly;
                    break;
                case CLRFieldRow.TypeFieldAccess.Family:
                    flags |= TCLRFieldDef.FD_Scope_Family;
                    break;
                case CLRFieldRow.TypeFieldAccess.FamilyAndAssembly:
                    flags |= TCLRFieldDef.FD_Scope_FamANDAssem;
                    break;
                case CLRFieldRow.TypeFieldAccess.FamilyOrAssembly:
                    flags |= TCLRFieldDef.FD_Scope_FamORAssem;
                    break;
                case CLRFieldRow.TypeFieldAccess.Private:
                    flags |= TCLRFieldDef.FD_Scope_Private;
                    break;
                case CLRFieldRow.TypeFieldAccess.Public:
                    flags |= TCLRFieldDef.FD_Scope_Public;
                    break;
                default:
                    throw new NotSupportedException("Unsupported field scope");
            }

            // CLARITYHACK - Default values are only actually used for array init
            bool hasUsefulDefault = false;
            if (fdef.HasDefault)
            {
                CLRSigType ftype = fdef.Signature.Type;
                if (ftype is CLRSigTypeSZArray)
                    hasUsefulDefault = true;
            }

            if (fdef.NotSerialized)
                flags |= TCLRFieldDef.FD_NotSerialized;
            if (fdef.Static)
                flags |= TCLRFieldDef.FD_Static;
            if (fdef.InitOnly)
                flags |= TCLRFieldDef.FD_InitOnly;
            if (fdef.Literal)
                flags |= TCLRFieldDef.FD_Literal;
            if (fdef.SpecialName)
                flags |= TCLRFieldDef.FD_SpecialName;
            if (hasUsefulDefault)
                flags |= TCLRFieldDef.FD_HasDefault;
            if (fdef.HasFieldRVA)
                flags |= TCLRFieldDef.FD_HasFieldRVA;

            // CLARITYTODO: NoReflection, from custom attrib [System.Reflection.FieldNoReflection]
            if (HasAttributes(fdef))
                flags |= TCLRFieldDef.FD_HasAttributes;

            if (hasUsefulDefault)
                tFDef.defaultValue = ImportFieldDefaultValue(fdef);

            tFDef.flags = (ushort)flags;
        }

        private TCLRSig ImportFieldDefaultValue(CLRFieldRow fdef)
        {
            ICLRTable constTable = m_tables.GetTable(CLRMetaDataTables.TableIndex.Constant);
            for (uint i = 0; i < constTable.NumRows; i++)
            {
                CLRConstantRow constRow = (CLRConstantRow)constTable.GetRow(i);
                if (constRow.Parent == fdef)
                {
                    CLRTypeDefRow owner = m_fieldOwners[fdef];
                    throw new NotImplementedException();
                }
            }
            throw new Exception("Missing default");
        }

        private void ImportFieldRef(TCLRFieldRef tFRef, CLRMemberRefRow fref)
        {
            tFRef.name = ImportString(fref.Name);
            tFRef.container = new TCLRIndex(m_fieldRefs.Index(fref).RowNumber, "FieldRef");
            tFRef.sig = new TCLRSig(m_sigRepo.Index(ImportFieldRefSig(fref.FieldSig)));
        }

        private void ImportMethodRef(TCLRMethodRef tMRef, CLRMemberRefRow mref)
        {
            tMRef.name = ImportString(mref.Name);
            tMRef.container = new TCLRIndex(m_methodRefs.Index(mref).RowNumber, "MethodRef");
            tMRef.sig = new TCLRSig(m_sigRepo.Index(ImportMethodRefSig(mref.MethodSig)));
        }

        private void ImportMethodDef(TCLRMethodDef tMDef, CLRMethodDefRow mdef)
        {
            uint flags = 0;
            switch (mdef.MemberAccess)
            {
                case CLRMethodDefRow.MethodMemberAccess.Assembly:
                    flags |= TCLRMethodDef.MD_Scope_Assem;
                    break;
                case CLRMethodDefRow.MethodMemberAccess.Private:
                    flags |= TCLRMethodDef.MD_Scope_Private;
                    break;
                case CLRMethodDefRow.MethodMemberAccess.FamilyAndAssembly:
                    flags |= TCLRMethodDef.MD_Scope_FamANDAssem;
                    break;
                case CLRMethodDefRow.MethodMemberAccess.FamilyOrAssembly:
                    flags |= TCLRMethodDef.MD_Scope_FamORAssem;
                    break;
                case CLRMethodDefRow.MethodMemberAccess.Public:
                    flags |= TCLRMethodDef.MD_Scope_Public;
                    break;
                case CLRMethodDefRow.MethodMemberAccess.Family:
                    flags |= TCLRMethodDef.MD_Scope_Family;
                    break;
                default:
                    throw new ParseFailedException("Unsupported method def access");
            }

            if (mdef.Static)
                flags |= TCLRMethodDef.MD_Static;
            if (mdef.Final)
                flags |= TCLRMethodDef.MD_Final;
            if (mdef.Virtual)
                flags |= TCLRMethodDef.MD_Virtual;
            if (mdef.HideBySig)
                flags |= TCLRMethodDef.MD_HideBySig;

            switch (mdef.VtableLayout)
            {
                case CLRMethodDefRow.MethodVtableLayout.NewSlot:
                    flags |= TCLRMethodDef.MD_NewSlot;
                    break;
                case CLRMethodDefRow.MethodVtableLayout.ReuseSlot:
                    flags |= TCLRMethodDef.MD_ReuseSlot;
                    break;
                default:
                    throw new ParseFailedException("Unknown vtable layout");
            }

            if (mdef.Abstract)
                flags |= TCLRMethodDef.MD_Abstract;
            if (mdef.SpecialName)
            {
                flags |= TCLRMethodDef.MD_SpecialName;
                if (mdef.Name == ".ctor")
                    flags |= TCLRMethodDef.MD_Constructor;
                else if (mdef.Name == ".cctor")
                    flags |= TCLRMethodDef.MD_StaticConstructor;
                else if (mdef.Name == "Finalize")
                {
                    flags |= TCLRMethodDef.MD_Finalizer;
                    throw new NotImplementedException();
                }
            }

            // CLARITYTODO: NativeProfiled?

            bool isDelegateMethod = false;

            {
                CLRTypeDefRow owner = FindMethodOwner(mdef);
                if (owner.Extends != null)
                {
                    if (owner.Extends is CLRTypeDefRow)
                    {
                        CLRTypeDefRow typeDef = (CLRTypeDefRow)owner.Extends;
                        if (typeDef.TypeNamespace == "System" && (typeDef.TypeName == "Delegate" || typeDef.TypeName == "MulticastDelegate"))
                            isDelegateMethod = true;
                    }
                    else if (owner.Extends is CLRTypeRefRow)
                    {
                        CLRTypeDefRow typeRef = (CLRTypeDefRow)owner.Extends;
                        if (typeRef.TypeNamespace == "System" && (typeRef.TypeName == "Delegate" || typeRef.TypeName == "MulticastDelegate"))
                            isDelegateMethod = true;
                    }
                }
            }

            if (isDelegateMethod)
            {
                if (mdef.Name == ".ctor")
                    flags |= TCLRMethodDef.MD_DelegateConstructor;
                else if (mdef.Name == "BeginInvoke")
                    flags |= TCLRMethodDef.MD_DelegateBeginInvoke;
                else if (mdef.Name == "Invoke")
                    flags |= TCLRMethodDef.MD_DelegateInvoke;
                else if (mdef.Name == "EndInvoke")
                    flags |= TCLRMethodDef.MD_DelegateEndInvoke;
            }

            if (mdef.Synchronized)
            {
                if (mdef.Static)
                    flags |= TCLRMethodDef.MD_GloballySynchronized;
                else
                    flags |= TCLRMethodDef.MD_Synchronized;
            }

            // CLARITYTODO: EntryPoint

            if (mdef.Method != null && mdef.Method.Sections != null && mdef.Method.Sections.Length > 0)
                flags |= TCLRMethodDef.MD_HasExceptionHandlers;

            {
                bool haveAttribs = false;
                ICLRTable attribs = m_tables.GetTable(CLRMetaDataTables.TableIndex.CustomAttribute);
                for (uint i = 0; i < attribs.NumRows; i++)
                {
                    CLRCustomAttributeRow attribRow = (CLRCustomAttributeRow)attribs.GetRow(i);
                    m_attributes.Index(attribRow);
                    haveAttribs = true;
                }
                if (haveAttribs)
                    flags |= TCLRMethodDef.MD_HasAttributes;
            }

            tMDef.name = ImportString(mdef.Name);
            CLRTypeDefRow ownerType = m_methodOwners[mdef];
            if (mdef.Method != null)
                tMDef.RVA = new TCLROffset(m_byteCodeRepo.Index(ImportByteCode(mdef)), "method byte code");
            else
                tMDef.RVA = TCLROffset.Empty;

            tMDef.flags = flags;

            tMDef.retVal = ConvertMethodRetVal(mdef.Signature.RetType);
            // CLARITYTODO: Figure out if numArgs includes vararg or not

            {
                int numArgs = mdef.Signature.ParamTypes.Length;
                if (mdef.Signature.HasThis)
                    numArgs++;
                if (numArgs > 255)
                    throw new OverflowException("Method with too many parameters");
                tMDef.numArgs = (byte)numArgs;
            }
            if (mdef.Method != null)
            {
                if (mdef.Method.LocalVarSig != null)
                {
                    int nVars = mdef.Method.LocalVarSig.LocalVars.Length;
                    if (mdef.Method.LocalVarSig.LocalVars.Length > 255)
                        throw new OverflowException("Method with too many parameters");
                    tMDef.numLocals = (byte)nVars;
                }
                if (mdef.Method.MaxStack > 255)
                    throw new OverflowException("Method uses too much stack");
                tMDef.lengthEvalStack = (byte)mdef.Method.MaxStack;
                if (tMDef.numLocals > 0)
                    tMDef.locals = new TCLRSig(m_sigRepo.Index(ImportLocalSignature(mdef.Method.LocalVarSig)));
            }
            if (mdef.Name == "GetTimeZoneOffset")
            {
                int bp = 0;
            }
            tMDef.sig = new TCLRSig(m_sigRepo.Index(ImportMethodDefSignature(mdef.Signature)));
        }

        private BinaryBlob ImportLocalSignature(CLRSigLocalVarSig localSig)
        {
            using (TCLRSignatureBuilder builder = new TCLRSignatureBuilder(this, m_bigEndian))
            {
                foreach (CLRSigLocalVar localVar in localSig.LocalVars)
                {
                    bool isByRef = false;
                    bool isPinned = false;
                    foreach (CLRSigConstraint constraint in localVar.Constraints)
                    {
                        if (constraint.ConstraintType == CLRSigConstraint.ConstraintTypeEnum.Pinned)
                            isPinned = true;
                        else
                            throw new NotSupportedException("Unsupported local var constraint");
                    }

                    if (localVar.VarKind == CLRSigLocalVar.LocalVarKind.ByRef)
                        isByRef = true;
                    else if (localVar.VarKind == CLRSigLocalVar.LocalVarKind.Default)
                    {
                    }
                    else
                        throw new NotSupportedException("Unsupported local var kind");

                    if (isPinned)
                        builder.WriteByte((byte)TCLRDataType.DATATYPE_TYPE_PINNED);
                    if (isByRef)
                        builder.WriteByte((byte)TCLRDataType.DATATYPE_BYREF);
                    builder.WriteType(localVar.Type);
                }
                return new BinaryBlob(builder.Finish());
            }
        }

        private BinaryBlob ImportMethodDefSignature(CLRSigMethodDefOrRefSig methodSig)
        {
            uint flags = 0;

            switch (methodSig.CallingConvention)
            {
                case CLRSigMethodDefOrRefSig.CallingConventionType.Default:
                    flags |= (uint)TCLRCallingConvention.PIMAGE_CEE_CS_CALLCONV_DEFAULT;
                    break;
                case CLRSigMethodDefOrRefSig.CallingConventionType.VarArg:
                    flags |= (uint)TCLRCallingConvention.PIMAGE_CEE_CS_CALLCONV_VARARG;
                    break;
                case CLRSigMethodDefOrRefSig.CallingConventionType.FastCall:
                case CLRSigMethodDefOrRefSig.CallingConventionType.Generic:
                case CLRSigMethodDefOrRefSig.CallingConventionType.StdCall:
                case CLRSigMethodDefOrRefSig.CallingConventionType.ThisCall:
                case CLRSigMethodDefOrRefSig.CallingConventionType.C:
                default:
                    throw new NotSupportedException();
            }

            if (methodSig.HasThis)
                flags |= (uint)TCLRCallingConvention.PIMAGE_CEE_CS_CALLCONV_HASTHIS;
            if (methodSig.ExplicitThis)
                flags |= (uint)TCLRCallingConvention.PIMAGE_CEE_CS_CALLCONV_EXPLICITTHIS;
            if (methodSig.NumGenericParameters > 0)
                throw new NotSupportedException("Generic calling convention methods are not supported");

            using (TCLRSignatureBuilder builder = new TCLRSignatureBuilder(this, m_bigEndian))
            {
                builder.WriteByte((byte)flags);
                if (methodSig.ParamTypes.Length > 255)
                    throw new OverflowException("Too many method paramters");
                builder.WriteByte((byte)methodSig.ParamTypes.Length);
                if (methodSig.RetType.TypeOfType != CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                    throw new NotSupportedException("Unusual method return type");
                builder.WriteType(methodSig.RetType.Type);
                foreach (CLRSigParamType ptype in methodSig.ParamTypes)
                {
                    if (ptype.TypeOfType == CLRSigParamOrRetType.TypeOfTypeEnum.ByRef)
                        builder.WriteByte((byte)TCLRDataType.DATATYPE_BYREF);
                    else if (ptype.TypeOfType != CLRSigParamOrRetType.TypeOfTypeEnum.Value)
                        throw new NotSupportedException("Unsupported param type");
                    builder.WriteType(ptype.Type);
                }
                return new BinaryBlob(builder.Finish());
            }
        }

        private byte ConvertMethodRetVal(CLRSigRetType retType)
        {
            // CLARITYHACK: TinyCLR only cares about return type for void checks and I4 promotion
            TCLRDataType dataType;

            switch (retType.Type.BasicType)
            {
                case CLRSigType.ElementType.VOID:
                    dataType = TCLRDataType.DATATYPE_VOID;
                    break;
                case CLRSigType.ElementType.BOOLEAN:
                    dataType = TCLRDataType.DATATYPE_BOOLEAN;
                    break;
                case CLRSigType.ElementType.CHAR:
                    dataType = TCLRDataType.DATATYPE_CHAR;
                    break;
                case CLRSigType.ElementType.I1:
                    dataType = TCLRDataType.DATATYPE_I1;
                    break;
                case CLRSigType.ElementType.I2:
                    dataType = TCLRDataType.DATATYPE_I2;
                    break;
                case CLRSigType.ElementType.I4:
                    dataType = TCLRDataType.DATATYPE_I4;
                    break;
                case CLRSigType.ElementType.I8:
                    dataType = TCLRDataType.DATATYPE_I8;
                    break;
                case CLRSigType.ElementType.R4:
                    dataType = TCLRDataType.DATATYPE_R4;
                    break;
                case CLRSigType.ElementType.R8:
                    dataType = TCLRDataType.DATATYPE_R8;
                    break;
                default:
                    dataType = TCLRDataType.DATATYPE_OBJECT;
                    break;
            }

            return (byte)dataType;
        }

        private BinaryBlob ImportByteCode(CLRMethodDefRow methodDef)
        {
            return new BinaryBlob(TCLRBytecodeBuilder.ImportMethod(methodDef, this, m_bigEndian));
        }

        private void ImportAttribute(TCLRAttribute tAttrib, CLRCustomAttributeRow attrib)
        {
            // CLARITYHACK - Only used by TinyCLR for binary serialization hints, not supported yet
            //throw new NotImplementedException();
        }

        private void ImportTypeSpec(TCLRTypeSpec tTypeSpec, CLRTypeSpecRow typeSpec)
        {
            using (TCLRSignatureBuilder builder = new TCLRSignatureBuilder(this, m_bigEndian))
            {
                builder.WriteType(typeSpec.Signature.Type);
                tTypeSpec.sig = new TCLRSig(m_sigRepo.Index(new BinaryBlob(builder.Finish())));
            }
        }

        public uint IndexString(string str)
        {
            byte[] baseBytes = System.Text.Encoding.UTF8.GetBytes(str);
            byte[] terminatedBytes = new byte[baseBytes.Length + 1];
            Array.Copy(baseBytes, 0, terminatedBytes, 0, baseBytes.Length);

            uint index = m_stringRepo.Index(new BinaryBlob(terminatedBytes));
            return index;
        }

        public uint IndexMethodDef(CLRMethodDefRow row)
        {
            return m_methodDefs.Index(row).RowNumber;
        }

        public uint IndexMethodRef(CLRMemberRefRow row)
        {
            return m_methodRefs.Index(row).RowNumber;
        }

        public uint IndexFieldRef(CLRMemberRefRow row)
        {
            return m_fieldRefs.Index(row).RowNumber;
        }

        public uint IndexTypeDef(CLRTypeDefRow row)
        {
            return m_typeDefs.Index(row).RowNumber;
        }

        public uint IndexTypeRef(CLRTypeRefRow row)
        {
            return m_typeRefs.Index(row).RowNumber;
        }

        public uint IndexTypeSpec(CLRTypeSpecRow row)
        {
            return m_typeSpecs.Index(row).RowNumber;
        }

        public uint IndexFieldDef(CLRFieldRow row)
        {
            return m_fieldDefs.Index(row).RowNumber;
        }

        public CLRTypeDefRow FindMethodOwner(CLRMethodDefRow row)
        {
            return m_methodOwners[row];
        }

        private TCLRString ImportString(string str)
        {
            return new TCLRString(IndexString(str));
        }

        private BinaryBlob ImportFieldRefSig(CLRSigFieldSig fieldSig)
        {
            throw new NotImplementedException();
        }

        private BinaryBlob ImportMethodRefSig(CLRSigMethodDefOrRefSig methodSig)
        {
            throw new NotImplementedException();
        }

        private bool HasAttributes(CLRTableRow rows)
        {
            ICLRTable table = m_tables.GetTable(CLRMetaDataTables.TableIndex.CustomAttribute);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRCustomAttributeRow attrib = (CLRCustomAttributeRow)table.GetRow(i);
                if (attrib.Parent == rows)
                    return true;
            }
            return false;
        }

        private void ImportLinkedAttributes(CLRTableRow rows)
        {
            ICLRTable table = m_tables.GetTable(CLRMetaDataTables.TableIndex.CustomAttribute);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRCustomAttributeRow attrib = (CLRCustomAttributeRow)table.GetRow(i);
                if (attrib.Parent == rows)
                    m_attributes.Index(attrib);
            }
        }

        public CLRTableRow FindType(string typeNamespace, string typeName)
        {
            {
                ICLRTable table = m_tables.GetTable(CLRMetaDataTables.TableIndex.TypeDef);
                for (uint i = 0; i < table.NumRows; i++)
                {
                    CLRTypeDefRow row = (CLRTypeDefRow)table.GetRow(i);
                    if (row.TypeNamespace == typeNamespace && row.TypeName == typeName)
                        return row;
                }
            }
            {
                ICLRTable table = m_tables.GetTable(CLRMetaDataTables.TableIndex.TypeRef);
                for (uint i = 0; i < table.NumRows; i++)
                {
                    CLRTypeRefRow row = (CLRTypeRefRow)table.GetRow(i);
                    if (row.TypeNamespace == typeNamespace && row.TypeName == typeName)
                        return row;
                }
            }

            return null;
        }

        public void Export(Stream stream)
        {
            TCLRAssembly assm = new TCLRAssembly();
            using (MemoryStream headerMS = new MemoryStream())
            {
                BinaryWriter headerWriter;
                if (m_bigEndian)
                    headerWriter = new BigEndianBinaryWriter(headerMS);
                else
                    headerWriter = new BinaryWriter(headerMS);
                assm.Write(headerWriter);

                long headerEndPos = headerMS.Position;

                byte[] assmBytes;
                using (MemoryStream bodyMS = new MemoryStream())
                {
                    BinaryWriter bodyWriter;
                    if (m_bigEndian)
                        bodyWriter = new BigEndianBinaryWriter(bodyMS);
                    else
                        bodyWriter = new BinaryWriter(bodyMS);

                    int nTables = (int)TCLRTablesEnum.TBL_Max;
                    for (int i = 0; i < nTables; i++)
                    {
                        int nPadding = 0;
                        while (bodyMS.Position % 4 != 0)
                        {
                            nPadding++;
                            bodyWriter.Write((byte)0);
                        }
                        assm.paddingOfTables[i] = (byte)nPadding;
                        assm.startOfTables[i] = new TCLROffsetLong((uint)(bodyMS.Position + headerMS.Length));
                    
                        switch ((TCLRTablesEnum)i)
                        {
                            case TCLRTablesEnum.TBL_AssemblyRef:
                                // CLARITYTODO
                                break;
                            case TCLRTablesEnum.TBL_TypeRef:
                                m_typeRefs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_FieldRef:
                                m_fieldRefs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_MethodRef:
                                m_methodRefs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_TypeDef:
                                m_typeDefs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_FieldDef:
                                m_fieldDefs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_MethodDef:
                                m_methodDefs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_Attributes:
                                m_attributes.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_TypeSpec:
                                m_typeSpecs.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_Resources:
                                // CLARITYTODO
                                break;
                            case TCLRTablesEnum.TBL_ResourcesData:
                                // CLARITYTODO
                                break;
                            case TCLRTablesEnum.TBL_Strings:
                                m_stringRepo.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_Signatures:
                                m_sigRepo.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_ByteCode:
                                m_byteCodeRepo.WriteAll(bodyWriter);
                                break;
                            case TCLRTablesEnum.TBL_ResourcesFiles:
                                // CLARITYTODO
                                break;
                            case TCLRTablesEnum.TBL_EndOfAssembly:
                                break;
                            default:
                                throw new ArgumentException();
                        }
                    }
                    assmBytes = bodyMS.ToArray();
                }

                uint flags = 0;
                if (m_bigEndian)
                    flags |= TCLRAssembly.c_Flags_BigEndian;

                assm.headerCRC = 0;
                assm.assemblyCRC = CRC32.Hash(assmBytes);
                assm.flags = flags;

                assm.nativeMethodsChecksum = 0; // CLARITYTODO
                assm.patchEntryOffset = 0xffffffff;

                assm.version = m_version;
                assm.assemblyName = m_assemblyName;

                assm.stringTableVersion = 1;
                assm.numOfPatchedMethods = 0;

                headerMS.Seek(0, SeekOrigin.Begin);
                assm.Write(headerWriter);

                assm.headerCRC = CRC32.Hash(headerMS.ToArray());
                headerMS.Seek(0, SeekOrigin.Begin);
                assm.Write(headerWriter);

                headerMS.WriteTo(stream);
                stream.Write(assmBytes, 0, assmBytes.Length);
            }
        }
    }
}
