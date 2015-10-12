using System.IO;

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

        public CppMidCompiler(CppBuilder builder, CppClass cls, CppMethod method, ExceptionHandlingRegion mainRegion, VReg[] args, VReg[] locals)
        {
            m_builder = builder;
            m_cls = cls;
            m_method = method;
            m_mainRegion = mainRegion;
            m_args = args;
            m_locals = locals;

            m_instructionStream = new MemoryStream();
            m_instructionWriter = new StreamWriter(m_instructionStream);
            m_regAllocator = new CppRegisterAllocator();
        }

        private void EmitRegion(ExceptionHandlingRegion region)
        {
            CppRegionEmitter emitter = new CppRegionEmitter(region, m_regAllocator);
            emitter.Emit(m_instructionWriter);
        }

        public void EmitAll(Stream baseStream)
        {
            EmitRegion(m_mainRegion);

            m_instructionWriter.Flush();
            m_instructionStream.WriteTo(baseStream);
        }
    }
}
