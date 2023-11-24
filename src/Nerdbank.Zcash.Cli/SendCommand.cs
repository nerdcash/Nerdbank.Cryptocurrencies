// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class SendCommand : SyncFirstCommandBase
{
	internal required ZcashAddress Recipient { get; init; }

	internal required decimal Amount { get; init; }

	internal string? Memo { get; init; }

	internal decimal? Fee { get; init; }

	internal static Command BuildCommand()
	{
		Argument<ZcashAddress> recipientArgument = new("recipient", Utilities.AddressParser, description: Strings.SendRecipientArgumentDescription);
		Argument<decimal> amountArgument = new("amount", Strings.SendAmountArgumentDescription);
		Option<string> memoOption = new("--memo", Strings.SendMemoOptionDescription);
		Option<decimal?> feeOption = new("--fee", Strings.SendFeeOptionDescription);
		feeOption.AddValidator(v =>
		{
			if (v.GetValueForOption(feeOption) < 0)
			{
				v.ErrorMessage = "The fee must be a non-negative number.";
			}
		});

		Command command = new("send", Strings.SendCommandDescription)
		{
			WalletPathArgument,
			recipientArgument,
			amountArgument,
			memoOption,
			////feeOption, // zingolib does not yet expose a way to do this.
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
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
				Fee = ctxt.ParseResult.GetValueForOption(feeOption),
				NoSync = ctxt.ParseResult.GetValueForOption(NoSyncOption),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
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

		Transaction.SendItem item = new(this.Recipient, this.Amount, Zcash.Memo.FromMessage(this.Memo));
		string txid = await client.SendAsync(
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
