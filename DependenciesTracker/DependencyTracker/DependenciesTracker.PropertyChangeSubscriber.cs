﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using JetBrains.Annotations;

namespace DependenciesTracker
{
    internal partial class DependenciesTracker<T>
    {
        private sealed class RootObjectSubscriber : SubscriberBase
        {
            public RootObjectSubscriber([NotNull] object effectiveObject, [NotNull] PathItem<T> pathItem,
                [NotNull] Action<PathItem<T>> onChanged)
                : base(effectiveObject, pathItem, onChanged)
            {
                Ancestors.Add(CreateSubscriber(effectiveObject, pathItem.Ancestor, onChanged));
            }

            public override void Dispose() { }
        }

        private sealed class PropertyChangeSubscriber : SubscriberBase
        {
            [CanBeNull]
            private IDisposable _observer;

            public PropertyChangeSubscriber([NotNull] object effectiveObject,
                [NotNull] PathItem<T> pathItem, [NotNull] Action<PathItem<T>> onChanged)
                : base(effectiveObject, pathItem, onChanged)
            {

                var notifyPropertyChange = effectiveObject as INotifyPropertyChanged;

                if (notifyPropertyChange != null)
                    Subscribe(notifyPropertyChange);

                Ancestor = InitAncestor();
            }

            [CanBeNull]
            private IDisposable InitAncestor()
            {
                if (PathItem.Ancestor == null)
                    return null;

                var ancestorEffectiveObject = PathItem.PropertyGetter(EffectiveObject);

                return ancestorEffectiveObject == null ? null : CreateSubscriber(ancestorEffectiveObject, PathItem.Ancestor, OnChanged);
            }

            [CanBeNull]
            private IDisposable Ancestor { get; set; }

            private void Subscribe([NotNull] INotifyPropertyChanged notifyPropertyChange)
            {
                _observer = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    h => notifyPropertyChange.PropertyChanged += h,
                    h => notifyPropertyChange.PropertyChanged -= h)
                    .Where(sa => sa.EventArgs.PropertyName == PathItem.PropertyName)
                    .Subscribe(_ => OnObservedPropertyChanged());
            }

            private void OnObservedPropertyChanged()
            {
                if (Ancestor != null)
                    Ancestor.Dispose();

                Ancestor = InitAncestor();
                OnChanged(PathItem);
            }

            public override void Dispose()
            {
                if (_observer != null)
                    _observer.Dispose();
                if (Ancestor != null)
                    Ancestor.Dispose();
            }
        }

        private sealed class CollectionChangeSubscriber : SubscriberBase
        {
            private IDisposable _observer;

            private new IEnumerable EffectiveObject
            {
                get { return (IEnumerable)base.EffectiveObject; }
            }
            public CollectionChangeSubscriber([NotNull] object effectiveObject, [NotNull] PathItem<T> pathItem, [NotNull] Action<PathItem<T>> onChanged)
                : base(effectiveObject, pathItem, onChanged)
            {
                Debug.Assert(pathItem.IsCollection);

                var notifyCollectionChange = effectiveObject as INotifyCollectionChanged;
                if (notifyCollectionChange != null)
                    SubscribeToCollectionChange(notifyCollectionChange);

                foreach (var ancestor in InitAncestors(EffectiveObject))
                    Ancestors.Add(ancestor);
            }

            private IEnumerable<SubscriberBase> InitAncestors(IEnumerable items)
            {
                if (PathItem.Ancestor == null)
                    return new SubscriberBase[0];

                return items.Cast<object>().Where(item => item != null).Select(item => CreateSubscriber(item, PathItem.Ancestor, OnChanged));
            }

            private void SubscribeToCollectionChange([NotNull] INotifyCollectionChanged notifyCollectionChange)
            {
                Debug.Assert(notifyCollectionChange != null);

                _observer = Observable
                    .FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                        h => notifyCollectionChange.CollectionChanged += h,
                        h => notifyCollectionChange.CollectionChanged -= h)
                    .Subscribe(ep => CollectionItemsChanged(ep.EventArgs));
            }

            private const int _invalidCollectionIndexValue = -1;

            private void CollectionItemsChanged(NotifyCollectionChangedEventArgs eventArgs)
            {  
                if (PathItem.Ancestor != null)
                    UpdateAncestors(eventArgs);

                OnChanged(PathItem);
            }

            private void UpdateAncestors(NotifyCollectionChangedEventArgs eventArgs)
            {
                switch (eventArgs.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        CheckIndexValid(eventArgs.NewStartingIndex, NotifyCollectionChangedAction.Add);
                        Ancestors.Insert(eventArgs.NewStartingIndex,
                            CreateSubscriber(eventArgs.NewItems.Cast<object>().Single(), PathItem.Ancestor, OnChanged));
                        return;
                    case NotifyCollectionChangedAction.Remove:
                        CheckIndexValid(eventArgs.OldStartingIndex, NotifyCollectionChangedAction.Remove);
                        Ancestors[eventArgs.OldStartingIndex].Dispose();
                        Ancestors.RemoveAt(eventArgs.OldStartingIndex);
                        return;
                    case NotifyCollectionChangedAction.Replace:
                        CheckIndexValid(eventArgs.OldStartingIndex, NotifyCollectionChangedAction.Replace);
                        Ancestors[eventArgs.OldStartingIndex].Dispose();
                        Ancestors[eventArgs.OldStartingIndex] = CreateSubscriber(eventArgs.NewItems.Cast<object>().Single(),
                            PathItem.Ancestor, OnChanged);
                        return;
                    case NotifyCollectionChangedAction.Reset:
                        foreach (var ancestor in Ancestors)
                            ancestor.Dispose();
                        foreach (var ancestor in InitAncestors(EffectiveObject))
                            Ancestors.Add(ancestor);
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private static void CheckIndexValid(int index, NotifyCollectionChangedAction action)
            {
                if (index == _invalidCollectionIndexValue)
                    throw new NotSupportedException(string.Format("Processing {0} with unset index is not supported", action));
            }

            public override void Dispose()
            {
                if (_observer != null)
                    _observer.Dispose();

                foreach (var ancestor in Ancestors)
                    ancestor.Dispose();
            }
        }

        private abstract class SubscriberBase : IDisposable
        {
            private readonly object _effectiveObject;
            public readonly PathItem<T> PathItem;
            public readonly Action<PathItem<T>> OnChanged;
            private readonly IList<SubscriberBase> _ancestors = new List<SubscriberBase>();

            public object EffectiveObject
            {
                get { return _effectiveObject; }
            }

            [NotNull]
            protected IList<SubscriberBase> Ancestors
            {
                get { return _ancestors; }
            }

            public static SubscriberBase CreateSubscriber([NotNull] object effectiveObject, [NotNull] PathItem<T> pathItem, [NotNull] Action<PathItem<T>> onChanged)
            {
                if (pathItem.PropertyName == string.Empty)
                    return new RootObjectSubscriber(effectiveObject, pathItem, onChanged);

                if (pathItem.IsCollection)
                    return new CollectionChangeSubscriber(effectiveObject, pathItem, onChanged);

                return new PropertyChangeSubscriber(effectiveObject, pathItem, onChanged);
            }

            protected SubscriberBase([NotNull] object effectiveObject, [NotNull] PathItem<T> pathItem, [NotNull] Action<PathItem<T>> onChanged)
            {
                if (effectiveObject == null) throw new ArgumentNullException("effectiveObject");
                if (pathItem == null) throw new ArgumentNullException("pathItem");
                if (onChanged == null) throw new ArgumentNullException("onChanged");

                _effectiveObject = effectiveObject;
                PathItem = pathItem;
                OnChanged = onChanged;
            }


            public abstract void Dispose();
        }
    }
}