// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class SyncCommand : WalletUserCommandBase
{
	internal static Command BuildCommand()
	{
		Command command = new("sync", Strings.SyncCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new SyncCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal override async Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
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
