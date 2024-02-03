// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class ShieldCommand : SyncFirstCommandBase
{
	internal bool ShieldAllAddresses { get; init; }

	internal static Command BuildCommand()
	{
		Option<bool> shieldAllOption = new("--all", Strings.ShieldAllOptionDescription);

		Command command = new("shield", Strings.ShieldCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
			shieldAllOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new ShieldCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				NoSync = ctxt.ParseResult.GetValueForOption(NoSyncOption),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
				ShieldAllAddresses = ctxt.ParseResult.GetValueForOption(shieldAllOption),
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

		IReadOnlyList<(TransparentAddress Address, decimal Balance)> unshieldedBalances = client.GetUnshieldedBalances();
		if (unshieldedBalances.Count == 0)
		{
			this.Console.WriteLine(Strings.NoUnshieldedFunds);
			return 0;
		}

		foreach ((TransparentAddress address, decimal balance) in unshieldedBalances)
		{
			this.Console.WriteLine($"Shielding {balance} ZEC from {address}...");
			await client.ShieldAsync(address, cancellationToken);

			if (!this.ShieldAllAddresses)
			{
				// Indicate by exit code if additional funds remain unshielded.
				return unshieldedBalances.Count > 1 ? 1 : 0;
			}
		}

		return 0;
	}
}
