using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppDependencySet
    {
        private HashSet<string> m_protoDependencies;
        private HashSet<string> m_defDependencies;

        public CppDependencySet()
        {
            m_protoDependencies = new HashSet<string>();
            m_defDependencies = new HashSet<string>();
        }

        public void AddProtoDependency(string classFullName)
        {
            if (!m_defDependencies.Contains(classFullName))
                m_protoDependencies.Add(classFullName);
        }

        public void AddDependency(string classFullName, bool needDef)
        {
            if (needDef)
                AddDefDependency(classFullName);
            else
                AddProtoDependency(classFullName);
        }

        public void AddDefDependency(string classFullName)
        {
            m_protoDependencies.Remove(classFullName);
            m_defDependencies.Add(classFullName);
        }

        public void WriteAll(StreamWriter writer)
        {
            SortedSet<string> protos = new SortedSet<string>();
            SortedSet<string> defs = new SortedSet<string>();
            foreach (string fullName in m_protoDependencies)
                protos.Add(fullName);
            foreach (string fullName in m_defDependencies)
                defs.Add(fullName);

            foreach (string fullName in protos)
                writer.WriteLine("#include \"" + CppClass.GeneratePrototypePathForFullName(fullName) + "\"");
            foreach (string fullName in defs)
                writer.WriteLine("#include \"" + CppClass.GenerateDefinitionPathForFullName(fullName) + "\"");
        }

        public void AddTypeSpecDependencies(CLRTypeSpec ts, bool needDef)
        {
            if (ts is CLRTypeSpecClass)
            {
                CLRTypeSpecClass cls = (CLRTypeSpecClass)ts;
                AddDependency(CppClass.GenerateFullPath(cls.TypeDef), needDef);
            }
            else if (ts is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)ts;
                AddTypeSpecDependencies(gi.GenericType, needDef);
                foreach (CLRTypeSpec param in gi.ArgTypes)
                    AddTypeSpecDependencies(param, needDef);
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                AddTypeSpecDependencies(((CLRTypeSpecSZArray)ts).SubType, needDef);
            }
            else if (ts is CLRTypeSpecComplexArray)
            {
                AddTypeSpecDependencies(((CLRTypeSpecComplexArray)ts).SubType, needDef);
            }
            else if (ts is CLRTypeSpecVarOrMVar)
            {
            }
            else if (ts is CLRTypeSpecVoid)
            {
            }
            else
                throw new ArgumentException();
        }

        public void AddMethodSigDependencies(CLRMethodSignatureInstance inst, bool needDef)
        {
            AddTypeSpecDependencies(inst.RetType, needDef);
            foreach (CLRMethodSignatureInstanceParam param in inst.ParamTypes)
                AddTypeSpecDependencies(param.Type, needDef);
        }
    }
}
