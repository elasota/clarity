using System;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppMethodSpec
    {
        public CLRTypeSpec[] GenericParameters { get; private set; }
        public CppMethod CppMethod { get; private set; }

        public CppMethodSpec(CppMethod method)
        {
            CppMethod = method;
        }

        public CppMethodSpec(CppMethod method, CLRTypeSpec[] genericParameters)
        {
            CppMethod = method;
            GenericParameters = genericParameters;
        }
    }
}
