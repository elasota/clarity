using System;

namespace AssemblyImporter.CLR
{
    // II.22.33
    public class CLRParamRow : CLRTableRow, ICLROwnedBy<CLRMethodDefRow>, ICLRHasConstant
    {
        public bool In { get; private set; }
        public bool Out { get; private set; }
        public bool Optional { get; private set; }
        public bool HasDefault { get; private set; }
        public bool HasFieldMarshal { get; private set; }
        public CLRConstantRow[] AttachedConstants { get; set; }

        public ushort Sequence { get; private set; }
        public string Name { get; private set; }

        public CLRMethodDefRow Owner { get; set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            uint flags = parser.ReadU16();
            Sequence = parser.ReadU16();
            Name = parser.ReadString();

            In = ((flags & 0x1) != 0);
            Out = ((flags & 0x2) != 0);
            Optional = ((flags & 0x10) != 0);
            HasDefault = ((flags & 0x1000) != 0);
            HasFieldMarshal = ((flags & 0x2000) != 0);
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Only one MethodDef owner
            // Flags restricted
            // Sequence >= 0 and <= parameters in owner method (Sequence 0 is owner return type, parameters 1+)
            //    (Sequence 0 may be absent if void return)
            // Sequence must increment, but gaps are allowed (WARN)
            // If HasDefault, owns 1 Constant
            // If HasDefault, owns no Constant
            // If FieldMarshal, owns one FieldMarshal
            // Name may be null
            // If Name is non-null, indexes non-empty string (WARN)
        }
    }
}
