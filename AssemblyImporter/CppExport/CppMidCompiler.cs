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

        public void EmitAll(CppDependencySet depSet, Stream localClusterStream, Stream baseStream)
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

            MemoryStream localClusterTempStream = null;
            StreamWriter localClusterWriter = null;

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
                        localClusterTempStream = new MemoryStream();
                        localClusterWriter = new StreamWriter(localClusterTempStream);

                        string localClusterMangle;
                        {
                            CppMangleBuilder localClusterMangleBuilder = new CppMangleBuilder();
                            localClusterMangleBuilder.Add(CppBuilder.CreateInstanceTypeSpec(m_builder.Assemblies, m_cls.TypeDef));
                            localClusterMangleBuilder.Add(m_method.Name);
                            localClusterMangleBuilder.Add(m_method.MethodSignature);
                            localClusterMangle = localClusterMangleBuilder.Finish();
                        }

                        localClusterWriter.WriteLine("namespace CLRX");
                        localClusterWriter.WriteLine("{");
                        localClusterWriter.WriteLine("namespace bLocalClusters");
                        localClusterWriter.WriteLine("{");

                        if (m_cls.NumGenericParameters > 0 || m_method.NumGenericParameters > 0)
                        {
                            localClusterWriter.Write("\ttemplate< ");
                            CppBuilder.WriteTemplateDualParamCluster(false, m_cls.NumGenericParameters, m_method.NumGenericParameters, "class T", "class M", localClusterWriter);
                            localClusterWriter.WriteLine(" >");
                        }

                        localClusterWriter.Write("\tstruct bLocalCluster_");
                        localClusterWriter.WriteLine(localClusterMangle);
                        localClusterWriter.WriteLine("\t{");
                        bool compileTimeEvaluateTrace = true;
                        foreach (VReg vReg in allVRegs)
                        {
                            if (vReg.Traceability != CppTraceabilityEnum.NotTraced)
                            {
                                depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, true);
                                localClusterWriter.Write("\t\t");
                                if (vReg.VType.TypeSpec.UsesAnyGenericParams)
                                    localClusterWriter.Write("typename ");

                                if (vReg.Traceability != CppTraceabilityEnum.MaybeTraced)
                                    compileTimeEvaluateTrace = false;

                                switch (vReg.VType.ValType)
                                {
                                    case VType.ValTypeEnum.AnchoredManagedPtr:
                                        localClusterWriter.Write("::CLRVM::TAnchoredManagedPtrLocal< ");
                                        break;
                                    case VType.ValTypeEnum.LocalManagedPtr:
                                        localClusterWriter.Write("::CLRVM::TLocalManagedPtrLocal< ");
                                        break;
                                    case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                                        localClusterWriter.Write("::CLRVM::TMaybeAnchoredManagedPtrLocal< ");
                                        break;
                                    case VType.ValTypeEnum.ValueValue:
                                        localClusterWriter.Write("::CLRVM::TValLocal< ");
                                        break;
                                    case VType.ValTypeEnum.NotNullReferenceValue:
                                    case VType.ValTypeEnum.NullableReferenceValue:
                                        localClusterWriter.Write("::CLRVM::TRefLocal< ");
                                        break;
                                    default:
                                        throw new ArgumentException();
                                }
                                localClusterWriter.Write("::CLRVM::ELocalType::");
                                localClusterWriter.Write(vReg.Usage.ToString());
                                localClusterWriter.Write(", ");
                                localClusterWriter.Write(m_builder.SpecToAmbiguousStorage(vReg.VType.TypeSpec));
                                localClusterWriter.Write(" >::Type ");
                                localClusterWriter.Write(vReg.BasicName);
                                localClusterWriter.WriteLine(";");
                            }
                        }

                        // Emit tracing flag
                        localClusterWriter.WriteLine("\t\tenum");
                        localClusterWriter.WriteLine("\t\t{");
                        localClusterWriter.Write("\t\t\tIsTraceable = ");
                        if (compileTimeEvaluateTrace)
                        {
                            bool first = true;
                            localClusterWriter.WriteLine("(");
                            foreach (VReg vReg in allVRegs)
                            {
                                if (vReg.Traceability == CppTraceabilityEnum.MaybeTraced)
                                {
                                    localClusterWriter.Write("\t\t\t\t");
                                    if (first)
                                        first = false;
                                    else
                                        localClusterWriter.Write("|| ");

                                    localClusterWriter.Write("(");
                                    localClusterWriter.Write("::CLRTI::TypeTraits< ");
                                    localClusterWriter.Write(m_builder.SpecToAmbiguousStorage(vReg.VType.TypeSpec));
                                    localClusterWriter.Write(" >::IsValueTraceable != 0");
                                    localClusterWriter.WriteLine(")");
                                }
                            }
                            localClusterWriter.WriteLine("\t\t\t) ? 1 : 0;");
                        }
                        else
                            localClusterWriter.WriteLine("1");
                        localClusterWriter.WriteLine("\t\t};");

                        // Emit visitor callback
                        localClusterWriter.WriteLine("\t\tvoid VisitReferences(::CLRExec::IRefVisitor &visitor)");
                        localClusterWriter.WriteLine("\t\t{");
                        foreach (VReg vReg in allVRegs)
                        {
                            if (vReg.Traceability != CppTraceabilityEnum.NotTraced)
                            {
                                depSet.AddTypeSpecDependencies(vReg.VType.TypeSpec, true);
                                localClusterWriter.Write("\t\t\t");

                                if (vReg.Traceability != CppTraceabilityEnum.MaybeTraced)
                                    compileTimeEvaluateTrace = false;

                                localClusterWriter.Write("::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::");
                                localClusterWriter.Write(vReg.Usage.ToString());
                                localClusterWriter.Write(", ");
                                localClusterWriter.Write(m_builder.SpecToAmbiguousStorage(vReg.VType.TypeSpec));
                                localClusterWriter.Write(" >");

                                switch (vReg.VType.ValType)
                                {
                                    case VType.ValTypeEnum.AnchoredManagedPtr:
                                        localClusterWriter.Write("::TraceAnchoredManagedPtrLocal");
                                        break;
                                    case VType.ValTypeEnum.LocalManagedPtr:
                                        localClusterWriter.Write("::TraceLocalManagedPtrLocal");
                                        break;
                                    case VType.ValTypeEnum.MaybeAnchoredManagedPtr:
                                        localClusterWriter.Write("::TraceMaybeAnchoredManagedPtrLocal");
                                        break;
                                    case VType.ValTypeEnum.ValueValue:
                                        localClusterWriter.Write("::TraceValLocal");
                                        break;
                                    case VType.ValTypeEnum.NotNullReferenceValue:
                                    case VType.ValTypeEnum.NullableReferenceValue:
                                        localClusterWriter.Write("::TraceRefLocal");
                                        break;
                                    default:
                                        throw new ArgumentException();
                                }

                                localClusterWriter.Write("(visitor, this->");
                                localClusterWriter.Write(vReg.BasicName);
                                localClusterWriter.WriteLine(");");
                            }
                        }

                        localClusterWriter.WriteLine("\t\t}");
                        localClusterWriter.WriteLine("\t};");
                        localClusterWriter.WriteLine("}");
                        localClusterWriter.WriteLine("}");


                        localsWriter.Write("\t::CLRX::bLocalClusters::bLocalCluster_");
                        localsWriter.Write(localClusterMangle);
                        CppBuilder.WriteTemplateDualParamCluster(true, m_cls.NumGenericParameters, m_method.NumGenericParameters, "T", "M", localsWriter);
                        localsWriter.WriteLine(" bTracedLocals;");

                        if (compileTimeEvaluateTrace)
                        {
                            localsWriter.Write("\tconst ::CLRExec::TMaybeTracingLocalFrame< ::CLRX::bLocalClusters::bLocalCluster_");
                            localsWriter.Write(localClusterMangle);
                            CppBuilder.WriteTemplateDualParamCluster(true, m_cls.NumGenericParameters, m_method.NumGenericParameters, "T", "M", localsWriter);
                            localsWriter.WriteLine(" >::Type bTLFrame = ::CLRVM::DisambiguateTLFrame<bTracedLocalsStruct>(frame, bTracedLocals);");
                        }
                        else
                        {
                            localsWriter.Write("\tconst ::CLRExec::TracingLocalFrame< ::CLRX::bLocalClusters::bLocalCluster_");
                            localsWriter.Write(localClusterMangle);
                            CppBuilder.WriteTemplateDualParamCluster(true, m_cls.NumGenericParameters, m_method.NumGenericParameters, "T", "M", localsWriter);
                            localsWriter.WriteLine(" > bTLFrame(frame, bTracedLocals);");
                        }
                    }
                    else
                        localsWriter.WriteLine("\tconst ::CLRExec::Frame &bTLFrame = frame;");

                    localsWriter.Flush();
                    localsStream.WriteTo(baseStream);

                    if (haveAnyTraced)
                    {
                        localClusterWriter.Flush();
                        localClusterTempStream.WriteTo(localClusterStream);
                        localClusterWriter.Dispose();
                    }
                }
            }

            m_instructionStream.WriteTo(baseStream);
        }
    }
}

