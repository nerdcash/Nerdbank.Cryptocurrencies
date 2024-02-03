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

		this.PrintAccountBalances(client.GetBalances());
		this.PrintUnshieldedBalances(client.GetUnshieldedBalances());

		return 0;
	}

	private void PrintAccountBalances(AccountBalances userBalances)
	{
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
			if (amount.Amount >= 0)
			{
				// Keep our non-negative value aligned considering a - character that might appear in other rows.
				this.Console.Write(" ");
			}

			this.Console.WriteLine(amount.ToString());
		}
	}

	private void PrintUnshieldedBalances(IReadOnlyList<(TransparentAddress Address, decimal Balance)> unshieldedFunds)
	{
		this.Console.WriteLine(string.Empty);

		if (unshieldedFunds.Count == 0)
		{
			this.Console.WriteLine(Strings.NoUnshieldedFunds);
			return;
		}

		this.Console.WriteLine("Unshielded balances:");
		foreach ((TransparentAddress address, decimal balance) in unshieldedFunds)
		{
			this.Console.WriteLine($"{Security.ZEC.Amount(balance)} {address}");
		}
	}
}
