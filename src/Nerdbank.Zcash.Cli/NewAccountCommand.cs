// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using static System.CommandLine.ArgumentValidation;
using System.CommandLine.Parsing;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash.Cli;

internal class NewAccountCommand
{
	private static readonly int SeedPhraseWordLengthDefault = Bip39Mnemonic.WordsRequiredForEntropyLength(Zip32HDWallet.MinimumEntropyLengthInBits);

	private NewAccountCommand()
	{
	}



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
		Option<int> seedPhraseWordLengthOption = new("--seedPhraseLength")
		{
			Description = Strings.SeedPhraseLengthOptionDescription,
		};
		seedPhraseWordLengthOption.Validators.Add(v =>
		{
			int value = v.GetValueOrDefault<int>();
			if (value % 3 != 0 || value < Bip39Mnemonic.WordsRequiredForEntropyLength(Zip32HDWallet.MinimumEntropyLengthInBits))
			{
				v.AddError(Strings.BadSeedPhraseLength);
			}
		});

		Option<string> seedPhraseOption = new("--seedPhrase")
		{
			Description = Strings.SeedPhraseOptionDescription,
			Arity = ArgumentArity.ZeroOrOne,
		};

		Option<string> seedPhrasePasswordOption = new("--password")
		{
			Description = Strings.PasswordOptionDescription,
		};

		Option<uint> accountIndexOption = new("--index")
		{
			Description = Strings.AccountIndexOptionDescription,
		};

		Option<bool> offlineModeOption = new("--offline")
		{
			Description = Strings.OfflineOptionDescription,
		};

		Option<string> walletPathOption = new Option<string>("--wallet")
		{
			Description = Strings.NewAccountWalletPathOptionDescription,
		}.AcceptLegalFilePathsOnly();

		Option<string> nameOption = new Option<string>("--name")
		{
			Description = Strings.AccountNameOptionDescription,
		};

		Option<uint?> birthdayHeightOption = new("--birthday-height")
		{
			Description = Strings.BirthdayHeightOptionDescription,
		};

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

		command.Validators.Add(v =>
		{
			var seedPhraseRes = v.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == seedPhraseOption);
			var lengthRes = v.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == seedPhraseWordLengthOption);
			if (seedPhraseRes?.Tokens.Count > 0 && lengthRes?.Tokens.Count > 0)
			{
				v.AddError(Strings.SeedLengthAndSeedPhraseNotAllowed);
			}
		});

		command.SetAction(parseResult =>
		{
			var rootCommandResult = parseResult.RootCommandResult;
			var seedPhraseRes = rootCommandResult.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == seedPhraseOption);
			return new NewAccountCommand()
			{
				SeedPhraseWordLength = parseResult.GetValue(seedPhraseWordLengthOption),
				SeedPhrase = parseResult.GetValue(seedPhraseOption),
				PromptForSeedPhrase = seedPhraseRes is { Tokens.Count: 0 },
				Password = parseResult.GetValue(seedPhrasePasswordOption),
				TestNet = parseResult.GetValue(WalletUserCommandBase.TestNetOption),
				AccountIndex = parseResult.GetValue(accountIndexOption),
				LightWalletServerUrl = parseResult.GetValue(WalletUserCommandBase.LightServerUriOption),
				OfflineMode = parseResult.GetValue(offlineModeOption),
				WalletPath = parseResult.GetValue(walletPathOption),
				Name = parseResult.GetValue(nameOption)!,
				BirthdayHeight = parseResult.GetValue(birthdayHeightOption),
			}.ExecuteAsync(CancellationToken.None);
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
				Console.Error.WriteLine(errorMessage);
				return 1;
			}
		}

		ZcashNetwork network = this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		Zip32HDWallet zip32 = new(mnemonic, network);

		Console.WriteLine($"Seed phrase:     {mnemonic}");
		Console.WriteLine($"Password:        {this.Password}");

		this.BirthdayHeight ??= await this.ComputeBirthdayHeightAsync(network, cancellationToken);
		if (this.BirthdayHeight is not null)
		{
			Console.WriteLine($"Birthday height: {this.BirthdayHeight}");
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
			Console.WriteLine($"Wallet file saved to \"{this.WalletPath}\".");
		}

		this.PrintAccountInfo(account);

		Console.WriteLine(string.Empty);

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
		Console.WriteLine($"Index:           {this.AccountIndex}");
		Utilities.PrintAccountInfo(account);
	}
}
