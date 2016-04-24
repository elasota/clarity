using System;
using System.Text;

namespace System
{
    public struct Nullable<T> where T : struct
    {
        private bool m_hasValue;
        private T m_value;

        public bool HasValue
        {
            get
            {
                return m_hasValue;
            }
        }

        public T Value
        {
            get
            {
                if (m_hasValue == false)
                    throw new InvalidOperationException("Nullable value was retrieved while value was null");
                return m_value;
            }
        }

        public Nullable(T value)
        {
            m_hasValue = true;
            m_value = value;
        }

        public static implicit operator Nullable<T>(T value)
        {
            return new Nullable<T>(value);
        }

        public static explicit operator T(Nullable<T> value)
        {
            return value.Value;
        }

        public override bool Equals(object other)
        {
            if (m_hasValue)
                return other == null;
            return m_value.Equals(other);
        }

        public override int GetHashCode()
        {
            if (m_hasValue)
                return 0;
            return m_value.GetHashCode();
        }

        public T GetValueOrDefault()
        {
            if (m_hasValue)
                return m_value;
            return new T();
        }

        public T GetValueOrDefault(T defaultValue)
        {
            if (m_hasValue)
                return m_value;
            return defaultValue;
        }

        public override string ToString()
        {
            if (!m_hasValue)
                return "";
            return m_value.ToString();
        }
    }
}
