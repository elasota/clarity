using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    // II.22.20
    // II.23.1.7
    public class CLRGenericParamRow : CLRTableRow, ICLRHasCustomAttributes
    {
        public enum VarianceEnum
        {
            None = 0x0,
            Covariant = 0x1,
            Contravariant = 0x2,
        }

        public ushort Number { get; private set; }
        public CLRTableRow Owner { get; private set; }
        public string Name { get; private set; }
        public VarianceEnum Variance { get; private set; }

        public bool ReferenceTypeConstraint { get; private set; }
        public bool NotNullableValueTypeConstraint { get; private set; }
        public bool DefaultConstructorConstraint { get; private set; }

        public IList<CLRGenericParamConstraintRow> Constraints { get; private set; }

        private CustomAttributeCollection m_customAttributes;
        public CustomAttributeCollection CustomAttributes { get { return CustomAttributeCollection.LazyCreate(ref m_customAttributes); } }

        public override void Parse(CLRMetaDataParser parser)
        {
            Number = parser.ReadU16();
            uint flags = parser.ReadU16();
            Owner = parser.ReadTypeOrMethodDef();
            Name = parser.ReadString();

            Constraints = new List<CLRGenericParamConstraintRow>();

            Variance = (VarianceEnum)(flags & 0x3);

            if ((Variance & (VarianceEnum.Covariant | VarianceEnum.Contravariant)) != 0)
                throw new NotSupportedException("Covariant and contravariant generic parameters are not supported.");

            ReferenceTypeConstraint = ((flags & 0x4) != 0);
            NotNullableValueTypeConstraint = ((flags & 0x8) != 0);
            DefaultConstructorConstraint = ((flags & 0x10) != 0);
        }
    }
}
