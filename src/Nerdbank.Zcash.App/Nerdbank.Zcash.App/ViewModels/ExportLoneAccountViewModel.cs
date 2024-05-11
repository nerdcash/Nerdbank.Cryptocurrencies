// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class ExportLoneAccountViewModel : ExportAccountViewModelBase
{
	[Obsolete("For design-time use only.", error: true)]
	public ExportLoneAccountViewModel()
		: this(new DesignTimeViewModelServices(), new Account(new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), ZcashNetwork.TestNet))))
	{
	}

	public ExportLoneAccountViewModel(IViewModelServices viewModelServices, Account account)
		: base(viewModelServices, account)
	{
		this.FullViewingKey = account.ZcashAccount.FullViewing?.UnifiedKey;
		this.IncomingViewingKey = account.ZcashAccount.IncomingViewing?.UnifiedKey;
	}

	public string FullViewingKeyCaption => "Full viewing key";

	public string? FullViewingKey { get; }

	public string IncomingViewingKeyCaption => "Incoming viewing key";

	public string? IncomingViewingKey { get; }
}
