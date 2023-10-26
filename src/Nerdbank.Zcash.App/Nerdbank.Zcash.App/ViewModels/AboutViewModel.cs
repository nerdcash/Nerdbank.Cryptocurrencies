// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

		this.DonateCommand = ReactiveCommand.Create(this.Donate, nonEmptyWallet);
		this.SupportCommand = ReactiveCommand.Create(() => { });
		this.viewModelServices = viewModelServices;
	}

	public string Title => $"About {Strings.AppTitle}";

	public string Message => "This app is a Zcash wallet. It seeks to the most intuitive wallet available, while being reliable, secure, and champion some of the best privacy features Zcash has to offer.";

	public string LicenseCaption => "License";

	public string License => """
		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
		""";

	public string SupportCommandCaption => "Get support";

	public ReactiveCommand<Unit, Unit> SupportCommand { get; }

	public string DonateCommandCaption => "Donate";

	public ReactiveCommand<Unit, Unit> DonateCommand { get; }

	public string Version => ThisAssembly.AssemblyInformationalVersion;

	public string VersionCaption => "You are using version";

	public void Donate()
	{
		if (this.viewModelServices.SelectedAccount is not null)
		{
			SendingViewModel sending = new(this.viewModelServices)
			{
				RecipientAddress = DonationReceiver,
				Memo = Strings.FormatDonationMemo(Strings.AppTitle),
			};
			this.viewModelServices.NavigateTo(sending);
		}
	}
}
