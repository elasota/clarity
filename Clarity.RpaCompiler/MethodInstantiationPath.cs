using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class MethodInstantiationPath
    {
        MethodInstantiationPath Prev { get { return m_previous; } }
        MethodSpecTag MethodSpecTag { get { return m_methodSpec; } }
        CodeLocationTag CodeLocation { get { return m_codeLocation; } }

        private MethodInstantiationPath m_previous;
        private MethodSpecTag m_methodSpec;
        private CodeLocationTag m_codeLocation;

        public MethodInstantiationPath(MethodInstantiationPath prev, MethodSpecTag methodSpec, CodeLocationTag codeLocation)
        {
            m_previous = prev;
            m_methodSpec = methodSpec;
            m_codeLocation = codeLocation;
        }

        public override string ToString()
        {
            string result = "";
            if (m_previous != null)
                result = m_previous.ToString() + " --> ";
            result += m_methodSpec.ToString() + "@" + m_codeLocation.ToString();
            return result;
        }
    }
}
