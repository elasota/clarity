using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
    [Clarity.ForceInterfaceRefsToObjectRefs]
    public interface IList<T> : ICollection<T>, IEnumerable<T>, IEnumerable
    {
        T this[int index] { get; set; }

        int IndexOf(T item);
        void Insert(int index, T item);
        void RemoveAt(int index);
    }
}
