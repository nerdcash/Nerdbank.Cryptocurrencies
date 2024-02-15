// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Humanizer;

namespace Nerdbank.Zcash.Cli;

internal class SyncCommand : WalletUserCommandBase
{
	internal SyncCommand()
	{
	}

	[SetsRequiredMembers]
	internal SyncCommand(WalletUserCommandBase copyFrom)
		: base(copyFrom)
	{
	}

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
		Stopwatch syncTimer = Stopwatch.StartNew();
		int? lastPercentCompleteReported = null;
		string? lastErrorReported = null;
		LightWalletClient.SyncResult syncResult = await client.DownloadTransactionsAsync(
			new Progress<LightWalletClient.SyncProgress>(p =>
			{
				if (p.Total > 0)
				{
					int newPercent = (int)(100 * p.Current / p.Total);
					if (newPercent != lastPercentCompleteReported)
					{
						this.Console.WriteLine($"{newPercent}% complete");
						lastPercentCompleteReported = newPercent;
					}

					if (lastErrorReported != p.LastError)
					{
						this.Console.WriteLine(p.LastError is null ? "Last error resolved." : $"Non-fatal error: {p.LastError}");
						lastErrorReported = p.LastError;
					}
				}
			}),
			new Progress<IReadOnlyCollection<Transaction>>(transactions =>
			{
				foreach (Transaction tx in transactions)
				{
					HistoryCommand.PrintTransaction(this.Console, tx);
				}
			}),
			cancellationToken);

		this.Console.WriteLine($"Sync 100% complete. Scanned to block {syncResult.LatestBlock} in {syncTimer.Elapsed.Humanize(2, minUnit: Humanizer.Localisation.TimeUnit.Second)}.");

		return 0;
	}
}
