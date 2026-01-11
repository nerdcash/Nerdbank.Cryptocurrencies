// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash.Cli;

internal class NewAccountCommand
{
	private static readonly int SeedPhraseWordLengthDefault = Bip39Mnemonic.WordsRequiredForEntropyLength(Zip32HDWallet.MinimumEntropyLengthInBits);

	private NewAccountCommand()
	{
	}

	internal required IConsole Console { get; set; }

	internal required string Name { get; set; }

	internal bool PromptForSeedPhrase { get; set; }

	internal string? SeedPhrase { get; set; }

	internal int SeedPhraseWordLength { get; set; } = SeedPhraseWordLengthDefault;

	internal string? Password { get; set; }

	internal bool TestNet { get; set; }

	internal uint AccountIndex { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	internal uint? BirthdayHeight { get; set; }

	internal bool OfflineMode { get; set; }

	internal string? WalletPath { get; set; }

	internal static Command BuildCommand()
	{
		Option<int> seedPhraseWordLengthOption = new("--seedPhraseLength", () => SeedPhraseWordLengthDefault, Strings.SeedPhraseLengthOptionDescription);
		seedPhraseWordLengthOption.AddValidator(v =>
		{
			int value = v.GetValueForOption(seedPhraseWordLengthOption);
			if (value % 3 != 0 || value < Bip39Mnemonic.WordsRequiredForEntropyLength(Zip32HDWallet.MinimumEntropyLengthInBits))
			{
				v.ErrorMessage = Strings.BadSeedPhraseLength;
			}
		});

		Option<string> seedPhraseOption = new("--seedPhrase", Strings.SeedPhraseOptionDescription) { Arity = ArgumentArity.ZeroOrOne };

		Option<string> seedPhrasePasswordOption = new("--password", Strings.PasswordOptionDescription);

		Option<uint> accountIndexOption = new("--index", () => 0, Strings.AccountIndexOptionDescription);

		Option<bool> offlineModeOption = new("--offline", Strings.OfflineOptionDescription);

		Option<string> walletPathOption = new Option<string>("--wallet", Strings.NewAccountWalletPathOptionDescription)
			.LegalFilePathsOnly();

		Option<string> nameOption = new Option<string>("--name", () => "(default)", Strings.AccountNameOptionDescription);

		Option<uint?> birthdayHeightOption = new("--birthday-height", Strings.BirthdayHeightOptionDescription);

		Command command = new("new", Strings.NewAccountCommandDescription)
		{
			seedPhraseWordLengthOption,
			seedPhraseOption,
			seedPhrasePasswordOption,
			WalletUserCommandBase.TestNetOption,
			accountIndexOption,
			WalletUserCommandBase.LightServerUriOption,
			offlineModeOption,
			walletPathOption,
			nameOption,
			birthdayHeightOption,
		};

		command.AddValidator(v =>
		{
			if (v.FindResultFor(seedPhraseOption)?.Token is not null && v.FindResultFor(seedPhraseWordLengthOption)?.Token is not null)
			{
				v.ErrorMessage = Strings.SeedLengthAndSeedPhraseNotAllowed;
			}
		});

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new NewAccountCommand()
			{
				Console = ctxt.Console,
				SeedPhraseWordLength = ctxt.ParseResult.GetValueForOption(seedPhraseWordLengthOption),
				SeedPhrase = ctxt.ParseResult.GetValueForOption(seedPhraseOption),
				PromptForSeedPhrase = ctxt.ParseResult.FindResultFor(seedPhraseOption) is { Token: not null, Tokens: { Count: 0 } },
				Password = ctxt.ParseResult.GetValueForOption(seedPhrasePasswordOption),
				TestNet = ctxt.ParseResult.GetValueForOption(WalletUserCommandBase.TestNetOption),
				AccountIndex = ctxt.ParseResult.GetValueForOption(accountIndexOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(WalletUserCommandBase.LightServerUriOption),
				OfflineMode = ctxt.ParseResult.GetValueForOption(offlineModeOption),
				WalletPath = ctxt.ParseResult.GetValueForOption(walletPathOption),
				Name = ctxt.ParseResult.GetValueForOption(nameOption)!,
				BirthdayHeight = ctxt.ParseResult.GetValueForOption(birthdayHeightOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		if (this.PromptForSeedPhrase)
		{
			if (!this.TryCollectSeedPhrase())
			{
				return 2;
			}
		}

		Bip39Mnemonic? mnemonic;
		if (this.SeedPhrase is null)
		{
			mnemonic = Bip39Mnemonic.Create(this.SeedPhraseWordLength / 3 * 32, this.Password);
		}
		else
		{
			if (!Bip39Mnemonic.TryParse(this.SeedPhrase, this.Password, out mnemonic, out _, out string? errorMessage))
			{
				this.Console.Error.WriteLine(errorMessage);
				return 1;
			}
		}

		ZcashNetwork network = this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		Zip32HDWallet zip32 = new(mnemonic, network);

		this.Console.WriteLine($"Seed phrase:     {mnemonic}");
		this.Console.WriteLine($"Password:        {this.Password}");

		this.BirthdayHeight ??= await this.ComputeBirthdayHeightAsync(network, cancellationToken);
		if (this.BirthdayHeight is not null)
		{
			this.Console.WriteLine($"Birthday height: {this.BirthdayHeight}");
		}

		ZcashAccount account = new(zip32, this.AccountIndex)
		{
			BirthdayHeight = this.BirthdayHeight,
		};

		if (this.WalletPath is not null)
		{
			using LightWalletClient client = Utilities.ConstructLightClient(
				this.LightWalletServerUrl,
				account.Network,
				this.WalletPath);
			await client.AddAccountAsync(account, cancellationToken);
			this.Console.WriteLine($"Wallet file saved to \"{this.WalletPath}\".");
		}

		this.PrintAccountInfo(account);

		this.Console.WriteLine(string.Empty);

		return 0;
	}

	private bool TryCollectSeedPhrase()
	{
		while (this.SeedPhrase is null)
		{
			System.Console.Write(Strings.SeedPhrasePrompt);
			System.Console.Write(' ');
			string? seedPhrase = System.Console.ReadLine();
			if (seedPhrase is null)
			{
				return false;
			}

			if (Bip39Mnemonic.TryParse(seedPhrase, out _, out _, out _))
			{
				this.SeedPhrase = seedPhrase;
				break;
			}
			else
			{
				System.Console.WriteLine(Strings.InvalidSeedPhrase);
			}
		}

		return true;
	}

	private async Task<uint?> ComputeBirthdayHeightAsync(ZcashNetwork network, CancellationToken cancellationToken)
	{
		if (!this.OfflineMode)
		{
			Uri serverUrl = this.LightWalletServerUrl ?? Utilities.GetDefaultLightWalletUrl(this.TestNet);
			uint height = await LightWalletClient.GetLatestBlockHeightAsync(serverUrl, cancellationToken);
			return height;
		}

		return null;
	}

	private void PrintAccountInfo(ZcashAccount account)
	{
		this.Console.WriteLine($"Index:           {this.AccountIndex}");
		Utilities.PrintAccountInfo(this.Console, account);
	}
}
