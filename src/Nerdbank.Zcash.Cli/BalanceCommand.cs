// Copyright (c) IronPigeon, LLC. All rights reserved.
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

		foreach (ZcashAccount account in client.GetAccounts())
		{
			this.Console.WriteLine($"Account default address: {account.DefaultAddress}");

			this.PrintAccountBalances(client.GetBalances(account));
			this.PrintUnshieldedBalances(client.GetUnshieldedBalances(account));
		}

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
		int amountWidth = lines.Max(l => l.Item2.Absolute.ToString().Length);
		foreach ((string caption, SecurityAmount amount) in lines)
		{
			PrintLine(caption, amount);
		}

		void PrintLine(string caption, SecurityAmount amount)
		{
			this.Console.Write(caption.PadRight(captionWidth));
			this.Console.Write(" ");

			// Align our negative indicator.
			this.Console.Write(amount.Amount >= 0 ? " " : "-");

			this.Console.WriteLine(amount.Absolute.ToString().PadLeft(amountWidth));
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
