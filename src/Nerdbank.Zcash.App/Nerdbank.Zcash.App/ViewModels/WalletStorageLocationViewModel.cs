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

	public string WalletStorageLocation => WalletStorageLocationStrings.FormatWalletStorageLocation(this.viewModelServices.App.AppPlatformSettings.ConfidentialDataPath);

	public bool WalletIsEncrypted => this.viewModelServices.App.AppPlatformSettings.ConfidentialDataPathIsEncrypted;

	public string WalletEncryptionExplanation => this.viewModelServices.App.AppPlatformSettings.ConfidentialDataPathIsEncrypted
		? WalletStorageLocationStrings.WalletEncryptedExplanation
		: WalletStorageLocationStrings.WalletNotEncryptedExplanation;
}
