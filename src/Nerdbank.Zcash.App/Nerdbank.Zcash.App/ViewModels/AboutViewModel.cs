// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.VisualStudio.Threading;

namespace Nerdbank.Zcash.App.ViewModels;

public class AboutViewModel : ViewModelBase, IHasTitle
{
	public static readonly ZcashAddress DonationReceiver = ZcashAddress.Decode("u17dsy4yqsyc6mxrmntvd6d48yy56j4lxfxhe096qrcwgz9ddu928ujtre05dfwp43mdsuxa7j7kmn5ksa94lh2lehwl302ffp9f4gnjvde3tlkcj8fm8xhl6dmxxz9x2jshmltj9hdlzsep9m029d7mapx0qg575s5fsyr2x03cfw4v9e");
	private readonly IViewModelServices viewModelServices;

	[Obsolete("For designer use only")]
	public AboutViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public AboutViewModel(IViewModelServices viewModelServices)
	{
		IObservable<bool> nonEmptyWallet = viewModelServices.WhenAnyValue(vm => vm.Wallet.IsEmpty, empty => !empty);

		this.WalletStorageLocation = new WalletStorageLocationViewModel(viewModelServices);
		this.DonateCommand = ReactiveCommand.Create(this.Donate, nonEmptyWallet);
		this.SupportCommand = ReactiveCommand.Create(() =>
		{
			this.ShowSupport();
			return Unit.Default;
		});
		this.ShowCapabilitiesCommand = ReactiveCommand.Create(this.ShowCapabilities);
		this.viewModelServices = viewModelServices;
		this.SelfUpdating = new UpdatingViewModel(viewModelServices.App.SelfUpdating);

		// Trigger an update check.
		viewModelServices.App.SelfUpdating.DownloadUpdateAsync(CancellationToken.None).Forget();
	}

	public string Title => AboutStrings.FormatAboutHeading(Strings.AppTitle);

	public string Message => AboutStrings.Message;

	public UpdatingViewModel SelfUpdating { get; }

	public WalletStorageLocationViewModel WalletStorageLocation { get; }

	public string LicenseCaption => AboutStrings.License;

	public string License => AboutStrings.License;

	public string SupportCommandCaption => AboutStrings.GetSupport;

	public ReactiveCommand<Unit, Unit> SupportCommand { get; }

	public string DonateCommandCaption => AboutStrings.Donate;

	public ReactiveCommand<Unit, SendingViewModel> DonateCommand { get; }

	public string ShowCapabilitiesCommandCaption => AboutStrings.ShowCapabilitiesCommandCaption;

	public ReactiveCommand<Unit, CapabilitiesViewModel> ShowCapabilitiesCommand { get; }

	public string Version => ThisAssembly.AssemblyInformationalVersion;

	public string VersionCaption => AboutStrings.Version;

	public SendingViewModel Donate()
	{
		SendingViewModel viewModel = new(this.viewModelServices);
		viewModel.LineItems[0].RecipientAddress = DonationReceiver;
		viewModel.LineItems[0].Memo = AboutStrings.FormatDonationMemo(Strings.AppTitle);

		return this.viewModelServices.NavigateTo(viewModel);
	}

	public CapabilitiesViewModel ShowCapabilities() => this.viewModelServices.NavigateTo(new CapabilitiesViewModel());

	public void ShowSupport() => Process.Start(new ProcessStartInfo("https://discord.gg/dR9v9SWMYF") { UseShellExecute = true });
}
