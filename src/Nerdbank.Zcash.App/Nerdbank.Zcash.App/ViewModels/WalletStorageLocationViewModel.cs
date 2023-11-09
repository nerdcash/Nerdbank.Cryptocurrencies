// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class WalletStorageLocationViewModel : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;

	[Obsolete("Design-time only.", error: true)]
	public WalletStorageLocationViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public WalletStorageLocationViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
	}

	public string WalletStorageLocation => $"Wallet storage location: \"{this.viewModelServices.App.AppPlatformSettings.ConfidentialDataPath}\".";

	public bool WalletIsEncrypted => this.viewModelServices.App.AppPlatformSettings.ConfidentialDataPathIsEncrypted;

	public string WalletEncryptionExplanation => this.viewModelServices.App.AppPlatformSettings.ConfidentialDataPathIsEncrypted
		? "Encryption is active so only your local device account can access your wallet. This guards against other accounts on this device stealing your wallet.\nViruses and malware that may be running under your same account may still be able to access your wallet."
		: "Encryption is not available on this device to protect your wallet.";
}
