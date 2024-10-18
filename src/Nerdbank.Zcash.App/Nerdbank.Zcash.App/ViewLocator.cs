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

		Type? viewModelType = data.GetType();

		while (viewModelType is not null)
		{
			string name = viewModelType.AssemblyQualifiedName!.Replace("ViewModel", "View");
			Type? viewType = Type.GetType(name);
			if (viewType is not null)
			{
				return (Control)Activator.CreateInstance(viewType)!;
			}

			// We're missing a view.
			// If the view model is defined in another assembly, look at its base type(s)
			// until we find a view model defined in the same assembly as the view locator.
			if (viewModelType.Assembly != typeof(ViewLocator).Assembly)
			{
				viewModelType = viewModelType.BaseType;
			}
			else
			{
				// give up.
				break;
			}
		}

		return new TextBlock { Text = $"Missing view for view model {data.GetType().FullName}" };
	}

	/// <inheritdoc/>
	public bool Match(object? data)
	{
		return data is ViewModelBase;
	}
}
