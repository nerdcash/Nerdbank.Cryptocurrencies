// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class PaymentsCommand : SyncFirstCommandBase
{
	internal uint StartingBlock { get; init; }

	internal required ZcashAddress Address { get; init; }

	internal static Command BuildCommand()
	{
		Option<uint> startingBlockOption = new("--from")
		{
			Description = Strings.PaymentsStartingBlockOptionDescription,
		};

		Argument<ZcashAddress> addressArgument = new("address")
		{
			Description = Strings.PaymentsAddressArgumentDescription,
			Arity = ArgumentArity.ExactlyOne,
			CustomParser = Utilities.AddressParser,
		};

		Command command = new("payments", Strings.PaymentsCommandDescription)
		{
			WalletPathArgument,
			addressArgument,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
			startingBlockOption,
		};

		command.SetAction(async (parseResult, cancellationToken) =>
		{
			return await new PaymentsCommand
			{
				WalletPath = parseResult.GetValue(WalletPathArgument)!,
				Address = parseResult.GetValue(addressArgument)!,
				TestNet = parseResult.GetValue(TestNetOption),
				LightWalletServerUrl = parseResult.GetValue(LightServerUriOption),
				NoSync = parseResult.GetValue(NoSyncOption),
				StartingBlock = parseResult.GetValue(startingBlockOption),
			}.ExecuteAsync(cancellationToken);
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

		List<Transaction> txs = client.GetIncomingPayments(this.Address, this.StartingBlock);

		if (txs.Count == 0)
		{
			Console.WriteLine(Strings.PaymentsNoneFound);
		}
		else
		{
			Console.WriteLine(string.Format(Strings.PaymentsFoundCount, txs.Count));
			foreach (Transaction tx in txs)
			{
				HistoryCommand.PrintTransaction(tx);
			}
		}

		return 0;
	}
}
