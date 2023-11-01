// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
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
			if (e.PropertyName is not nameof(IPersistableData.IsDirty))
			{
				if (sender is IPersistableData persistable)
				{
					persistable.IsDirty = true;
				}
				else
				{
					Debug.Fail("The sender is expected to be persistable.");
				}
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
