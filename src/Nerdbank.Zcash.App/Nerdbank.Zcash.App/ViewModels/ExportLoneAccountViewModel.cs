// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class ExportLoneAccountViewModel : ExportAccountViewModelBase
{
	[Obsolete("For design-time use only.", error: true)]
	public ExportLoneAccountViewModel()
		: this(new DesignTimeViewModelServices(), new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), ZcashNetwork.TestNet)))
	{
	}

	public ExportLoneAccountViewModel(IViewModelServices viewModelServices, ZcashAccount account)
		: base(viewModelServices, account.BirthdayHeight)
	{
		this.FullViewingKey = account.FullViewing?.UnifiedKey;
		this.IncomingViewingKey = account.IncomingViewing?.UnifiedKey;
	}

	public string FullViewingKeyCaption => "Full viewing key";

	public string? FullViewingKey { get; }

	public string IncomingViewingKeyCaption => "Incoming viewing key";

	public string? IncomingViewingKey { get; }
}
