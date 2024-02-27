// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class SendCommand : SyncFirstCommandBase
{
	internal required ZcashAddress Recipient { get; init; }

	internal required decimal Amount { get; init; }

	internal string? Memo { get; init; }

	internal static Command BuildCommand()
	{
		Argument<ZcashAddress> recipientArgument = new("recipient", Utilities.AddressParser, description: Strings.SendRecipientArgumentDescription);
		Argument<decimal> amountArgument = new("amount", Strings.SendAmountArgumentDescription);
		Option<string> memoOption = new("--memo", Strings.SendMemoOptionDescription);

		Command command = new("send", Strings.SendCommandDescription)
		{
			WalletPathArgument,
			recipientArgument,
			amountArgument,
			memoOption,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
			SpendingKeySeedOption,
			SpendingKeyAccountIndexOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new SendCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				Recipient = ctxt.ParseResult.GetValueForArgument(recipientArgument),
				Amount = ctxt.ParseResult.GetValueForArgument(amountArgument),
				Memo = ctxt.ParseResult.GetValueForOption(memoOption),
				NoSync = ctxt.ParseResult.GetValueForOption(NoSyncOption),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
				SpendingKeySeed = ctxt.ParseResult.GetValueForOption(SpendingKeySeedOption),
				SpendingKeyAccountIndex = ctxt.ParseResult.GetValueForOption(SpendingKeyAccountIndexOption),
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

		Transaction.LineItem item = new(this.Recipient, this.Amount, Zcash.Memo.FromMessage(this.Memo));
		TxId txid = await client.SendAsync(
			this.SelectedAccount!,
			new[] { item },
			new Progress<LightWalletClient.SendProgress>(p =>
			{
				if (p.Total > 0)
				{
					this.Console.WriteLine($"{100 * p.Progress / p.Total}%");
				}
			}),
			cancellationToken);

		this.Console.WriteLine($"Transmitted transaction ID: {txid}");

		return 0;
	}
}
