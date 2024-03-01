// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace Nerdbank.Zcash.App.Models;

internal static partial class ModelUtilities
{
	/// <summary>
	/// Marks an object as dirty when some persistable object it owns becomes dirty.
	/// </summary>
	/// <param name="owner">The containing object.</param>
	/// <param name="child">The child object.</param>
	internal static void StartWatchingForDirtyChild(this IPersistableData owner, IPersistableData child)
	{
		child.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName is nameof(IPersistableData.IsDirty))
			{
				if (sender is IPersistableData persistable)
				{
					if (persistable.IsDirty)
					{
						owner.IsDirty = true;
					}
				}
				else
				{
					Debug.Fail("The sender is expected to be persistable.");
				}
			}
		};
	}

	/// <summary>
	/// Marks an object as dirty when some persistable object it owns becomes dirty.
	/// </summary>
	/// <param name="owner">The containing object.</param>
	/// <param name="children">The child objects to watch. If this is an observable collection, collection changes will mark the <paramref name="owner"/> dirty as well.</param>
	internal static void StartWatchingForDirtyChildren(this IPersistableData owner, IEnumerable<IPersistableData> children)
	{
		foreach (IPersistableData child in children)
		{
			owner.StartWatchingForDirtyChild(child);
		}

		// TODO: we should watch each child added to the collection too, so that after a Save, we still get marked dirty when the new child is modified.
		if (children is INotifyCollectionChanged collection)
		{
			owner.MarkSelfDirtyOnCollectionChanged(collection);
		}
	}

	/// <summary>
	/// Sets the <see cref="IPersistableData.IsDirty"/> flag on an object
	/// when any of its other properties change.
	/// </summary>
	/// <param name="data">The persistable object.</param>
	internal static void MarkSelfDirtyOnPropertyChanged(this IPersistableData data)
	{
		data.PropertyChanged += static (sender, e) =>
		{
			if (sender is IPersistableData persistable)
			{
				if (persistable.IsPersisted(e.PropertyName))
				{
					persistable.IsDirty = true;
				}
			}
			else
			{
				Debug.Fail("The sender is expected to be persistable.");
			}
		};
	}

	internal static void MarkSelfDirtyOnCollectionChanged(this IPersistableData data, INotifyCollectionChanged collection)
	{
		collection.CollectionChanged += (sender, e) =>
		{
			data.IsDirty = true;
		};
	}

	/// <summary>
	/// Invokes a delegate when a property on any element in a collection changes, or the collection itself changes.
	/// </summary>
	/// <typeparam name="TCollection">The type of the collection.</typeparam>
	/// <typeparam name="TElement">The type of element stored by the collection.</typeparam>
	/// <param name="collection">The collection to watch.</param>
	/// <param name="memberName">The name of the property to watch for changes.</param>
	/// <param name="elementChanged">The delegate to invoke when the property changes with the element that changed; or invoked with <see langword="null" /> if the collection itself changed.</param>
	internal static void NotifyOnCollectionElementMemberChanged<TCollection, TElement>(this TCollection collection, string memberName, Action<TElement?> elementChanged)
		where TCollection : IEnumerable<TElement>, INotifyCollectionChanged
		where TElement : class, INotifyPropertyChanged
	{
		collection.CollectionChanged += (s, e) =>
		{
			if (e.NewItems is not null)
			{
				foreach (TElement element in e.NewItems)
				{
					if (element is not null)
					{
						element.PropertyChanged += OnPropertyChanged;
					}
				}
			}

			if (e.OldItems is not null)
			{
				foreach (TElement element in e.OldItems)
				{
					if (element is not null)
					{
						element.PropertyChanged -= OnPropertyChanged;
					}
				}
			}

			elementChanged(null);
		};

		foreach (TElement element in collection)
		{
			if (element is not null)
			{
				element.PropertyChanged += OnPropertyChanged;
			}
		}

		void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == memberName)
			{
				elementChanged((TElement?)sender);
			}
		}
	}

	internal static void ClearDirtyFlag(this IEnumerable<IPersistableData> collection)
	{
		foreach (IPersistableData member in collection)
		{
			member.IsDirty = false;
		}
	}

	internal static void SetIsDirty(this IPersistableDataHelper model, ref bool isDirty, bool value)
	{
		if (isDirty != value)
		{
			if (!value)
			{
				model.ClearDirtyFlagOnMembers();
			}

			isDirty = value;
			model.OnPropertyChanged(nameof(IPersistableData.IsDirty));
		}
		else if (value)
		{
			// Always raise this, because getting dirty 'again' when we're already dirty
			// should still trigger a fresh Save to make sure we don't drop anything.
			model.OnPropertyChanged(nameof(IPersistableData.IsDirty));
		}
	}
}
