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
			RequiredSelectedAccountOption,
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
				SelectedAccountAddress = ctxt.ParseResult.GetValueForOption(RequiredSelectedAccountOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal static void PrintTransaction(IConsole console, Transaction tx)
	{
		console.WriteLine($"{tx.When?.ToLocalTime():yyyy-MM-dd hh:mm:ss tt}  {tx.NetChange,13:N8} Block: {tx.MinedHeight} Txid: {tx.TransactionId}");
		const string indentation = "                      ";

		foreach (Transaction.SendItem send in tx.Outgoing)
		{
			console.WriteLine($"{indentation} -{send.Amount,13:N8} {FormatMemo(send.Memo)} {send.ToAddress}");
		}

		foreach (Transaction.RecvItem recv in tx.Incoming)
		{
			if (!recv.IsChange)
			{
				console.WriteLine($"{indentation} +{recv.Amount,13:N8} {recv.Pool} {FormatMemo(recv.Memo)} {recv.ToAddress}");
			}
		}

		if (tx.IsOutgoing)
		{
			console.WriteLine($"{indentation} -{tx.Fee,13:N8} transaction fee");
		}
	}

	internal override async Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		int exitCode = await base.ExecuteAsync(client, cancellationToken);
		if (exitCode != 0)
		{
			return exitCode;
		}

		List<Transaction> txs = client.GetDownloadedTransactions(this.SelectedAccount!, this.StartingBlock);
		foreach (Transaction tx in txs)
		{
			PrintTransaction(this.Console, tx);
		}

		return 0;
	}

	private static string? FormatMemo(Memo memo)
	{
		if (memo.MemoFormat == Zip302MemoFormat.MemoFormat.ProprietaryData)
		{
			return "(proprietary)";
		}

		return memo.Message?.ReplaceLineEndings("\\n");
	}
}
