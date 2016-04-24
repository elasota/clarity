using System;
using System.Collections.Generic;

namespace System.Collections.Generic
{
    public interface IEnumerator<out T> : IDisposable, IEnumerator
    {
        T Current { get; }
        bool MoveNext();
    }
}
