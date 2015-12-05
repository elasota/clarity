////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////namespace System.Reflection
namespace System.Reflection
{
    using System;
    using System.Runtime.CompilerServices;

    [Serializable()]
    [Clarity.ExportStub("System_Reflection_PropertyInfo.cpp")]
    abstract public class PropertyInfo : MemberInfo
    {
        public abstract Type PropertyType
        {
            get;
        }

        [Diagnostics.DebuggerStepThrough]
        [Diagnostics.DebuggerHidden]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern virtual Object GetValue(Object obj, Object[] index);
        [Diagnostics.DebuggerStepThrough]
        [Diagnostics.DebuggerHidden]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern virtual void SetValue(Object obj, Object value, Object[] index);
    }
}


