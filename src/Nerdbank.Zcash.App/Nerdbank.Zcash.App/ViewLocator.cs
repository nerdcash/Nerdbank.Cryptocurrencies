// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Nerdbank.Zcash.App;

/// <summary>
/// Locates a view for a given view model.
/// </summary>
public class ViewLocator : IDataTemplate
{
	/// <inheritdoc/>
	public Control? Build(object? data)
	{
		if (data is null)
		{
			return null;
		}

		var name = data.GetType().AssemblyQualifiedName!.Replace("ViewModel", "View");
		var type = Type.GetType(name);

		if (type != null)
		{
			return (Control)Activator.CreateInstance(type)!;
		}

		return new TextBlock { Text = name };
	}

	/// <inheritdoc/>
	public bool Match(object? data)
	{
		return data is ViewModelBase;
	}
}
