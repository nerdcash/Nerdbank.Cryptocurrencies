// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.ReactiveUI;

namespace Nerdbank.Zcash.App.Views;

public partial class ImportAccountView : ReactiveUserControl<ImportAccountViewModel>
{
	public ImportAccountView()
	{
		this.InitializeComponent();
	}
}
