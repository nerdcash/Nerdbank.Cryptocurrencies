// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash.Cli;

internal abstract class DeriveCommand
{
	protected static readonly Option<string> SeedPhraseOption = new("--seedPhrase")
	{
		Description = Strings.SeedPhraseOptionDescription,
		Required = true,
		Arity = ArgumentArity.ExactlyOne,
	};

	protected static readonly Option<string> PasswordOption = new("--password")
	{
		Description = Strings.PasswordOptionDescription,
		Arity = ArgumentArity.ZeroOrOne,
	};

	protected static readonly Option<uint> AccountIndexOption = new("--account")
	{
		Description = Strings.AccountIndexOptionDescription,
		Arity = ArgumentArity.ZeroOrOne,
	};

	protected static readonly Option<bool> TestNetOption = new("--testnet")
	{
		Description = Strings.TestNetOptionDescription,
	};

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
		command.Options.Add(SeedPhraseOption);
		command.Options.Add(PasswordOption);
		command.Options.Add(AccountIndexOption);
		command.Options.Add(TestNetOption);

		command.Validators.Add(cr =>
		{
			var seedPhraseResult = cr.Children.OfType<OptionResult>().FirstOrDefault(sr => sr.Option == SeedPhraseOption);
			if (seedPhraseResult?.Tokens is IReadOnlyList<Token> tokenList && tokenList.Count > 0)
			{
				StringBuilder sb = new();
				foreach (Token token in tokenList)
				{
					sb.Append(token.Value);
					sb.Append(' ');
				}

				if (!Bip39Mnemonic.TryParse(sb.ToString(), (string?)null, out _, out _, out string? errorMessage))
				{
					cr.AddError(errorMessage);
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

			command.SetAction(
				parseResult => new DeriveOrchardCommand
				{
					SeedPhrase = parseResult.GetValue(SeedPhraseOption)!,
					Password = parseResult.GetValue(PasswordOption),
					AccountIndex = parseResult.GetValue(AccountIndexOption),
					TestNet = parseResult.GetValue(TestNetOption),
				}.Execute(CancellationToken.None));

			return command;
		}

		protected override void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken)
		{
			Console.WriteLine(zip32.CreateOrchardAccount(this.AccountIndex).DefaultAddress);
		}
	}

	private class DeriveSaplingCommand : DeriveCommand
	{
		internal static new Command BuildCommand()
		{
			Command command = new("sapling", Strings.DeriveSaplingCommandDescription);
			AddCommonOptions(command);

			command.SetAction(
				parseResult => new DeriveSaplingCommand
				{
					SeedPhrase = parseResult.GetValue(SeedPhraseOption)!,
					Password = parseResult.GetValue(PasswordOption),
					AccountIndex = parseResult.GetValue(AccountIndexOption),
					TestNet = parseResult.GetValue(TestNetOption),
				}.Execute(CancellationToken.None));

			return command;
		}

		protected override void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken)
		{
			Console.WriteLine(zip32.CreateSaplingAccount(this.AccountIndex).DefaultAddress);
		}
	}

	private class DeriveTransparentCommand : DeriveCommand
	{
		private const uint DefaultAddressCount = 20;

		internal required uint AddressIndex { get; init; }

		internal uint AddressCount { get; init; } = DefaultAddressCount;

		internal static new Command BuildCommand()
		{
			Option<uint> addressIndexOption = new("--address")
			{
				Description = Strings.AddressIndexOptionDescription,
			};
			Option<uint> addressCountOption = new("--count")
			{
				Description = Strings.TransparentAddressCountOptionDescription,
			};

			Command command = new("transparent", Strings.DeriveTransparentCommandDescription);
			AddCommonOptions(command);
			command.Options.Add(addressIndexOption);
			command.Options.Add(addressCountOption);

			command.SetAction(
				parseResult => new DeriveTransparentCommand
				{
					SeedPhrase = parseResult.GetValue(SeedPhraseOption)!,
					Password = parseResult.GetValue(PasswordOption),
					AccountIndex = parseResult.GetValue(AccountIndexOption),
					AddressIndex = parseResult.GetValue(addressIndexOption),
					AddressCount = parseResult.GetValue(addressCountOption),
					TestNet = parseResult.GetValue(TestNetOption),
				}.Execute(CancellationToken.None));

			return command;
		}

		protected override void Execute(Zip32HDWallet zip32, CancellationToken cancellationToken)
		{
			Zip32HDWallet.Transparent.ExtendedViewingKey transparentKey = zip32.CreateTransparentAccount(this.AccountIndex).FullViewingKey;

			for (uint addressIndex = this.AddressIndex; addressIndex < this.AddressIndex + this.AddressCount; addressIndex++)
			{
				TransparentAddress transparent = transparentKey.GetReceivingKey(addressIndex).DefaultAddress;
				Console.WriteLine($"{addressIndex,5} {transparent}");
			}
		}
	}
}
