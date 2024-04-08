// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Microsoft.VisualStudio.Threading;

namespace Nerdbank.Zcash.App.Views;

public partial class HomeScreenView : UserControl
{
	public HomeScreenView()
	{
		this.InitializeComponent();
	}

	protected override void OnInitialized()
	{
		base.OnInitialized();

		// The UI updating fails in design-time mode for some reason.
		// But you can add || true to the expression below to test in a launched app.
		if (Design.IsDesignMode)
		{
			App.Current?.SelfUpdating?.MockUpdateAsync(CancellationToken.None).Forget();
		}
	}
}
