using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ZeroPlus.Oms.Ui.Collections
{
    public class FastObservableCollection<T> : ObservableCollection<T>
    {

        private bool _blockNotification;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_blockNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        public void AddRange(List<T> list)
        {

            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (list.Count == 0)
            {
                return;
            }

            try
            {
                _blockNotification = true;
                foreach (T item in list)
                {
                    try
                    {
                        Add(item);
                    }
                    catch { /* ignored */}
                }
            }
            finally
            {
                _blockNotification = false;
                try
                {
                    base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list));
                }
                catch { /* ignored */}
            }
        }

        public void AddRange(IReadOnlyCollection<T> list)
        {

            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (list.Count == 0)
            {
                return;
            }

            try
            {
                _blockNotification = true;
                foreach (T item in list)
                {
                    try
                    {
                        Add(item);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _blockNotification = false;
                base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list));
            }
        }

        public void Refresh(int itemHandle)
        {
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, this[itemHandle]));
        }
        public void Refresh()
        {
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void AddItem(T item)
        {
            Add(item);
        }

        public void Sort(IComparer<T> comparer)
        {
            List<T> list = this.ToList();
            list.Sort(comparer);
            _blockNotification = true;
            Clear();
            AddRange(list);
        }
    }
}
