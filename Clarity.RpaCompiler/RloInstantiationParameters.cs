using System;
using System.Collections.Generic;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class RloInstantiationParameters
    {
        private TypeSpecTag[] m_typeParams;
        private TypeSpecTag[] m_methodParams;

        public TypeSpecTag[] TypeParams { get { return m_typeParams; } }
        public TypeSpecTag[] MethodParams { get { return m_methodParams; } }

        public RloInstantiationParameters(TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            m_typeParams = typeParams;
            m_methodParams = methodParams;
        }
    }
}
