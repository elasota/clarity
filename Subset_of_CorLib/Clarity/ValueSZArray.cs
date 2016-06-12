using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Clarity
{
    public abstract class ValueSZArray<T> : Array, IList<T>, ICollection<T>, IEnumerable<T>
        where T : struct
    {
        private T[] ThisAsArray
        {
            get
            {
                return (T[])(object)this;
            }
        }

        int ICollection<T>.Count
        {
            get
            {
                return this.Length;
            }
        }

        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return false;
            }
        }

        T IList<T>.this[int index]
        {
            get
            {
                return ThisAsArray[index];
            }
            set
            {
                ThisAsArray[index] = value;
            }
        }

        int IList<T>.IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<T>.Contains(T item)
        {
            int length = this.Length;
            T[] thisArray = ThisAsArray;

            for (int i = 0; i < length; i++)
                if (thisArray[i].Equals(item))
                    return true;
            return false;
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            int length = this.Length;
            T[] thisArray = ThisAsArray;
            for (int i = 0; i < length; i++)
                array[arrayIndex + i] = thisArray[i];
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new SZArrayEnumerator<T>(ThisAsArray);
        }

        public override bool Equals(object obj)
        {
            if (obj == this)
                return true;

            if (obj == null)
                return false;

            if (obj.GetType() != this.GetType())
                return false;

            T[] otherArray = (T[])obj;
            T[] thisArray = (T[])(object)this;

            int len = thisArray.Length;
            if (otherArray.Length != this.Length)
                return false;

            for (int i = 0; i < len; i++)
            {
                if (!thisArray[i].Equals(otherArray[i]))
                    return false;
            }
            return true;
        }
    }
}
