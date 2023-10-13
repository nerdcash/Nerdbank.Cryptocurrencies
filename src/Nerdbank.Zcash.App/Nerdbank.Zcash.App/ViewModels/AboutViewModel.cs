// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class AboutViewModel : ViewModelBase
{
	public AboutViewModel()
	{
		this.DonateCommand = ReactiveCommand.Create(() => { });
		this.SupportCommand = ReactiveCommand.Create(() => { });
	}

	public string Title => $"About {Strings.AppTitle}";

	public string Message => "This app is a Zcash wallet. It seeks to the most intuitive wallet available, while being reliable, secure, and champion some of the best privacy features Zcash has to offer.";

	public string SupportCommandCaption => "Get support";

	public ReactiveCommand<Unit, Unit> SupportCommand { get; }

	public string DonateCommandCaption => "Donate";

	public ReactiveCommand<Unit, Unit> DonateCommand { get; }

	public string Version => ThisAssembly.AssemblyInformationalVersion;

	public string VersionCaption => "You are using version";
}
