﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class BalancesCommand : WalletUserCommandBase
{
	internal static Command BuildCommand()
	{
		Command command = new("balances", Strings.BalancesCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new BalancesCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal override Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		LightWalletClient.PoolBalances poolBalances = client.GetPoolBalances();

		if (poolBalances.TransparentBalance.HasValue)
		{
			PrintLine("Transparent", poolBalances.TransparentBalance.Value);
		}

		if (poolBalances.Sapling is not null)
		{
			PrintShieldedPoolBalances("Sapling", poolBalances.Sapling);
		}

		if (poolBalances.Orchard is not null)
		{
			PrintShieldedPoolBalances("Orchard", poolBalances.Orchard);
		}

		void PrintShieldedPoolBalances(string poolName, LightWalletClient.ShieldedPoolBalance pool)
		{
			PrintLine($"{poolName}", pool.Balance);
			PrintLine($"{poolName} verified", pool.VerifiedBalance);
			PrintLine($"{poolName} unverified", pool.UnverifiedBalance);
			PrintLine($"{poolName} spendable", pool.SpendableBalance);
		}

		void PrintLine(string caption, decimal amount)
		{
			this.Console.WriteLine($"{caption,-20} {amount,14:F8}");
		}

		return Task.FromResult(0);
	}
}