using System;
using System.Collections.Generic;
using Clarity.Rpa;

// This pass adds initialization and load instructions to all locals
namespace Clarity.RpaCompiler.Passes
{
    public class InitLocalsPass
    {
        private RloMethodBody m_methodBody;

        public InitLocalsPass(RloMethodBody methodBody)
        {
            m_methodBody = methodBody;
        }

        void Run()
        {
            bool isInstanceMethod;
            switch (m_methodBody.MethodSpec.MethodSlotType)
            {
                case MethodSlotType.Instance:
                case MethodSlotType.Virtual:
                    isInstanceMethod = true;
                    break;
                case MethodSlotType.Static:
                    isInstanceMethod = false;
                    break;
                default:
                    throw new Exception();
            }

            int firstArg = isInstanceMethod ? 1 : 0;
            int numArgs = firstArg + m_methodBody.MethodSignature.ParamTypes.Length;

            throw new NotImplementedException();
            
        }
    }
}
