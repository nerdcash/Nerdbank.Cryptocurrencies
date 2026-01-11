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

		command.SetAction(async parseResult =>
		{
			return await new BalanceCommand
			{
				WalletPath = parseResult.GetValue(WalletPathArgument),
				TestNet = parseResult.GetValue(TestNetOption),
				LightWalletServerUrl = parseResult.GetValue(LightServerUriOption),
				NoSync = parseResult.GetValue(NoSyncOption),
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

		foreach (ZcashAccount account in client.GetAccounts())
		{
			Console.WriteLine($"Account default address: {account.DefaultAddress}");

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
			Console.Write(caption.PadRight(captionWidth));
			Console.Write(" ");

			// Align our negative indicator.
			Console.Write(amount.Amount >= 0 ? " " : "-");

			Console.WriteLine(amount.Absolute.ToString().PadLeft(amountWidth));
		}
	}

	private void PrintUnshieldedBalances(IReadOnlyList<(TransparentAddress Address, decimal Balance)> unshieldedFunds)
	{
		Console.WriteLine(string.Empty);

		if (unshieldedFunds.Count == 0)
		{
			Console.WriteLine(Strings.NoUnshieldedFunds);
			return;
		}

		Console.WriteLine("Unshielded balances:");
		foreach ((TransparentAddress address, decimal balance) in unshieldedFunds)
		{
			Console.WriteLine($"{Security.ZEC.Amount(balance)} {address}");
		}
	}
}
