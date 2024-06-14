// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash.Cli;

internal abstract class DeriveCommand
{
	protected static readonly Option<string> SeedPhraseOption = new("--seedPhrase", Strings.SeedPhraseOptionDescription) { IsRequired = true, Arity = ArgumentArity.ExactlyOne };

	protected static readonly Option<string> PasswordOption = new("--password", Strings.PasswordOptionDescription) { Arity = ArgumentArity.ZeroOrOne };

	protected static readonly Option<uint> AccountIndexOption = new("--account", () => 0, Strings.AccountIndexOptionDescription) { Arity = ArgumentArity.ZeroOrOne };

	protected static readonly Option<bool> TestNetOption = new("--testnet", Strings.TestNetOptionDescription);

	internal required IConsole Console { get; set; }

	internal required string SeedPhrase { get; init; }

	internal required string? Password { get; init; }

	internal required uint AccountIndex { get; init; }

	internal bool TestNet { get; set; }

	internal static Command BuildCommand()
	{
		Command command = new("derive", Strings.DeriveCommandDescription)
		{
			DeriveOrchardCommand.BuildCommand(),
			DeriveSaplingCommand.BuildCommand(),
			DeriveTransparentCommand.BuildCommand(),
		};

		return command;
	}

	internal int Execute(CancellationToken cancellationToken)
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Parse(this.SeedPhrase, this.Password);
		Zip32HDWallet zip32 = new(mnemonic, this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
		this.Execute(zip32, cancellationToken);
		return 0;
	}

	protected static void AddCommonOptions(Command command)
	{
		command.AddOption(SeedPhraseOption);
		command.AddOption(PasswordOption);
		command.AddOption(AccountIndexOption);
		command.AddOption(TestNetOption);

		command.AddValidator(cr =>
		{
			if (cr.FindResultFor(SeedPhraseOption)?.Tokens is IReadOnlyList<Token> tokenList)
			{
				StringBuilder sb = new();
				foreach (Token token in tokenList)
				{
					sb.Append(token.Value);
					sb.Append(' ');
				}

				if (!Bip39Mnemonic.TryParse(sb.ToString(), (string?)null, out _, out _, out string? errorMessage))
				{
					cr.ErrorMessage = errorMessage;
				}
			}
		});
	}

	protected abstract void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken);

	private class DeriveOrchardCommand : DeriveCommand
	{
		internal static new Command BuildCommand()
		{
			Command command = new("orchard", Strings.DeriveOrchardCommandDescription);
			AddCommonOptions(command);

			command.SetHandler(
				ctxt => new DeriveOrchardCommand
				{
					Console = ctxt.Console,
					SeedPhrase = ctxt.ParseResult.GetValueForOption(SeedPhraseOption)!,
					Password = ctxt.ParseResult.GetValueForOption(PasswordOption),
					AccountIndex = ctxt.ParseResult.GetValueForOption(AccountIndexOption),
					TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				}.Execute(ctxt.GetCancellationToken()));

			return command;
		}

		protected override void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken)
		{
			this.Console.WriteLine(zip32.CreateOrchardAccount(this.AccountIndex).DefaultAddress);
		}
	}

	private class DeriveSaplingCommand : DeriveCommand
	{
		internal static new Command BuildCommand()
		{
			Command command = new("sapling", Strings.DeriveSaplingCommandDescription);
			AddCommonOptions(command);

			command.SetHandler(
				ctxt => new DeriveSaplingCommand
				{
					Console = ctxt.Console,
					SeedPhrase = ctxt.ParseResult.GetValueForOption(SeedPhraseOption)!,
					Password = ctxt.ParseResult.GetValueForOption(PasswordOption),
					AccountIndex = ctxt.ParseResult.GetValueForOption(AccountIndexOption),
					TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				}.Execute(ctxt.GetCancellationToken()));

			return command;
		}

		protected override void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken)
		{
			this.Console.WriteLine(zip32.CreateSaplingAccount(this.AccountIndex).DefaultAddress);
		}
	}

	private class DeriveTransparentCommand : DeriveCommand
	{
		private const uint DefaultAddressCount = 20;

		internal required uint AddressIndex { get; init; }

		internal uint AddressCount { get; init; } = DefaultAddressCount;

		internal static new Command BuildCommand()
		{
			Option<uint> addressIndexOption = new("--address", () => 0, Strings.AddressIndexOptionDescription);
			Option<uint> addressCountOption = new("--count", () => DefaultAddressCount, Strings.TransparentAddressCountOptionDescription);

			Command command = new("transparent", Strings.DeriveTransparentCommandDescription);
			AddCommonOptions(command);
			command.AddOption(addressIndexOption);
			command.AddOption(addressCountOption);

			command.SetHandler(
				ctxt => new DeriveTransparentCommand
				{
					Console = ctxt.Console,
					SeedPhrase = ctxt.ParseResult.GetValueForOption(SeedPhraseOption)!,
					Password = ctxt.ParseResult.GetValueForOption(PasswordOption),
					AccountIndex = ctxt.ParseResult.GetValueForOption(AccountIndexOption),
					AddressIndex = ctxt.ParseResult.GetValueForOption(addressIndexOption),
					AddressCount = ctxt.ParseResult.GetValueForOption(addressCountOption),
					TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				}.Execute(ctxt.GetCancellationToken()));

			return command;
		}

		protected override void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken)
		{
			Zip32HDWallet.Transparent.ExtendedViewingKey transparentKey = zip32.CreateTransparentAccount(this.AccountIndex).FullViewingKey;

			for (uint addressIndex = this.AddressIndex; addressIndex < this.AddressIndex + this.AddressCount; addressIndex++)
			{
				TransparentAddress transparent = transparentKey.GetReceivingKey(addressIndex).DefaultAddress;
				this.Console.WriteLine($"{addressIndex,5} {transparent}");
			}
		}
	}
}
