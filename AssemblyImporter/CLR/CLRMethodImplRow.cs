using System;

namespace AssemblyImporter.CLR
{
    // II.22.27
    public class CLRMethodImplRow : CLRTableRow
    {
        public CLRTypeDefRow Class { get; private set; }
        public CLRTableRow MethodBody { get; private set; }
        public CLRTableRow MethodDeclaration { get; private set; }

        public override void Parse(CLRMetaDataParser parser)
        {
            Class = (CLRTypeDefRow)parser.ReadTable(CLRMetaDataTables.TableIndex.TypeDef);
            MethodBody = parser.ReadMethodDefOrRef();
            MethodDeclaration = parser.ReadMethodDefOrRef();
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Class is not null
            // MethodBody is not null
            // MethodDeclaration is Virtual
            // Owner of Type from MethodDeclaration is not Sealed
            // Method indexed by MethodBody is member of Class or a base class of it
            // Method indexed by MethodBody is virtual
            // Method indexed by MethodBody RVA not 0
            // MethodDeclaration appears in class Extends chain or InterfaceImpl tree
            // MethodDeclaration method not Final
            // If MethodDeclaration is Strict, MethodDeclaration is accessible to Class
            // Method signature of MethodBody same as MethodDeclaration
            // No duplicates by Class+MethodDeclaration
        }
    }
}
