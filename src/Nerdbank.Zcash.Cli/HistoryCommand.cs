// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class HistoryCommand : SyncFirstCommandBase
{
	internal uint StartingBlock { get; init; }

	internal static Command BuildCommand()
	{
		Option<uint> startingBlockOption = new("--from", Strings.HistoryStartingBlockOptionDescription);

		Command command = new("history", Strings.HistoryCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
			startingBlockOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new HistoryCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
				NoSync = ctxt.ParseResult.GetValueForOption(NoSyncOption),
				StartingBlock = ctxt.ParseResult.GetValueForOption(startingBlockOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal override async Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		int exitCode = await base.ExecuteAsync(client, cancellationToken);
		if (exitCode != 0)
		{
			return exitCode;
		}

		List<LightWalletClient.Transaction> txs = client.GetDownloadedTransactions(this.StartingBlock);
		foreach (LightWalletClient.Transaction tx in txs)
		{
			this.Console.WriteLine($"{tx.BlockNumber} {tx.When.ToLocalTime():yyyy-MM-dd hh:mm:ss tt} {tx.NetChange,13:N8}");
		}

		return 0;
	}
}
