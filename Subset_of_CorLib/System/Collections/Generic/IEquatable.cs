using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
    public interface IEquatable<T>
    {
        bool Equals(T other);
    }
}
