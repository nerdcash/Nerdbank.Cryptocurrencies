// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;

namespace Nerdbank.Zcash.App.Views;

public partial class DecimalDisplay : UserControl
{
	public static readonly StyledProperty<bool> IsUnitsVisibleProperty = AvaloniaProperty.Register<DecimalDisplay, bool>(nameof(IsUnitsVisible), defaultValue: true);

	public DecimalDisplay()
	{
		this.InitializeComponent();
	}

	public bool IsUnitsVisible
	{
		get => this.GetValue(IsUnitsVisibleProperty);
		set => this.SetValue(IsUnitsVisibleProperty, value);
	}
}
