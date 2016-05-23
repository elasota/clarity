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
        private VReg[] m_temporaries;

        private MemoryStream m_instructionStream;
        private StreamWriter m_instructionWriter;
        private CppRegisterAllocator m_regAllocator;
        private string m_frameVarName;

        public CppMidCompiler(CppBuilder builder, CppClass cls, CppMethod method, ExceptionHandlingRegion mainRegion, string frameVarName, VReg[] args, VReg[] locals, VReg[] temporaries)
        {
            m_builder = builder;
            m_cls = cls;
            m_method = method;
            m_mainRegion = mainRegion;
            m_args = args;
            m_locals = locals;
            m_temporaries = temporaries;
            m_frameVarName = frameVarName;

            m_instructionStream = new MemoryStream();
            m_instructionWriter = new StreamWriter(m_instructionStream);
            m_regAllocator = new CppRegisterAllocator(builder);
        }

        private static void VRegsToHighLocals(IEnumerable<VReg> vRegs, List<Clarity.Rpa.HighLocal> highLocals, Dictionary<VReg, Clarity.Rpa.HighLocal> lookupDict)
        {
            foreach (VReg vReg in vRegs)
            {
                bool isReference = false;
                switch (vReg.VType.ValType)
                {
                    case VType.ValTypeEnum.ManagedPtr:
                        isReference = true;
                        break;
                    default:
                        isReference = false;
                        break;
                }
                Clarity.Rpa.HighLocal highLocal = new Clarity.Rpa.HighLocal(RpaTagFactory.CreateTypeTag(vReg.VType.TypeSpec), isReference ? Clarity.Rpa.HighLocal.ETypeOfType.ByRef : Clarity.Rpa.HighLocal.ETypeOfType.Value);
                highLocals.Add(highLocal);
                lookupDict.Add(vReg, highLocal);
            }
        }

        public void EmitAll(Clarity.Rpa.HighFileBuilder builder, BinaryWriter writer)
        {
            // Generate locals and args
            Dictionary<VReg, Clarity.Rpa.HighLocal> localLookup = new Dictionary<VReg, Clarity.Rpa.HighLocal>();

            List<Clarity.Rpa.HighLocal> highArgs = new List<Clarity.Rpa.HighLocal>();
            List<Clarity.Rpa.HighLocal> highLocals = new List<Clarity.Rpa.HighLocal>();

            VRegsToHighLocals(m_args, highArgs, localLookup);
            VRegsToHighLocals(m_locals, highLocals, localLookup);
            VRegsToHighLocals(m_temporaries, highLocals, localLookup);

            CppRegionEmitter emitter = new CppRegionEmitter(m_builder, m_mainRegion, m_regAllocator, localLookup);
            Clarity.Rpa.HighRegion mainRegion = emitter.Emit();

            Clarity.Pdb.PdbDebugInfo debugInfo = null;
            m_builder.AssemblyPdbs.TryGetValue(m_method.MethodDef.Table.MetaData.Assembly, out debugInfo);

            Clarity.Pdb.PdbDebugFunction debugFunction = null;
            if (debugInfo != null)
                debugFunction = debugInfo.GetFunction(m_method.MethodDef.MetadataToken);

            Clarity.Rpa.HighMethodBody methodBody = new Clarity.Rpa.HighMethodBody(mainRegion, highArgs.ToArray(), highLocals.ToArray(), debugFunction != null);

            methodBody.Write(builder, writer);
        }
    }
}

