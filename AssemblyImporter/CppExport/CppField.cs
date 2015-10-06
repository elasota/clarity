using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppField
    {
        public CLRTypeSpec DeclaredInClassSpec { get; private set; }
        public CLRTypeSpec Type { get; private set; }
        public string Name { get; private set; }
        public bool OriginallyGenericParam { get; private set; }
        public CLRFieldRow Field { get; private set; }

        public CppField(CLRAssemblyCollection assemblies, CLRFieldRow field)
        {
            Type = assemblies.InternVagueType(field.Signature.Type);
            OriginallyGenericParam = (field.Signature.Type is CLRSigTypeVarOrMVar);
            Name = field.Name;
            Field = field;
            DeclaredInClassSpec = CppBuilder.CreateInstanceTypeDef(assemblies, field.Owner);
        }

        private CppField(CppField baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            Type = baseInstance.Type.Instantiate(typeParams, methodParams);
            OriginallyGenericParam = baseInstance.OriginallyGenericParam;
            Name = baseInstance.Name;
            Field = baseInstance.Field;
            DeclaredInClassSpec = baseInstance.DeclaredInClassSpec.Instantiate(typeParams, methodParams);
        }

        public CppField Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CppField(this, typeParams, methodParams);
        }
    }
}
