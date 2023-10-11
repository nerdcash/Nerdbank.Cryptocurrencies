// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using DynamicData.Binding;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Views;

public partial class SecurityAmountDisplay : UserControl
{
	public static readonly StyledProperty<SecurityAmount> ValueProperty = AvaloniaProperty.Register<SecurityAmountDisplay, SecurityAmount>(nameof(Value));

	public static readonly StyledProperty<bool> IsUnitsVisibleProperty = AvaloniaProperty.Register<SecurityAmountDisplay, bool>(nameof(IsUnitsVisible), defaultValue: true);

	public SecurityAmountDisplay()
	{
		this.InitializeComponent();
	}

	public SecurityAmount Value
	{
		get => this.GetValue(ValueProperty);
		set => this.SetValue(ValueProperty, value);
	}

	public bool IsUnitsVisible
	{
		get => this.GetValue(IsUnitsVisibleProperty);
		set => this.SetValue(IsUnitsVisibleProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change?.Property == ValueProperty)
		{
			this.DataContext = new SecurityAmountFormatted(change.NewValue is null ? default : (SecurityAmount)change.NewValue);
		}
	}
}
