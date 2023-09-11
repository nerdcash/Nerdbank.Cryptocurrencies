// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;

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

	protected static ZcashAddress AddressParser(ArgumentResult result)
	{
		if (ZcashAddress.TryParse(result.Tokens[0].Value, out ZcashAddress? addr, out _, out string? errorMessage))
		{
			return addr;
		}

		result.ErrorMessage = errorMessage;
		return null!;
	}
}
