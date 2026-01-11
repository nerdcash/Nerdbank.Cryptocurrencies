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
		Option<bool> continuallyOption = new("--continually")
		{
			Description = Strings.ContinuallyOptionDescription,
		};

		Command command = new("sync", Strings.SyncCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			continuallyOption,
		};

		command.SetAction(async (parseResult, cancellationToken) =>
		{
			return await new SyncCommand
			{
				WalletPath = parseResult.GetValue(WalletPathArgument)!,
				TestNet = parseResult.GetValue(TestNetOption),
				LightWalletServerUrl = parseResult.GetValue(LightServerUriOption),
				Continually = parseResult.GetValue(continuallyOption),
			}.ExecuteAsync(cancellationToken);
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
						Console.WriteLine($"{newPercent,3}% complete ({p.LastFullyScannedBlock:N0}/{p.TipHeight:N0})");
						lastPercentCompleteReported = newPercent;
					}

					if (lastErrorReported != p.LastError)
					{
						Console.WriteLine(p.LastError is null ? "Last error resolved." : $"Non-fatal error: {p.LastError}");
						lastErrorReported = p.LastError;
					}
				}
			}),
			new Progress<IReadOnlyDictionary<ZcashAccount, IReadOnlyCollection<Transaction>>>(transactions =>
			{
				foreach (Transaction tx in transactions.Values.SelectMany(t => t))
				{
					HistoryCommand.PrintTransaction(tx);
				}
			}),
			this.Continually,
			cancellationToken);

		Console.WriteLine($"Scanned to block {syncResult.LastFullyScannedBlock} in {syncTimer.Elapsed.Humanize(2, minUnit: TimeUnit.Second)}.");

		return 0;
	}
}
