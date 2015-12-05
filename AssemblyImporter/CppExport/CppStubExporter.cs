using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;
using System.IO;

namespace AssemblyImporter.CppExport
{
    public class CppStubExporter
    {
        public static void ExportStub(CppBuilder cppBuilder, string stubDir, CppClass cls)
        {
            string stubKeyBase = "// CLARITY_STUB ";
            string depStartBase = "// CLARITY_AUTO_DEPENDENCIES_START";
            string depEndBase = "// CLARITY_AUTO_DEPENDENCIES_END";
            string stubPath = null;
            foreach (CLRSigCustomAttribute stubExportAttrib in cppBuilder.CustomAttribsOfType(cls.TypeDef, "Clarity", "ExportStubAttribute"))
            {
                if (stubPath == null)
                    stubPath = stubDir + (string)stubExportAttrib.FixedArgs[0].Elements[0].Value;
                else
                    throw new ParseFailedException("Multiple ExportClassStubs attribs on a class");
            }

            if (stubPath != null)
            {
                HashSet<string> existingStubs = new HashSet<string>();
                List<string> contentLines = new List<string>();
                bool isParsingDeps = false;
                bool haveDepStart = false;
                int depLocation = 0;

                if (File.Exists(stubPath))
                {
                    using (StreamReader streamReader = new StreamReader(stubPath, System.Text.Encoding.ASCII))
                    {
                        for (string line = streamReader.ReadLine(); line != null; line = streamReader.ReadLine())
                        {
                            if (line == depStartBase)
                            {
                                depLocation = contentLines.Count;
                                isParsingDeps = true;
                                haveDepStart = true;
                            }
                            else if (line == depEndBase)
                                isParsingDeps = false;
                            else
                            {
                                if (isParsingDeps)
                                {
                                    if (!line.StartsWith("#include"))
                                        throw new ParseFailedException("Bad include line in stub exported dependencies block");
                                }
                                else
                                {
                                    contentLines.Add(line);
                                    if (!haveDepStart && line.StartsWith("#include"))
                                        depLocation = contentLines.Count;
                                }
                            }

                            int stubKeyBaseLoc = line.IndexOf(stubKeyBase);
                            if (stubKeyBaseLoc >= 0)
                                existingStubs.Add(line.Substring(stubKeyBaseLoc + stubKeyBase.Length));
                        }

                        if (isParsingDeps)
                            throw new ParseFailedException("Unterminated dependency block");
                    }
                }

                if (depLocation == contentLines.Count)
                    contentLines.Add("");

                CppDependencySet depSet = new CppDependencySet();

                byte[] newStubsContent;
                using (MemoryStream newStubsStream = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(newStubsStream, System.Text.Encoding.ASCII))
                    {
                        foreach (CppMethod method in cls.Methods)
                        {
                            if (method.MethodDef.Abstract || method.MethodDef.Method != null)
                                continue;

                            CLRTypeSpec spec = method.DeclaredInClassSpec;

                            string stubSig = method.MethodSignature.RetType.ToString() + " " + spec.ToString() + "/" + method.Name + "(";

                            bool firstParam = true;
                            foreach (CLRMethodSignatureInstanceParam param in method.MethodSignature.ParamTypes)
                            {
                                if (firstParam)
                                    firstParam = false;
                                else
                                    stubSig += ",";

                                switch (param.TypeOfType)
                                {
                                    case CLRSigParamOrRetType.TypeOfTypeEnum.ByRef:
                                        stubSig += "ref ";
                                        break;
                                    case CLRSigParamOrRetType.TypeOfTypeEnum.TypedByRef:
                                        stubSig += "tref ";
                                        break;
                                    case CLRSigParamOrRetType.TypeOfTypeEnum.Value:
                                        break;
                                    default:
                                        throw new ArgumentException();
                                }
                                stubSig += param.Type.ToString();
                            }
                            stubSig += ")";

                            if (!existingStubs.Contains(stubSig))
                            {
                                writer.WriteLine();
                                writer.WriteLine(stubKeyBase + stubSig);

                                int nClassParameters = cls.NumGenericParameters;
                                int nMethodParameters = method.NumGenericParameters;

                                if (nClassParameters != 0)
                                {
                                    writer.Write("template< ");
                                    CppBuilder.WriteTemplateParamCluster(false, nClassParameters, "T", writer);
                                    writer.WriteLine(" >");
                                }
                                if (nMethodParameters != 0)
                                {
                                    writer.Write("template< ");
                                    CppBuilder.WriteTemplateParamCluster(false, nMethodParameters, "M", writer);
                                    writer.WriteLine(" >");
                                }

                                writer.Write(cppBuilder.SpecToValueType(method.MethodSignature.RetType));
                                writer.Write(" (");
                                writer.Write(cls.GenerateCppClassName());
                                CppBuilder.WriteTemplateParamCluster(true, nClassParameters, "T", writer);
                                writer.Write("::");
                                writer.Write(method.GenerateCodeName());
                                CppBuilder.WriteTemplateParamCluster(true, nMethodParameters, "M", writer);
                                writer.Write(")");

                                CLRTypeSpec inlineThisType = method.Static ? null : CppBuilder.CreateInstanceTypeSpec(cppBuilder.Assemblies, cls.TypeDef);

                                cppBuilder.WriteMethodParameters(writer, null, inlineThisType, method.MethodSignature, CppBuilder.MethodParameterMapping.ClassImpl);
                                writer.WriteLine();
                                writer.WriteLine("{");
                                writer.WriteLine("\tCLARITY_NOTIMPLEMENTED;");
                                writer.WriteLine("}");
                            }

                            depSet.AddTypeSpecDependencies(spec, true);
                            depSet.AddMethodSigDependencies(method.MethodSignature, CppDependencySet.LevelEnum.Def);
                        }

                        writer.Flush();
                        newStubsContent = newStubsStream.ToArray();
                    }
                }

                // Write the stub file back out
                using (StreamWriter writer = new StreamWriter(stubPath, false, System.Text.Encoding.ASCII))
                {
                    for (int lineNum = 0; lineNum < contentLines.Count; lineNum++)
                    {
                        if (lineNum == depLocation)
                        {
                            writer.WriteLine(depStartBase);
                            depSet.WriteCodeDeps(writer);
                            writer.WriteLine(depEndBase);
                        }
                        writer.WriteLine(contentLines[lineNum]);
                    }

                    writer.Flush();
                    writer.BaseStream.Write(newStubsContent, 0, newStubsContent.Length);
                }
            }
            else
            {
                foreach (CppMethod method in cls.Methods)
                {
                    if (method.MethodDef.Abstract || method.MethodDef.Method != null)
                        continue;

                    Console.WriteLine("WARNING: " + cls.TypeDef.TypeNamespace + "." + cls.TypeDef.TypeName + " has InternalCall methods, but no StubExport");
                    break;
                }
            }
        }
    }
}
