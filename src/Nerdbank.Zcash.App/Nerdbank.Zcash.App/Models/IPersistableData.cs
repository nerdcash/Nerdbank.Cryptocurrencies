// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using MessagePack;

namespace Nerdbank.Zcash.App.Models;

public interface IPersistableData : INotifyPropertyChanged, IMessagePackSerializationCallbackReceiver
{
	bool IsDirty { get; set; }

	void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
	{
		this.IsDirty = false;
	}

	void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
	{
	}
}
