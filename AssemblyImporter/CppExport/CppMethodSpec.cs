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

        public override string ToString()
        {
            string fullName = CppMethod.MethodSignature.RetType.ToString();
            fullName += " ";
            fullName += CppMethod.DeclaredInClassSpec;
            fullName += ".";
            fullName += CppMethod.Name;

            if (GenericParameters != null && GenericParameters.Length > 0)
            {
                fullName += "<";
                for (int gp = 0; gp < GenericParameters.Length; gp++)
                {
                    if (gp != 0)
                        fullName += ",";
                    fullName += GenericParameters[gp].ToString();
                }
                fullName += ">";
            }

            fullName += "(";
            for (int p = 0; p < CppMethod.MethodSignature.ParamTypes.Length; p++)
            {
                if (p != 0)
                    fullName += ",";
                fullName += CppMethod.MethodSignature.ParamTypes[p];
            }
            fullName += ")";
            return fullName;
        }
    }
}
