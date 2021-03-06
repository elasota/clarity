﻿using System;
using System.Collections.Generic;

namespace AssemblyImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            CLR.CLRAssemblyCollection assemblies = new CLR.CLRAssemblyCollection();

            string exportDir = args[0];
            string stubDir = args[1];

            Dictionary<CLR.CLRAssembly, string> pdbPaths = new Dictionary<CLR.CLRAssembly, string>();

            for (int assmIndex = 2; assmIndex < args.Length; assmIndex++)
            {
                string path = args[assmIndex];
                Console.WriteLine("Loading assembly " + path);
                CLR.CLRAssembly clrAssembly;
                using (System.IO.FileStream fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    StreamParser parser = new StreamParser(fs, false);
                    CLR.CLRAssembly assembly = new CLR.CLRAssembly(parser);

                    string pdbPath = path.Substring(0, path.Length - 3) + "pdb";
                    if (System.IO.File.Exists(pdbPath))
                        pdbPaths.Add(assembly, pdbPath);

                    assemblies.Add(assembly);
                }
            }

            Console.WriteLine("Resolving...");
            assemblies.ResolveAll();

            Console.WriteLine("Exporting...");
            CppExport.CppBuilder builder = new CppExport.CppBuilder(exportDir + "\\", stubDir + "\\", assemblies, pdbPaths);
            Console.WriteLine("Done");

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
