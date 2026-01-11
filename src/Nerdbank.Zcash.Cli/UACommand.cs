// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class UACommand
{
	internal static Command BuildCommand()
	{
		Command command = new("ua", Strings.UACommandDescription)
		{
			ParseCommand.BuildCommand(),
			ConstructCommand.BuildCommand(),
		};

		return command;
	}

	private class ParseCommand
	{
		internal required string UnifiedAddress { get; init; }

		internal static Command BuildCommand()
		{
			Argument<string> uaArgument = new("unified address")
			{
				Description = Strings.UAParseArgumentDescription,
			};

			Command parseCommand = new("parse", Strings.UAParseCommandDescription)
		{
			uaArgument,
		};
			parseCommand.SetAction(parseResult =>
			{
				return new ParseCommand
				{
					UnifiedAddress = parseResult.GetValue(uaArgument),
				}.Execute(CancellationToken.None);
			});

			return parseCommand;
		}

		internal int Execute(CancellationToken cancellationToken)
		{
			if (!ZcashAddress.TryDecode(this.UnifiedAddress, out _, out string? errorMessage, out ZcashAddress? parsed))
			{
				Console.Error.WriteLine(errorMessage);
				return 1;
			}

			if (parsed is not UnifiedAddress ua)
			{
				Console.Error.WriteLine(Strings.NotAUnifiedAddress);
				return 2;
			}

			Console.WriteLine($"Network: {ua.Network}");

			foreach (ZcashAddress receiver in ua.Receivers)
			{
				Console.WriteLine($"{GetPoolReceiver(receiver),-12}: {receiver}");
			}

			return 0;
		}

		private static string GetPoolReceiver(ZcashAddress address) => address switch
		{
			TransparentAddress => "Transparent",
			SproutAddress => "Sprout",
			SaplingAddress => "Sapling",
			OrchardAddress => "Orchard",
			_ => "Unknown",
		};
	}

	private class ConstructCommand
	{
		internal required string[] Receivers { get; init; }

		internal static Command BuildCommand()
		{
			Argument<string[]> receiversArgument = new("receivers")
			{
				Description = Strings.UAConstructReceiversArgumentDescription,
				Arity = ArgumentArity.OneOrMore,
			};

			Command command = new("construct", Strings.UAConstructCommandDescription)
			{
				receiversArgument,
			};

			command.SetAction(parseResult =>
			{
				return new ConstructCommand
				{
					Receivers = parseResult.GetValue(receiversArgument),
				}.Execute(CancellationToken.None);
			});

			return command;
		}

		internal int Execute(CancellationToken cancellationToken)
		{
			ZcashAddress[] receiverAddresses = new ZcashAddress[this.Receivers.Length];
			for (int i = 0; i < this.Receivers.Length; i++)
			{
				if (!ZcashAddress.TryDecode(this.Receivers[i], out _, out string? errorMessage, out ZcashAddress? addr))
				{
					Console.Error.WriteLine(Strings.FormatInvalidAddress(this.Receivers[i], errorMessage));
					return 1;
				}

				receiverAddresses[i] = addr;
			}

			try
			{
				UnifiedAddress ua = UnifiedAddress.Create(receiverAddresses);
				Console.WriteLine(ua);
			}
			catch (ArgumentException ex)
			{
				Console.Error.WriteLine(ex.Message);
				return 2;
			}

			return 0;
		}
	}
}
