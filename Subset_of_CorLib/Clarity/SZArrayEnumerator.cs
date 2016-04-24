using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Clarity
{
    internal class SZArrayEnumerator<T> : IEnumerator<T>
    {
        private T[] m_array;
        private int m_index;

        internal SZArrayEnumerator(T[] array)
        {
            m_array = array;
            m_index = -1;
        }

        object IEnumerator.Current
        {
            get
            {
                return m_array[m_index];
            }
        }

        T IEnumerator<T>.Current
        {
            get
            {
                return m_array[m_index];
            }
        }

        void IDisposable.Dispose()
        {
        }

        bool IEnumerator.MoveNext()
        {
            m_index++;
            return m_index < m_array.Length;
        }

        bool IEnumerator<T>.MoveNext()
        {
            m_index++;
            return m_index < m_array.Length;
        }

        void IEnumerator.Reset()
        {
            m_index = -1;
        }
    }
}
