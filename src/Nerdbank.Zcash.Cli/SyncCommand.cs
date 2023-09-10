// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class SyncCommand
{
	internal required IConsole Console { get; init; }

	internal required string WalletPath { get; init; }

	internal bool TestNet { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	internal static Command BuildCommand()
	{
		Argument<string> walletPathArgument = new Argument<string>("wallet path", Strings.WalletPathArgumentDescription)
			.LegalFilePathsOnly();

		Option<bool> testNetOption = new("--testnet", Strings.TestNetOptionDescription);

		Option<Uri> lightServerUriOption = new("--lightserverUrl", Strings.LightServerUrlOptionDescription);

		Command command = new("sync", Strings.SyncCommandDescription)
		{
			walletPathArgument,
			testNetOption,
			lightServerUriOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new SyncCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(walletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(testNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(lightServerUriOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		using LightWalletClient client = Utilities.ConstructLightClient(
			this.LightWalletServerUrl,
			this.WalletPath,
			this.TestNet,
			watchMemPool: false);

		client.UpdateFrequency = TimeSpan.FromSeconds(3);

		await client.DownloadTransactionsAsync(
			new Progress<LightWalletClient.SyncProgress>(p =>
			{
				if (p.BatchTotal > 0)
				{
					this.Console.WriteLine($"{100 * p.BatchNum / p.BatchTotal}% complete");
				}
			}),
			cancellationToken);

		this.Console.WriteLine("Sync 100% complete");

		return 0;
	}
}
