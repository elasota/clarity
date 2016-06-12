using System.Collections.Generic;

namespace Clarity.Rpa
{
    public class UniqueQueue<TKey, TValue>
       where TValue : class, new()
    {
        private Dictionary<TKey, TValue> m_dict = new Dictionary<TKey, TValue>();
        private List<KeyValuePair<TKey, TValue>> m_list = new List<KeyValuePair<TKey, TValue>>();
        private int m_numDigested = 0;

        public IEnumerable<KeyValuePair<TKey, TValue>> AllInstances { get { return m_list; } }

        public TValue Lookup(TKey key)
        {
            TValue v;
            if (m_dict.TryGetValue(key, out v))
                return v;
            v = new TValue();
            m_dict.Add(key, v);
            m_list.Add(new KeyValuePair<TKey, TValue>(key, v));
            return v;
        }

        public bool HaveNext
        {
            get
            {
                return m_numDigested != m_list.Count;
            }
        }

        public KeyValuePair<TKey, TValue> GetNext()
        {
            if (!HaveNext)
                throw new System.IndexOutOfRangeException();
            return m_list[m_numDigested++];
        }
    }
}
