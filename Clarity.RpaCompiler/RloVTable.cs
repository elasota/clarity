using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.RpaCompiler
{
    public class RloVTable
    {
        private MethodInstantiationPath m_instantiationPath;

        public MethodInstantiationPath InstantiationPath { get { return m_instantiationPath; } set { m_instantiationPath = value; } }
    }
}
