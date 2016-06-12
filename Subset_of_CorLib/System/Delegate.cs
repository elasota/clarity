////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////namespace System
namespace System
{

    using System;
    using System.Reflection;
    using System.Threading;
    using System.Runtime.CompilerServices;
    [Serializable()]
    [Clarity.ExportStub("System_Delegate.cpp")]
    public abstract class Delegate
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public override extern bool Equals(Object obj);

        public static Delegate Combine(Delegate a, Delegate b)
        {
            if (a == null)
                return b;
            if (b == null)
                return a;

            return CombineImpl(a.ConvertToMulticastImpl(), b.ConvertToMulticastImpl());
        }

        extern public MethodInfo Method
        {
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get;
        }

        public Object Target
        {
            get
            {
                return this.LastInvocation.TargetImpl;
            }
        }
        
        public static Delegate Remove(Delegate source, Delegate value)
        {
            return RemoveImpl(source, value.LastInvocation);
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern bool operator ==(Delegate d1, Delegate d2);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern bool operator !=(Delegate d1, Delegate d2);

        protected virtual Delegate ConvertToMulticastImpl()
        {
            return this;
        }

        private extern Delegate LastInvocation
        {
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get;
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern static Delegate CombineImpl(Delegate a, Delegate b);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern static Delegate RemoveImpl(Delegate source, Delegate value);

        private object TargetImpl
        {
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get;
        }
    }
}


