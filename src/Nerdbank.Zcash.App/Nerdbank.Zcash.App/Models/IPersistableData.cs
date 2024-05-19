// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Reflection;
using MessagePack;

namespace Nerdbank.Zcash.App.Models;

public interface IPersistableData : INotifyPropertyChanged, IMessagePackSerializationCallbackReceiver
{
	bool IsDirty { get; set; }

	bool IsPersisted(string? propertyName) => propertyName is not null and not nameof(this.IsDirty)
		&& this.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetCustomAttribute<IgnoreMemberAttribute>() is null;

	void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
	{
		this.IsDirty = false;
	}

	void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
	{
	}
}
