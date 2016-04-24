////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////namespace System.Reflection
namespace System.Reflection
{

    using System;
    using System.Runtime.CompilerServices;

    // This is defined to support VarArgs
    //typedef ArgIterator  va_list;
    [Serializable()]
    [Clarity.ExportStub("System_Reflection_RuntimeMethodInfo.cpp")]
    internal sealed class RuntimeMethodInfo : MethodInfo
    {
        public extern override Type ReturnType
        {
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get;
        }

        public extern override Type ReflectedType
        {
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get;
        }
    }
}   // Namespace


