using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.HLODSystem.Utils
{
    public sealed class DisposableBag<T> : IDisposable, IList<T>, ICollection<T>, IEnumerable<T>
        where T : IDisposable
    {
        readonly System.Collections.Concurrent.ConcurrentBag<T> m_list
            = new System.Collections.Concurrent.ConcurrentBag<T>();
        
        public T[] ToArray() => m_list.ToArray();

        // ReSharper disable Unity.PerformanceAnalysis
        public void Dispose()
        {
            foreach (var item in m_list)
            {
                item.Dispose();
            }
            
            m_list.Clear();
        }

        public IEnumerator<T> GetEnumerator() => m_list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => m_list.GetEnumerator();

        public void Add(T item) => m_list.Add(item);

        public void AddRange(IEnumerable<T> collection)
        {
            foreach (var item in collection)
            {
                m_list.Add(item);
            }
        }
        
        public void Clear() => m_list.Clear();

        public bool Contains(T item)
        {
            foreach (var t in m_list)
            {
                if (t.Equals(item))
                    return true;
            }
            
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public int Count => m_list.Count;
        public bool IsReadOnly => false;
        public int IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public T this[int index]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }
    
    public class DisposableList<T> : IDisposable, IList<T>, ICollection<T>, IEnumerable<T> 
        where T : IDisposable
    {
        readonly List<T> m_list;

        public DisposableList(int capacity = 0)
        {
            m_list = new List<T>(capacity);
        }

        public int Capacity { get => m_list.Capacity; set => m_list.Capacity = value; }
        public T[] ToArray() => m_list.ToArray();

        public void EnsureCapacity(int capacity)
        {
            if (m_list.Capacity < capacity)
                m_list.Capacity = capacity;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Dispose()
        {
            for (int i = 0; i < m_list.Count; ++i)
            {
                m_list[i].Dispose();
            }
            m_list.Clear();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            m_list.Add(item);
        }
        
        public void AddRange(IEnumerable<T> collection)
        {
            m_list.AddRange(collection);
        }

        public void Clear()
        {
            m_list.Clear();
        }

        public bool Contains(T item)
        {
            return m_list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_list.CopyTo(array, arrayIndex);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public bool Remove(T? item)
        {
            item?.Dispose();
            return m_list.Remove(item!);
        }

        public int Count
        {
            get
            {
                return m_list.Count;
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public int IndexOf(T item)
        {
            return m_list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            m_list.Insert(index, item);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void RemoveAt(int index)
        {
            if (m_list[index] != null)
            {
                m_list[index].Dispose();
            }

            m_list.RemoveAt(index);
        }

        public T this[int index]
        {
            get => m_list[index];
            set => m_list[index] = value;
        }
    }
}