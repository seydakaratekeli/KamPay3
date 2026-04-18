using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace KamPay.Helpers
{
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        public ObservableRangeCollection() : base()
        {
        }

        public ObservableRangeCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new System.ArgumentNullException(nameof(collection));

            CheckReentrancy();
            var startIndex = Count;
            var addedItems = new List<T>(collection);
            foreach (var item in addedItems)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems, startIndex));
        }

        public void RemoveRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new System.ArgumentNullException(nameof(collection));

            CheckReentrancy();
            var removedItems = new List<T>(collection);
            foreach (var item in removedItems)
            {
                Items.Remove(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new System.ArgumentNullException(nameof(collection));

            CheckReentrancy();
            Items.Clear();
            foreach (var item in collection)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}