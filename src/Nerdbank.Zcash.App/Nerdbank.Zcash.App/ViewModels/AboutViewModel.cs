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
}
