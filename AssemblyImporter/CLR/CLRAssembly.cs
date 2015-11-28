using System;
using AssemblyImporter.PE;

namespace AssemblyImporter.CLR
{
    public class CLRAssembly
    {
        public CLRMetaData MetaData { get; private set; }

        public CLRAssembly(StreamParser parser)
        {
            PEAssembly peAssembly = new PE.PEAssembly(parser);

            RvaAndSize importAddressDD = peAssembly.DataDirectory[12];
            RvaAndSize importTableDD = peAssembly.DataDirectory[1];
            RvaAndSize clrHeaderDD = peAssembly.DataDirectory[14];

            PE.PESectionHeader textSection = null;
            for (int i = 0; i < peAssembly.SectionHeaders.Length; i++)
            {
                PE.PESectionHeader sheader = peAssembly.SectionHeaders[i];
                if (sheader.Name == ".text")
                    textSection = sheader;
            }
            if (textSection == null)
                throw new ParseFailedException("PE missing text section");

            parser.Seek(textSection.PointerToRawData);

            CLRHeader clrHeader = new CLRHeader(parser);

            // Parse metadata
            parser.Seek(peAssembly.ResolveRva(clrHeader.MetaData.RelativeVirtualAddress));

            MetaData = new CLRMetaData(parser, this);

            // Parse methods
            ICLRTable methodDefTable = MetaData.MetaDataTables.GetTable((int)CLRMetaDataTables.TableIndex.MethodDef);
            for (uint i = 0; i < methodDefTable.NumRows; i++)
            {
                CLRMethodDefRow methodDef = (CLRMethodDefRow)methodDefTable.GetRow(i);
                if (methodDef.RVA != 0)
                {
                    parser.Seek(peAssembly.ResolveRva(methodDef.RVA));
                    methodDef.DigestMethod(MetaData.MetaDataTables.MetaDataParser);
                }
            }
        }

        private void ResolveTable(CLRMetaDataTables.TableIndex tableIndex, CLRAssemblyCollection assemblies, ref bool resolvedAny, ref bool resolvedAll)
        {
            CLRMetaDataTables tables = MetaData.MetaDataTables;
            ICLRTable typeRefTable = tables.GetTable(tableIndex);
            uint numRows = typeRefTable.NumRows;
            for (uint i = 0; i < numRows; i++)
            {
                ICLRResolvable typeRef = (ICLRResolvable)typeRefTable.GetRow(i);
                if (!typeRef.IsResolved)
                {
                    typeRef.Resolve(assemblies);
                    if (typeRef.IsResolved)
                        resolvedAny = true;
                    else
                        resolvedAll = false;
                }
            }
        }

        public void Resolve(CLRAssemblyCollection assemblies, ref bool resolvedAny, ref bool resolvedAll)
        {
            ResolveTable(CLRMetaDataTables.TableIndex.TypeSpec, assemblies, ref resolvedAny, ref resolvedAll);
            ResolveTable(CLRMetaDataTables.TableIndex.AssemblyRef, assemblies, ref resolvedAny, ref resolvedAll);
            ResolveTable(CLRMetaDataTables.TableIndex.ModuleRef, assemblies, ref resolvedAny, ref resolvedAll);
            ResolveTable(CLRMetaDataTables.TableIndex.TypeRef, assemblies, ref resolvedAny, ref resolvedAll);
            ResolveTable(CLRMetaDataTables.TableIndex.TypeSpec, assemblies, ref resolvedAny, ref resolvedAll);
        }

        public void ParseCustomAttributes(CLRAssemblyCollection assemblies)
        {
            ICLRTable table = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.CustomAttribute);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRCustomAttributeRow ca = (CLRCustomAttributeRow)table.GetRow(i);
                ca.Resolve();
            }
        }

        public void ResolveNestedClasses()
        {
            ICLRTable table = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.NestedClass);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRNestedClassRow nc = (CLRNestedClassRow)table.GetRow(i);
                nc.EnclosingClass.AddChildClass(nc.NestedClass);
            }
        }

        public void ResolveCustomAttributes()
        {
            ICLRTable table = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.CustomAttribute);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRCustomAttributeRow ca = (CLRCustomAttributeRow)table.GetRow(i);
                ICLRHasCustomAttributes hasCA = (ICLRHasCustomAttributes)ca.Parent;

                hasCA.CustomAttributes.Add(ca);
            }
        }

        public void ResolveGenericParameters()
        {
            ICLRTable gpTable = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.GenericParam);
            for (uint i = 0; i < gpTable.NumRows; i++)
            {
                CLRGenericParamRow gp = (CLRGenericParamRow)gpTable.GetRow(i);
                CLRTableRow owner = gp.Owner;
                if (owner is CLRTypeDefRow)
                    ((CLRTypeDefRow)owner).AddGenericParameter(gp);
                else if (owner is CLRMethodDefRow)
                    ((CLRMethodDefRow)owner).AddGenericParameter(gp);
                else
                    throw new ParseFailedException("Strange generic param owner");
            }
        }

        public void ResolveGenericConstraints()
        {
            ICLRTable gpcTable = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.GenericParamConstraint);
            for (uint i = 0; i < gpcTable.NumRows; i++)
            {
                CLRGenericParamConstraintRow gpc = (CLRGenericParamConstraintRow)gpcTable.GetRow(i);
                gpc.Owner.Constraints.Add(gpc);
            }
        }

        public void ResolveInterfaceImplementations()
        {
            ICLRTable table = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.InterfaceImpl);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRInterfaceImplRow ii = (CLRInterfaceImplRow)table.GetRow(i);
                ii.Class.AddInterfaceImplementation(ii.Interface);
            }
        }

        public void ResolveMethodImplementations()
        {
            ICLRTable table = MetaData.MetaDataTables.GetTable(CLRMetaDataTables.TableIndex.MethodImpl);
            for (uint i = 0; i < table.NumRows; i++)
            {
                CLRMethodImplRow mi = (CLRMethodImplRow)table.GetRow(i);
                mi.Class.AddMethodImplementation(mi);
            }
        }
    }
}
