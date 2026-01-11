// Copyright (c) IronPigeon, LLC. All rights reserved.
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

		command.SetHandler(async parseResult =>
		{
			return await new SendCommand
			{
				WalletPath = parseResult.GetValue(WalletPathArgument),
				Recipient = parseResult.GetValue(recipientArgument),
				Amount = parseResult.GetValue(amountArgument),
				Memo = parseResult.GetValue(memoOption),
				NoSync = parseResult.GetValue(NoSyncOption),
				TestNet = parseResult.GetValue(TestNetOption),
				LightWalletServerUrl = parseResult.GetValue(LightServerUriOption),
				SpendingKeySeed = parseResult.GetValue(SpendingKeySeedOption),
				SpendingKeyAccountIndex = parseResult.GetValue(SpendingKeyAccountIndexOption),
			}.ExecuteAsync(CancellationToken.None);
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
		ReadOnlyMemory<TxId> txids = await client.SendAsync(
			this.SelectedAccount!,
			[item],
			new Progress<LightWalletClient.SendProgress>(p =>
			{
				if (p.Total > 0)
				{
					Console.WriteLine($"{100 * p.Progress / p.Total}%");
				}
			}),
			cancellationToken);

		for (int i = 0; i < txids.Length; i++)
		{
			Console.WriteLine($"Transmitted transaction ID: {txids.Span[i]}");
		}

		return 0;
	}
}
