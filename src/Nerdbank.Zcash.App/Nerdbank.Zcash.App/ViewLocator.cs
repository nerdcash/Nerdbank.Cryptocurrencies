// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nerdbank.Zcash.App.ViewModels;

namespace Nerdbank.Zcash.App;

public class ViewLocator : IDataTemplate
{
	public Control? Build(object data)
	{
		if (data is null)
		{
			return null;
		}

		var name = data.GetType().FullName!.Replace("ViewModel", "View");
		var type = Type.GetType(name);

		if (type != null)
		{
			return (Control)Activator.CreateInstance(type)!;
		}

		return new TextBlock { Text = name };
	}

	public bool Match(object? data)
	{
		return data is ViewModelBase;
	}
}
