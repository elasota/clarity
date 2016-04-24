using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloLocal
    {
        private RloType m_type;
        private bool m_isArg;

        public RloLocal(Compiler compiler, HighLocal highLocal, RloInstantiationParameters instParams, bool isArg)
        {
            switch (highLocal.TypeOfType)
            {
                case HighLocal.ETypeOfType.ByRef:
                    {
                        RloValueType vt = new RloValueType(compiler, highLocal.Type, instParams);
                        vt = (RloValueType)compiler.InternRloType(vt);
                        RloRefType rt = new RloRefType(vt);
                        rt = (RloRefType)compiler.InternRloType(rt);
                        m_type = rt;
                    }
                    break;
                case HighLocal.ETypeOfType.TypedByRef:
                    m_type = compiler.InternedRloTypedRefType;
                    break;
                case HighLocal.ETypeOfType.Value:
                    {
                        RloValueType vt = new RloValueType(compiler, highLocal.Type, instParams);
                        vt = (RloValueType)compiler.InternRloType(vt);
                        m_type = vt;
                    }
                    break;
                default:
                    throw new Exception();
            }
        }
    }
}
