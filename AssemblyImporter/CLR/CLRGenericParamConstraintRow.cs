using System;

namespace AssemblyImporter.CLR
{
    // II.22.21
    public class CLRGenericParamConstraintRow : CLRTableRow, ICLRHasCustomAttributes
    {
        public CLRGenericParamRow Owner { get; private set; }
        public CLRTableRow Constraint { get; private set; }

        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }

        public override void Parse(CLRMetaDataParser parser)
        {
            Owner = (CLRGenericParamRow)parser.ReadTable(CLRMetaDataTables.TableIndex.GenericParam);
            Constraint = parser.ReadTypeDefOrRefOrSpec();
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
        }
    }
}
