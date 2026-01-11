// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using static System.CommandLine.ArgumentValidation;

namespace Nerdbank.Zcash.Cli;

internal class ImportAccountCommand
{
	private ImportAccountCommand()
	{
	}



	internal required string Key { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	internal uint? BirthdayHeight { get; set; }

	internal string? WalletPath { get; set; }

	internal static Command BuildCommand()
	{
		Argument<string> keyArgument = new("key")
		{
			Description = Strings.KeyArgumentDescription,
			Arity = ArgumentArity.ExactlyOne,
		};
		Option<string> walletPathOption = new Option<string>("--wallet")
		{
			Description = Strings.NewAccountWalletPathOptionDescription,
		}.AcceptLegalFilePathsOnly();
		Option<uint> birthdayHeightOption = new("--birthday-height")
		{
			Description = Strings.BirthdayHeightOptionDescription,
		};
		Option<Uri> lightServerUriOption = new("--lightserverUrl")
		{
			Description = Strings.LightServerUrlOptionDescription,
		};

		Command command = new("import", Strings.ImportAccountCommandDescription)
		{
			keyArgument,
			walletPathOption,
			birthdayHeightOption,
			lightServerUriOption,
		};

		command.SetAction((parseResult, cancellationToken) =>
		{
			return new ImportAccountCommand()
			{
				Key = parseResult.GetValue(keyArgument)!,
				LightWalletServerUrl = parseResult.GetValue(lightServerUriOption),
				WalletPath = parseResult.GetValue(walletPathOption),
				BirthdayHeight = parseResult.GetValue(birthdayHeightOption),
			}.ExecuteAsync(cancellationToken);
		});

		return command;
	}

	private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		if (!ZcashAccount.TryImportAccount(this.Key, out ZcashAccount? account))
		{
			Console.Error.WriteLine(Strings.UnrecognizedKeyFormat);
			return 1;
		}

		account.BirthdayHeight = this.BirthdayHeight;

		Console.WriteLine($"Network: {account.Network}");

		Uri serverUrl = this.LightWalletServerUrl ?? Utilities.GetDefaultLightWalletUrl(account.Network == ZcashNetwork.TestNet);
		if (this.WalletPath is not null)
		{
			using LightWalletClient client = Utilities.ConstructLightClient(
				this.LightWalletServerUrl,
				account.Network,
				this.WalletPath);
			await client.AddAccountAsync(account, cancellationToken);
			Console.WriteLine($"Wallet file saved to \"{this.WalletPath}\".");
		}

		Utilities.PrintAccountInfo(account);

		Console.WriteLine(string.Empty);

		return 0;
	}
}
