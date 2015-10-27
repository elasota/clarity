using System;
using System.Collections.Generic;
using System.IO;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppDependencySet
    {
        public enum LevelEnum
        {
            Proto,
            Def,
            Box
        }

        private HashSet<string> m_protoDependencies;
        private HashSet<string> m_defDependencies;
        private HashSet<string> m_boxDependencies;

        public CppDependencySet()
        {
            m_protoDependencies = new HashSet<string>();
            m_defDependencies = new HashSet<string>();
            m_boxDependencies = new HashSet<string>();
        }

        public void AddProtoDependency(string classFullName)
        {
            if (!m_defDependencies.Contains(classFullName) &&
                !m_boxDependencies.Contains(classFullName))
                m_protoDependencies.Add(classFullName);
        }

        public void AddDependency(string classFullName, LevelEnum defLevel)
        {
            if (defLevel == LevelEnum.Def)
                AddDefDependency(classFullName);
            else if (defLevel == LevelEnum.Proto)
                AddProtoDependency(classFullName);
            else if (defLevel == LevelEnum.Box)
                AddBoxDependency(classFullName);
            else
                throw new ArgumentException();
        }

        public void AddDefDependency(string classFullName)
        {
            m_protoDependencies.Remove(classFullName);
            if (!m_boxDependencies.Contains(classFullName))
                m_defDependencies.Add(classFullName);
        }

        public void AddBoxDependency(string classFullName)
        {
            m_protoDependencies.Remove(classFullName);
            m_defDependencies.Remove(classFullName);
            m_boxDependencies.Add(classFullName);
        }

        public void WriteAll(StreamWriter writer)
        {
            SortedSet<string> protos = new SortedSet<string>();
            SortedSet<string> defs = new SortedSet<string>();
            SortedSet<string> boxes = new SortedSet<string>();
            foreach (string fullName in m_protoDependencies)
                protos.Add(fullName);
            foreach (string fullName in m_defDependencies)
                defs.Add(fullName);
            foreach (string fullName in m_boxDependencies)
                boxes.Add(fullName);

            foreach (string fullName in protos)
                writer.WriteLine("#include \"" + CppClass.GeneratePrototypePathForFullName(fullName) + "\"");
            foreach (string fullName in defs)
                writer.WriteLine("#include \"" + CppClass.GenerateDefinitionPathForFullName(fullName) + "\"");
            foreach (string fullName in boxes)
                writer.WriteLine("#include \"" + CppClass.GenerateBoxPathForFullName(fullName) + "\"");
        }

        public void WriteCodeDeps(StreamWriter writer)
        {
            SortedSet<string> everything = new SortedSet<string>();
            foreach (string fullName in m_protoDependencies)
                everything.Add(fullName);
            foreach (string fullName in m_defDependencies)
                everything.Add(fullName);
            foreach (string fullName in m_boxDependencies)
                everything.Add(fullName);

            foreach (string fullName in everything)
                writer.WriteLine("#include \"" + CppClass.GenerateMainHeaderForFullName(fullName) + "\"");
        }

        public void AddTypeSpecDependencies(CLRTypeSpec ts, bool hackNeedDef)
        {
            AddTypeSpecDependencies(ts, hackNeedDef ? LevelEnum.Def : LevelEnum.Proto);
        }

        public void AddTypeSpecDependencies(CLRTypeSpec ts, LevelEnum depLevel)
        {
            if (ts is CLRTypeSpecClass)
            {
                CLRTypeSpecClass cls = (CLRTypeSpecClass)ts;
                AddDependency(CppClass.GenerateFullPath(cls.TypeDef), depLevel);
            }
            else if (ts is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)ts;
                AddTypeSpecDependencies(gi.GenericType, depLevel);
                foreach (CLRTypeSpec param in gi.ArgTypes)
                    AddTypeSpecDependencies(param, depLevel);
            }
            else if (ts is CLRTypeSpecSZArray)
            {
                AddTypeSpecDependencies(((CLRTypeSpecSZArray)ts).SubType, depLevel);
            }
            else if (ts is CLRTypeSpecComplexArray)
            {
                AddTypeSpecDependencies(((CLRTypeSpecComplexArray)ts).SubType, depLevel);
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

        public void AddMethodSigDependencies(CLRMethodSignatureInstance inst, LevelEnum depLevel)
        {
            AddTypeSpecDependencies(inst.RetType, depLevel);
            foreach (CLRMethodSignatureInstanceParam param in inst.ParamTypes)
                AddTypeSpecDependencies(param.Type, depLevel);
        }
    }
}
