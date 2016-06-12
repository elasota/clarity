using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Clarity
{
    public abstract class RefSZArray : Array, IList<object>, ICollection<object>, IEnumerable<object>
    {
        private object[] ThisAsArray
        {
            get
            {
                return (object[])(object)this;
            }
        }

        int ICollection<object>.Count
        {
            get
            {
                return this.Length;
            }
        }

        bool ICollection<object>.IsReadOnly
        {
            get
            {
                return false;
            }
        }

        object IList<object>.this[int index]
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

        int IList<object>.IndexOf(object item)
        {
            object[] array = ThisAsArray;
            int length = array.Length;

            if (item == null)
            {
                for (int i = 0; i < length; i++)
                    if (array[i] == null)
                        return i;
            }
            else
            {
                for (int i = 0; i < length; i++)
                    if (item.Equals(array[i]))
                        return i;
            }
            return -1;
        }

        public override bool Equals(object obj)
        {
            if (obj == this)
                return true;

            if (obj == null)
                return false;

            if (obj.GetType() != this.GetType())
                return false;

            object[] otherArray = (object[])obj;
            object[] thisArray = (object[])(object)this;

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

        void IList<object>.Insert(int index, object item)
        {
            throw new NotSupportedException();
        }

        void IList<object>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        void ICollection<object>.Add(object item)
        {
            throw new NotSupportedException();
        }

        void ICollection<object>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<object>.Contains(object item)
        {
            return ((IList<object>)(this)).IndexOf(item) != -1;
        }

        void ICollection<object>.CopyTo(object[] array, int arrayIndex)
        {
            int length = this.Length;
            object[] thisArray = ThisAsArray;
            for (int i = 0; i < length; i++)
                array[arrayIndex + i] = thisArray[i];
        }

        bool ICollection<object>.Remove(object item)
        {
            throw new NotSupportedException();
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator()
        {
            return new SZArrayEnumerator<object>(ThisAsArray);
        }
    }
}
