using System;
using System.Collections.Generic;

namespace System.Collections.Generic
{
    [Clarity.ForceInterfaceRefsToObjectRefs]
    public interface ICollection<T> : IEnumerable<T>, IEnumerable
    {
        int Count { get; }

        bool IsReadOnly { get; }
        void Add(T item);
        void Clear();
        bool Contains(T item);
        void CopyTo(T[] array, int arrayIndex);
        bool Remove(T item);
    }
}
