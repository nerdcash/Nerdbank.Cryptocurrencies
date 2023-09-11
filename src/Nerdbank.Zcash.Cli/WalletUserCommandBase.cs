using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal abstract class WalletUserCommandBase
{
	internal required IConsole Console { get; init; }

	internal required string WalletPath { get; init; }

	internal bool TestNet { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	protected static Argument<string> WalletPathArgument { get; } = new Argument<string>("wallet path", Strings.WalletPathArgumentDescription).LegalFilePathsOnly();

	protected static Option<bool> TestNetOption { get; } = new("--testnet", Strings.TestNetOptionDescription);

	protected static Option<Uri> LightServerUriOption { get; } = new("--lightserverUrl", Strings.LightServerUrlOptionDescription);

	internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		using LightWalletClient client = Utilities.ConstructLightClient(
			this.LightWalletServerUrl,
			this.WalletPath,
			this.TestNet,
			watchMemPool: false);

		client.UpdateFrequency = TimeSpan.FromSeconds(3);

		return await this.ExecuteAsync(client, cancellationToken);
	}

	internal abstract Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken);
}
