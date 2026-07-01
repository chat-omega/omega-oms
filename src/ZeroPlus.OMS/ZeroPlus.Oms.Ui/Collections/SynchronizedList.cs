using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Collections
{
    public class SynchronizedList<T>
    {
        private readonly List<T> _list = new();
        private readonly object _lock = new();

        public void Add(T item)
        {
            lock (_lock)
                _list.Add(item);
        }

        public T[] ToArray()
        {
            lock (_lock)
                return _list.ToArray();
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _list.Count;
            }
        }
    }
}
