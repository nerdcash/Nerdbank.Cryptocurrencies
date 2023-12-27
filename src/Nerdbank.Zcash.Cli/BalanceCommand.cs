// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.Cli;

internal class BalanceCommand : SyncFirstCommandBase
{
	internal static Command BuildCommand()
	{
		Command command = new("balance", Strings.BalanceCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new BalanceCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
				NoSync = ctxt.ParseResult.GetValueForOption(NoSyncOption),
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

		AccountBalances userBalances = client.GetBalances();

		(string, SecurityAmount)[] lines = [
			("Balance", userBalances.MainBalance),
			("Spendable", userBalances.Spendable),
			("Immature change", userBalances.ImmatureChange),
			("Minimum fees", userBalances.MinimumFees),
			("Immature income", userBalances.ImmatureIncome),
			("Dust", userBalances.Dust),
			("Incoming", userBalances.Incoming),
			("Incoming dust", userBalances.IncomingDust),
		];

		int captionWidth = lines.Max(l => l.Item1.Length);
		foreach ((string caption, SecurityAmount amount) in lines)
		{
			PrintLine(caption, amount);
		}

		void PrintLine(string caption, SecurityAmount amount)
		{
			this.Console.Write(caption.PadRight(captionWidth));
			this.Console.Write(" ");
			this.Console.WriteLine(amount.ToString());
		}

		return 0;
	}
}
