using System.IO;
using System.Collections.Generic;
using System;

namespace AssemblyImporter.CppExport
{
    public class CppMidCompiler
    {
        private CppBuilder m_builder;
        private CppClass m_cls;
        private ExceptionHandlingRegion m_mainRegion;
        private CppMethod m_method;
        private VReg[] m_args;
        private VReg[] m_locals;

        private MemoryStream m_instructionStream;
        private StreamWriter m_instructionWriter;
        private CppRegisterAllocator m_regAllocator;
        private string m_frameVarName;

        public CppMidCompiler(CppBuilder builder, CppClass cls, CppMethod method, ExceptionHandlingRegion mainRegion, string frameVarName, VReg[] args, VReg[] locals)
        {
            m_builder = builder;
            m_cls = cls;
            m_method = method;
            m_mainRegion = mainRegion;
            m_args = args;
            m_locals = locals;
            m_frameVarName = frameVarName;

            m_instructionStream = new MemoryStream();
            m_instructionWriter = new StreamWriter(m_instructionStream);
            m_regAllocator = new CppRegisterAllocator(builder);
        }

        private void EmitRegion(CppDependencySet depSet, int baseIndentLevel, ExceptionHandlingRegion region)
        {
            CppRegionEmitter emitter = new CppRegionEmitter(depSet, baseIndentLevel, m_builder, region, m_regAllocator, m_frameVarName);
            emitter.Emit(m_instructionWriter);
        }

        public void EmitAll(CppDependencySet depSet, Stream baseStream)
        {
            EmitRegion(depSet, 1, m_mainRegion);

            m_instructionWriter.Flush();

            // Generate locals, args, and temps
            List<VReg> paramVRegs = new List<VReg>();
            paramVRegs.AddRange(m_locals);
            paramVRegs.AddRange(m_args);

            List<VReg> allVRegs = new List<VReg>();
            allVRegs.AddRange(paramVRegs);
            allVRegs.AddRange(m_regAllocator.AllRegisters);

            // Emit locals
            using (MemoryStream localsStream = new MemoryStream())
            {
                using (StreamWriter localsWriter = new StreamWriter(localsStream))
                {
                    // Emit untraced locals
                    bool haveAnyTraced = false;
                    foreach (VReg vReg in allVRegs)
                    {
                        if (vReg.Traceability == CppTraceabilityEnum.NotTraced)
                        {
                            depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, true);
                            localsWriter.Write("\t");
                            localsWriter.Write(m_builder.VTypeStorageToValueType(vReg.VType));
                            localsWriter.Write(" ");
                            localsWriter.Write(vReg.BasicName);
                            localsWriter.WriteLine(";");
                        }
                        else
                            haveAnyTraced = true;
                    }

                    // Emit traced regs
                    if (haveAnyTraced)
                    {
                        localsWriter.WriteLine("\tstruct bTracedLocalsStruct");
                        localsWriter.WriteLine("\t{");
                        bool compileTimeEvaluateTrace = true;
                        foreach (VReg vReg in allVRegs)
                        {
                            if (vReg.Traceability != CppTraceabilityEnum.NotTraced)
                            {
                                depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, true);
                                localsWriter.Write("\t\t");
                                if (vReg.VType.TypeSpec.UsesAnyGenericParams)
                                    localsWriter.Write("typename ");

                                if (vReg.Traceability != CppTraceabilityEnum.MaybeTraced)
                                    compileTimeEvaluateTrace = false;

                                switch (vReg.VType.ValType)
                                {
                                    case VType.ValTypeEnum.AnchoredManagedPtr:
                                        localsWriter.Write("::CLRVM::TAnchoredManagedPtrLocal< ");
                                        break;
                                    case VType.ValTypeEnum.LocalManagedPtr:
                                        localsWriter.Write("::CLRVM::TLocalManagedPtrLocal< ");
                                        break;
                                    case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                                        localsWriter.Write("::CLRVM::TMaybeAnchoredManagedPtrLocal< ");
                                        break;
                                    case VType.ValTypeEnum.ValueValue:
                                        localsWriter.Write("::CLRVM::TValLocal< ");
                                        break;
                                    case VType.ValTypeEnum.NotNullReferenceValue:
                                    case VType.ValTypeEnum.NullableReferenceValue:
                                        localsWriter.Write("::CLRVM::TRefLocal< ");
                                        break;
                                    default:
                                        throw new ArgumentException();
                                }
                                localsWriter.Write("::CLRVM::ELocalType::");
                                localsWriter.Write(vReg.Usage.ToString());
                                localsWriter.Write(", ");
                                localsWriter.Write(m_builder.SpecToAmbiguousStorage(vReg.VType.TypeSpec));
                                localsWriter.Write(" >::Type ");
                                localsWriter.Write(vReg.BasicName);
                                localsWriter.WriteLine(";");
                            }
                        }

                        // Emit tracing flag
                        localsWriter.WriteLine("\t\tenum");
                        localsWriter.WriteLine("\t\t{");
                        localsWriter.Write("\t\t\tIsTraceable = ");
                        if (compileTimeEvaluateTrace)
                        {
                            bool first = true;
                            localsWriter.WriteLine("(");
                            foreach (VReg vReg in allVRegs)
                            {
                                if (vReg.Traceability == CppTraceabilityEnum.MaybeTraced)
                                {
                                    localsWriter.Write("\t\t\t\t");
                                    if (first)
                                        first = false;
                                    else
                                        localsWriter.Write("|| ");

                                    localsWriter.Write("(");
                                    localsWriter.Write("::CLRTI::TypeTraits< ");
                                    localsWriter.Write(m_builder.SpecToAmbiguousStorage(vReg.VType.TypeSpec));
                                    localsWriter.Write(" >::IsValueTraceable != 0");
                                    localsWriter.WriteLine(")");
                                }
                            }
                            localsWriter.WriteLine("\t\t\t) ? 1 : 0;");
                        }
                        else
                            localsWriter.WriteLine("1");
                        localsWriter.WriteLine("\t\t};");

                        // Emit visitor callback
                        localsWriter.WriteLine("\t\tvoid VisitReferences(::CLRExec::IRefVisitor &visitor)");
                        localsWriter.WriteLine("\t\t{");
                        foreach (VReg vReg in allVRegs)
                        {
                            if (vReg.Traceability != CppTraceabilityEnum.NotTraced)
                            {
                                depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, true);
                                localsWriter.Write("\t\t\t");

                                if (vReg.Traceability != CppTraceabilityEnum.MaybeTraced)
                                    compileTimeEvaluateTrace = false;

                                localsWriter.Write("::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::");
                                localsWriter.Write(vReg.Usage.ToString());
                                localsWriter.Write(", ");
                                localsWriter.Write(m_builder.SpecToAmbiguousStorage(vReg.VType.TypeSpec));
                                localsWriter.Write(" >");

                                switch (vReg.VType.ValType)
                                {
                                    case VType.ValTypeEnum.AnchoredManagedPtr:
                                        localsWriter.Write("::TraceAnchoredManagedPtrLocal");
                                        break;
                                    case VType.ValTypeEnum.LocalManagedPtr:
                                        localsWriter.Write("::TraceLocalManagedPtrLocal");
                                        break;
                                    case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                                        localsWriter.Write("::TraceMaybeAnchoredManagedPtrLocal");
                                        break;
                                    case VType.ValTypeEnum.ValueValue:
                                        localsWriter.Write("::TraceValLocal");
                                        break;
                                    case VType.ValTypeEnum.NotNullReferenceValue:
                                    case VType.ValTypeEnum.NullableReferenceValue:
                                        localsWriter.Write("::TraceRefLocal");
                                        break;
                                    default:
                                        throw new ArgumentException();
                                }

                                localsWriter.Write("(visitor, this->");
                                localsWriter.Write(vReg.BasicName);
                                localsWriter.WriteLine(");");
                            }
                        }

                        localsWriter.WriteLine("\t\t}");


                        localsWriter.WriteLine("\t};");
                        localsWriter.WriteLine("\tbTracedLocalsStruct bTracedLocals;");

                        if (compileTimeEvaluateTrace)
                            localsWriter.WriteLine("\tconst ::CLRExec::TMaybeTracingLocalFrame<bTracedLocalsStruct>::Type bTLFrame = ::CLRVM::DisambiguateTLFrame<bTracedLocalsStruct>(frame, bTracedLocals);");
                        else
                            localsWriter.WriteLine("\tconst ::CLRExec::TracingLocalFrame<bTracedLocalsStruct> bTLFrame(frame, bTracedLocals);");
                    }
                    else
                        localsWriter.WriteLine("\tconst ::CLRExec::Frame<bTracedLocalsStruct> &bTLFrame = frame;");

                    localsWriter.Flush();
                    localsStream.WriteTo(baseStream);
                }
            }

            m_instructionStream.WriteTo(baseStream);
        }
    }
}

