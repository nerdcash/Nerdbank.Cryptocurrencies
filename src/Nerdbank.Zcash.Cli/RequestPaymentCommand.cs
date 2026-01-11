// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;
using Nerdbank.QRCodes;
using QRCoder;

namespace Nerdbank.Zcash.Cli;

internal class RequestPaymentCommand
{
	internal required ZcashAddress[] Payees { get; init; }

	internal required decimal[]? Amounts { get; init; }

	internal required Memo[]? Memos { get; init; }

	internal required string[]? Labels { get; init; }

	internal required string[]? Messages { get; init; }

	internal required string? SaveQRCodePath { get; init; }

	internal static Command BuildCommand()
	{
		Argument<ZcashAddress[]> payeesArgument = new("payee")
		{
			Description = Strings.RequestPaymentPayeeArgumentDescription,
			CustomParser = Utilities.AddressParserAllowMultiple,
			Arity = ArgumentArity.OneOrMore,
		};
		Option<decimal[]> amountsOption = new("--amount")
		{
			Description = Strings.RequestPaymentAmountOptionDescription,
			Arity = ArgumentArity.OneOrMore,
		};
		Option<Memo[]> memosOption = new("--memo")
		{
			Description = Strings.RequestPaymentMemoOptionDescription,
			CustomParser = Utilities.MemoParserAllowMultiple,
			Arity = ArgumentArity.OneOrMore,
		};
		Option<string[]> labelsOption = new("--label")
		{
			Description = Strings.RequestPaymentLabelOptionDescription,
			Arity = ArgumentArity.OneOrMore,
		};
		Option<string[]> messagesOption = new("--message")
		{
			Description = Strings.RequestPaymentMessageOptionDescription,
			Arity = ArgumentArity.OneOrMore,
		};
		Option<string> saveQRCodeOption = new Option<string>("--output")
		{
			Description = Strings.RequestPaymentSaveQRCodeOption,
		}.AcceptLegalFilePathsOnly();

		Command command = new("invoice", Strings.RequestPaymentCommandDescription)
		{
			payeesArgument,
			amountsOption,
			memosOption,
			labelsOption,
			messagesOption,
			saveQRCodeOption,
		};

		command.Validators.Add(v =>
		{
			int payeeCount = v.Children.OfType<ArgumentResult>().FirstOrDefault(ar => ar.Argument == payeesArgument)?.Tokens.Count ?? 0;
			int amountsCount = v.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == amountsOption)?.Tokens.Count ?? 0;
			int memosCount = v.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == memosOption)?.Tokens.Count ?? 0;
			int labelsCount = v.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == labelsOption)?.Tokens.Count ?? 0;
			int messagesCount = v.Children.OfType<OptionResult>().FirstOrDefault(or => or.Option == messagesOption)?.Tokens.Count ?? 0;

			if (amountsCount > 0 && payeeCount != amountsCount)
			{
				v.AddError(Strings.FormatRequestPaymentArgumentCountMismatch(amountsOption.Name, payeeCount, amountsCount));
				return;
			}

			if (memosCount > 0 && payeeCount != memosCount)
			{
				v.AddError(Strings.FormatRequestPaymentArgumentCountMismatch(memosOption.Name, payeeCount, memosCount));
				return;
			}

			if (labelsCount > 0 && payeeCount != labelsCount)
			{
				v.AddError(Strings.FormatRequestPaymentArgumentCountMismatch(labelsOption.Name, payeeCount, labelsCount));
				return;
			}

			if (messagesCount > 0 && payeeCount != messagesCount)
			{
				v.AddError(Strings.FormatRequestPaymentArgumentCountMismatch(messagesOption.Name, payeeCount, messagesCount));
				return;
			}
		});

		command.SetAction((parseResult, cancellationToken) =>
		{
			return Task.FromResult(new RequestPaymentCommand
			{
				Payees = parseResult.GetValue(payeesArgument)!,
				Amounts = parseResult.GetValue(amountsOption),
				Memos = parseResult.GetValue(memosOption),
				Labels = parseResult.GetValue(labelsOption),
				Messages = parseResult.GetValue(messagesOption),
				SaveQRCodePath = parseResult.GetValue(saveQRCodeOption),
			}.Execute());
		});

		return command;
	}

	internal int Execute()
	{
		ImmutableArray<Zip321PaymentRequestUris.PaymentRequestDetails>.Builder details =
			ImmutableArray.CreateBuilder<Zip321PaymentRequestUris.PaymentRequestDetails>(this.Payees.Length);
		for (int i = 0; i < this.Payees.Length; i++)
		{
			decimal? amount = this.Amounts?.Length > 0 ? this.Amounts[i] : null;
			Memo memo = this.Memos?.Length > 0 ? this.Memos[i] : Memo.NoMemo;
			string? label = this.Labels?.Length > 0 ? this.Labels[i] : null;
			string? message = this.Messages?.Length > 0 ? this.Messages[i] : null;

			details.Add(new(this.Payees[i])
			{
				Amount = amount,
				Memo = memo,
				Label = label,
				Message = message,
			});
		}

		Zip321PaymentRequestUris.PaymentRequest request = new(details.MoveToImmutable());

		int exitCode = this.ExportQRCode(request);

		Console.WriteLine($"Uri: {request}");

		return exitCode;
	}

	private int ExportQRCode(Zip321PaymentRequestUris.PaymentRequest request)
	{
		QRCodeGenerator generator = new();
		QREncoder encoder = new();
		QRCodeData data = generator.CreateQrCode(request.ToString(), encoder.ECCLevel);

		int exitCode = 0;
		if (this.SaveQRCodePath is not null)
		{
			try
			{
				encoder.Encode(data, new FileInfo(this.SaveQRCodePath), null);
				Console.WriteLine($"QR code saved to \"{this.SaveQRCodePath}\".");
			}
			catch (NotSupportedException ex)
			{
				Console.Error.WriteLine(ex.Message);
				exitCode = 1;
			}
		}

		return exitCode;
	}
}
