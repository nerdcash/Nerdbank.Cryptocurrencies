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

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new AccountsCommand()
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal override Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		foreach (ZcashAccount account in client.GetAccounts())
		{
			this.Console.WriteLine($"{account.DefaultAddress}");
		}

		return Task.FromResult(0);
	}
}
