// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class AccountsCommand : WalletUserCommandBase
{
	private AccountsCommand()
	{
	}

	internal static Command BuildCommand()
	{
		Command command = new("accounts", Strings.AccountsCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
		};

		command.SetHandler(async parseResult =>
		{
			return await new AccountsCommand()
			{
				WalletPath = parseResult.GetValue(WalletPathArgument),
				TestNet = parseResult.GetValue(TestNetOption),
			}.ExecuteAsync(CancellationToken.None);
		});

		return command;
	}

	internal override Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		foreach (ZcashAccount account in client.GetAccounts())
		{
			Console.WriteLine($"{account.DefaultAddress}");
		}

		return Task.FromResult(0);
	}
}
