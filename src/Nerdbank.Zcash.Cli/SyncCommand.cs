// Copyright (c) IronPigeon, LLC. All rights reserved.
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

	internal bool Continually { get; init; }

	internal static Command BuildCommand()
	{
		Option<bool> continuallyOption = new("--continually", Strings.ContinuallyOptionDescription);

		Command command = new("sync", Strings.SyncCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			continuallyOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new SyncCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
				Continually = ctxt.ParseResult.GetValueForOption(continuallyOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal override async Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		Stopwatch syncTimer = Stopwatch.StartNew();
		int? lastPercentCompleteReported = null;
		string? lastErrorReported = null;
		LightWalletClient.SyncProgress syncResult = await client.DownloadTransactionsAsync(
			new Progress<LightWalletClient.SyncProgress>(p =>
			{
				if (p.TotalSteps > 0)
				{
					int newPercent = (int)p.PercentComplete;
					if (newPercent != lastPercentCompleteReported)
					{
						this.Console.WriteLine($"{newPercent,3}% complete ({p.LastFullyScannedBlock:N0}/{p.TipHeight:N0})");
						lastPercentCompleteReported = newPercent;
					}

					if (lastErrorReported != p.LastError)
					{
						this.Console.WriteLine(p.LastError is null ? "Last error resolved." : $"Non-fatal error: {p.LastError}");
						lastErrorReported = p.LastError;
					}
				}
			}),
			new Progress<IReadOnlyDictionary<ZcashAccount, IReadOnlyCollection<Transaction>>>(transactions =>
			{
				foreach (Transaction tx in transactions.Values.SelectMany(t => t))
				{
					HistoryCommand.PrintTransaction(this.Console, tx);
				}
			}),
			this.Continually,
			cancellationToken);

		this.Console.WriteLine($"Scanned to block {syncResult.LastFullyScannedBlock} in {syncTimer.Elapsed.Humanize(2, minUnit: TimeUnit.Second)}.");

		return 0;
	}
}
