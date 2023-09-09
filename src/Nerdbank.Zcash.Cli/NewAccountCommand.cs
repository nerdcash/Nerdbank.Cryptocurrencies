﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.Cli;

internal class NewAccountCommand
{
	private const int SeedPhraseWordLengthDefault = 24;

	private NewAccountCommand()
	{
	}

	internal required IConsole Console { get; set; }

	internal bool PromptForSeedPhrase { get; set; }

	internal string? SeedPhrase { get; set; }

	internal int SeedPhraseWordLength { get; set; } = SeedPhraseWordLengthDefault;

	internal string? Password { get; set; }

	internal bool TestNet { get; set; }

	internal uint AccountIndex { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	internal bool OfflineMode { get; set; }

	private Uri? DefaultLightWalletUrl => this.TestNet ? null : new Uri("https://zcash.mysideoftheweb.com:9067/");

	internal static Command BuildCommand()
	{
		Option<int> seedPhraseWordLengthOption = new("--seedPhraseLength", () => SeedPhraseWordLengthDefault, Strings.SeedPhraseLengthOptionDescription);
		seedPhraseWordLengthOption.AddValidator(v =>
		{
			int value = v.GetValueForOption(seedPhraseWordLengthOption);
			if (value % 3 != 0 || value <= 0)
			{
				v.ErrorMessage = Strings.BadSeedPhraseLength;
			}
		});

		Option<string> seedPhraseOption = new("--seedPhrase", Strings.SeedPhraseOptionDescription) { Arity = ArgumentArity.ZeroOrOne };

		Option<string> seedPhrasePasswordOption = new("--password", Strings.PasswordOptionDescription);

		Option<bool> testNetOption = new("--testnet", Strings.TestNetOptionDescription);

		Option<uint> accountIndexOption = new("--index", () => 0, Strings.AccountIndexOptionDescription);

		Option<Uri> lightServerUriOption = new("--lightserverUrl", Strings.LightServerUrlOptionDescription);

		Option<bool> offlineModeOption = new("--offline", Strings.OfflineOptionDescription);

		Command command = new("NewAccount", Strings.NewAccountCommandDescription)
		{
			seedPhraseWordLengthOption,
			seedPhraseOption,
			seedPhrasePasswordOption,
			testNetOption,
			accountIndexOption,
			lightServerUriOption,
			offlineModeOption,
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
				TestNet = ctxt.ParseResult.GetValueForOption(testNetOption),
				AccountIndex = ctxt.ParseResult.GetValueForOption(accountIndexOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(lightServerUriOption),
				OfflineMode = ctxt.ParseResult.GetValueForOption(offlineModeOption),
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

		ZcashAccount account = new(zip32, this.AccountIndex);
		await this.PrintBirthdayHeightAsync(network, cancellationToken);
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

	private async Task PrintBirthdayHeightAsync(ZcashNetwork network, CancellationToken cancellationToken)
	{
		if (!this.OfflineMode)
		{
			Uri? serverUrl = this.LightWalletServerUrl ?? this.DefaultLightWalletUrl;
			if (serverUrl is null)
			{
				this.Console.Error.WriteLine(Strings.FormatNoLightWalletServerKnown(network));
			}
			else
			{
				ulong height = await LightWalletClient.GetLatestBlockHeightAsync(serverUrl, cancellationToken);
				this.Console.WriteLine($"Birthday height: {height}");
			}
		}
	}

	private void PrintAccountInfo(ZcashAccount account)
	{
		this.Console.WriteLine($"Network:         {account.Network}");
		this.Console.WriteLine($"Index:           {this.AccountIndex}");
		this.Console.WriteLine(string.Empty);
		this.Console.WriteLine($"Unified address:      {account.DefaultAddress}");
		this.Console.WriteLine($"Orchard receiver:     {account.IncomingViewing.Orchard!.DefaultAddress}");
		this.Console.WriteLine($"Sapling receiver:     {account.IncomingViewing.Sapling!.DefaultAddress}");
		this.Console.WriteLine($"Transparent receiver: {account.IncomingViewing.Transparent!.DefaultAddress}");

		this.Console.WriteLine(string.Empty);
		this.Console.WriteLine($"Unified full viewing key:     {account.FullViewing!.UnifiedKey}");
		this.Console.WriteLine($"Unified incoming viewing key: {account.IncomingViewing!.UnifiedKey}");
	}
}