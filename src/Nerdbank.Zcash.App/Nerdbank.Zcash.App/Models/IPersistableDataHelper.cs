// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Nerdbank.Zcash.App.Models;

internal interface IPersistableDataHelper : IPersistableData
{
	/// <summary>
	/// Raises the <see cref="INotifyPropertyChanged.PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the property that was changed.</param>
	void OnPropertyChanged(string propertyName);

	void ClearDirtyFlagOnMembers();
}
