// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class ZcashAmountFormatted : ViewModelBase
{
	private const int Precision = 8;
	private decimal amount;
	private string darkText = string.Empty;
	private string lightText = string.Empty;

	public ZcashAmountFormatted()
	{
		this.LinkProperty(nameof(this.Network), nameof(this.Units));
	}

	public ZcashAmountFormatted(decimal amount, ZcashNetwork network = ZcashNetwork.MainNet)
		: this()
	{
		this.Amount = amount;
		this.Network = network;
	}

	public decimal Amount
	{
		get => this.amount;
		set
		{
			this.RaiseAndSetIfChanged(ref this.amount, value);
			string fullValue = value.ToString("N8");
			this.DarkText = fullValue.TrimEnd('0');
			this.LightText = fullValue.Substring(this.DarkText.Length);
		}
	}

	public string DarkText
	{
		get => this.darkText;
		private set => this.RaiseAndSetIfChanged(ref this.darkText, value);
	}

	public string LightText
	{
		get => this.lightText;
		private set => this.RaiseAndSetIfChanged(ref this.lightText, value);
	}

	public string Units => this.Network.GetTickerName();
}
