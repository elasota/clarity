﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;
using System;

namespace Clarity.RpaCompiler
{
    public class RloMethod
    {
        private bool m_isInternal;
        private RloMethodBody m_methodBody;

        public RloMethod(Compiler compiler, MethodSpecTag methodSpec)
        {
            CliClass cls = compiler.GetClosedClass(methodSpec.DeclaringClass);
            HighMethod method;
            {
                uint methodSlot;
                if (!cls.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodSlot))
                    throw new Exception("Couldn't find method");
                method = cls.Methods[methodSlot];
            }

            if (method.IsInternal)
            {
                m_isInternal = true;
                return;
            }

            TypeSpecTag[] typeParams = methodSpec.DeclaringClass.ArgTypes;
            TypeSpecTag[] methodParams = methodSpec.GenericParameters;

            if ((uint)methodParams.Length != method.MethodSignature.NumGenericParameters)
                throw new Exception("Method generic parameter count doesn't match argument count");

            RloInstantiationParameters instParams = new RloInstantiationParameters(typeParams, methodParams);
            m_methodBody = new RloMethodBody(compiler, method, methodSpec.DeclaringClass, cls.IsStruct, instParams);
        }
    }
}