////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace System.Threading
{

    using System;
    using System.Runtime.CompilerServices;

    [Clarity.ExportStub("System_Threading_ManualResetEvent.cpp")]
    public sealed class ManualResetEvent : WaitHandle
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern public ManualResetEvent(bool initialState);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern public bool Reset();

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern public bool Set();
    }
}


