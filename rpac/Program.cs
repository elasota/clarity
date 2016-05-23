using System;
using System.IO;

namespace rpac
{
    class Program
    {
        static void Main(string[] args)
        {
            Clarity.RpaCompiler.Compiler compiler = new Clarity.RpaCompiler.Compiler();
            foreach (string str in args)
            {
                Console.WriteLine("Loading " + str + "...");

                using (FileStream fs = new FileStream(str, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader reader = new BinaryReader(fs))
                    {
                        compiler.LoadRpa(reader);
                    }
                }
            }

            Console.WriteLine("Creating CLI types...");

            foreach (Clarity.Rpa.HighTypeDef typeDef in compiler.TypeDefs)
            {
                if (typeDef.Semantics == Clarity.Rpa.TypeSemantics.Interface)
                    compiler.InstantiateInterface(typeDef.TypeName);
                else
                    compiler.InstantiateOpenClass(typeDef.TypeName);
            }

            compiler.CompileOpenClasses();

            Console.WriteLine("Compiling methods...");
            foreach (Clarity.Rpa.HighTypeDef typeDef in compiler.TypeDefs)
            {
                if ((typeDef.Semantics == Clarity.Rpa.TypeSemantics.Class || typeDef.Semantics == Clarity.Rpa.TypeSemantics.Struct)
                    && typeDef.NumGenericParameters == 0)
                {
                    Clarity.Rpa.TypeSpecClassTag typeTag = new Clarity.Rpa.TypeSpecClassTag(typeDef.TypeName, new Clarity.Rpa.TypeSpecTag[0]);
                    typeTag = (Clarity.Rpa.TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(typeTag);

                    foreach (Clarity.Rpa.HighMethod method in typeDef.Methods)
                    {
                        if (method.MethodSignature.NumGenericParameters == 0)
                        {
                            Clarity.Rpa.MethodSpecTag methodSpecTag = new Clarity.Rpa.MethodSpecTag(
                                method.IsStatic ? Clarity.Rpa.MethodSlotType.Static : Clarity.Rpa.MethodSlotType.Instance,
                                new Clarity.Rpa.TypeSpecTag[0],
                                typeTag,
                                method.MethodDeclTag
                                );
                            methodSpecTag = compiler.TagRepository.InternMethodSpec(methodSpecTag);
                            compiler.InstantiateMethod(methodSpecTag, null);
                        }
                    }
                }
            }

            compiler.CompileAllMethods();
        }
    }
}
