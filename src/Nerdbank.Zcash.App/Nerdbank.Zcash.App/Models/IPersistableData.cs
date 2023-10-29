// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Nerdbank.Zcash.App.Models;

public interface IPersistableData : INotifyPropertyChanged
{
	bool IsDirty { get; set; }
}
