using System;
using System.Collections.Generic;

namespace AssemblyImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            CLR.CLRAssemblyCollection assemblies = new CLR.CLRAssemblyCollection();

            foreach (string path in args)
            {
                Console.WriteLine("Loading assembly " + path);
                CLR.CLRAssembly clrAssembly;
                using (System.IO.FileStream fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    StreamParser parser = new StreamParser(fs, false);
                    assemblies.Add(new CLR.CLRAssembly(parser));
                }
            }

            assemblies.ResolveAll();

            CppExport.CppBuilder builder = new CppExport.CppBuilder(assemblies);

            /*
            Console.WriteLine("Compacting...");
            TCLR.TCLRAssemblyBuilder builder = new TCLR.TCLRAssemblyBuilder();
            builder.ImportAssembly(clrAssembly, false);

            string tclrAssemblyPath = path;
            if (tclrAssemblyPath.EndsWith(".dll"))
                tclrAssemblyPath = tclrAssemblyPath.Substring(0, tclrAssemblyPath.Length - 4);
            tclrAssemblyPath += ".cca";
            Console.WriteLine("Writing Clarity compact assembly " + tclrAssemblyPath);

            using (System.IO.FileStream fs = new System.IO.FileStream(tclrAssemblyPath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            {
                builder.Export(fs);
            }
            */
        }
    }
}
