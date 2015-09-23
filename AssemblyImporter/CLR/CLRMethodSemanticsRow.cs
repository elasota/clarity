using System;

namespace AssemblyImporter.CLR
{
    public class CLRMethodSemanticsRow : CLRTableRow
    {
        public CLRMethodDefRow Method { get; private set; }
        public CLRTableRow Association { get; private set; }
        public bool Setter { get; private set; }
        public bool Getter { get; private set; }
        public bool Other { get; private set; }
        public bool AddOn { get; private set; }
        public bool RemoveOn { get; private set; }
        public bool Fire { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            uint semantics = parser.ReadU16();
            Setter = ((semantics & 0x1) != 0);
            Getter = ((semantics & 0x2) != 0);
            Other = ((semantics & 0x4) != 0);
            AddOn = ((semantics & 0x8) != 0);
            RemoveOn = ((semantics & 0x10) != 0);
            Fire = ((semantics & 0x20) != 0);

            Method = (CLRMethodDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.MethodDef);
            Association = parser.ReadHasSemantics();
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Flags restricted
            // Method not null
            // All methods for a Property or Event have the same accessibility and cannot be CompilerControlled
            // If row is Property:
            //     Setter, Getter, or Other set
            // If row is Event:
            //     AddOn, RemoveOn, Fire, or Other set
            // If row is Event and Semtnatics Addon or RemoveOn, Method must take a Delegate as parameter and return void
            // If row is Event and Semantics Fire, Method can return any type
            // For all properties, getter, setter, or both must exist
            // Getter is named get_*
            // Setter is named set_*
            // If both getter and setter, MemberAccessMask is identical and both have same Virtual flag
            // Getter and setter have SpecialName = 1
            // Getter return type matches property type
            // Last parameter for setter matches property type
            // Setter method signature returns void
            // If property is indexed, setter and getter indexes are same number and type
            // AddOn is named add_*, returns void, has 1 delegate parameter
            // RemoveOn is named remove_*, returns void, has 1 delegate parameter
            // Fire is named raise_*, returns void, has one Event parameter
        }
    }
}
