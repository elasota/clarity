////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace System.Threading
{

    using System;
    using System.Runtime.CompilerServices;

    [Clarity.ExportStub("System_Threading_AutoResetEvent.cpp")]
    public sealed class AutoResetEvent : WaitHandle
    {

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern public AutoResetEvent(bool initialState);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern public bool Reset();
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern public bool Set();
    }
}


