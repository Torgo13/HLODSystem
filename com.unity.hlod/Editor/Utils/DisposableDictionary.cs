using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.HLODSystem.Utils
{
    public class DisposableDictionary<TKey, TValue> : IDisposable, IDictionary<TKey, TValue> 
        where TValue:IDisposable
    {
#if BUGFIX
        private readonly System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> m_dic
            = new System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>();
#else
        private Dictionary<TKey, TValue> m_dic = new Dictionary<TKey, TValue>();
#endif // BUGFIX
        // ReSharper disable Unity.PerformanceAnalysis
        public void Dispose()
        {
            foreach (var value in m_dic.Values)
            {
                value.Dispose();
            }
            m_dic.Clear();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
#if BUGFIX
            return m_dic.GetEnumerator();
#else
            return ((IDictionary<TKey, TValue>) m_dic).GetEnumerator();
#endif // BUGFIX
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>) m_dic).Add(item);
        }

        public void Clear()
        {
            ((IDictionary<TKey, TValue>) m_dic).Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>) m_dic).Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>) m_dic).CopyTo(array, arrayIndex);
            
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>) m_dic).Remove(item);
        }

        public int Count { get => ((IDictionary<TKey, TValue>) m_dic).Count; }
        public bool IsReadOnly { get => ((IDictionary<TKey, TValue>) m_dic).IsReadOnly; }
#if BUGFIX
        public void Add(TKey key, TValue value)
        {
            _ = m_dic.TryAdd(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            return m_dic.ContainsKey(key);
        }

        public bool Remove(TKey key)
        {
            return m_dic.Remove(key, out _);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return m_dic.TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get => m_dic[key];
            set => m_dic[key] = value;
        }

        public ICollection<TKey> Keys { get => m_dic.Keys; }
        public ICollection<TValue> Values { get => m_dic.Values; }
#else
        public void Add(TKey key, TValue value)
        {
            ((IDictionary<TKey, TValue>) m_dic).Add(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            return ((IDictionary<TKey, TValue>) m_dic).ContainsKey(key);
        }

        public bool Remove(TKey key)
        {
            return ((IDictionary<TKey, TValue>) m_dic).Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return ((IDictionary<TKey, TValue>) m_dic).TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get => ((IDictionary<TKey, TValue>) m_dic)[key];
            set => ((IDictionary<TKey, TValue>) m_dic)[key] = value;
        }

        public ICollection<TKey> Keys { get => ((IDictionary<TKey, TValue>) m_dic).Keys; }
        public ICollection<TValue> Values { get => ((IDictionary<TKey, TValue>) m_dic).Values; }
#endif // BUGFIX
    }
}