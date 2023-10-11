// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class ViewModelBase : ReactiveObject
{
	// TODO: replace this with a real check on the ZcashAccount.
	public ZcashNetwork Network { get; set; } = ZcashNetwork.MainNet;

	protected Security ZcashSecurity => this.Network.AsSecurity();

	protected void LinkProperty(string basePropertyName, string dependentPropertyName)
	{
		this.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanged(dependentPropertyName);
			}
		};

		this.PropertyChanging += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanging(dependentPropertyName);
			}
		};
	}
}
