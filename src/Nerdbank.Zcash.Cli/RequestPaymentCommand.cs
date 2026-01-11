// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.CommandLine;
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
		Argument<ZcashAddress[]> payeesArgument = new("payee", parse: Utilities.AddressParserAllowMultiple, description: Strings.RequestPaymentPayeeArgumentDescription) { Arity = ArgumentArity.OneOrMore };
		Option<decimal[]> amountsOption = new("--amount", Strings.RequestPaymentAmountOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<Memo[]> memosOption = new("--memo", parseArgument: Utilities.MemoParserAllowMultiple, description: Strings.RequestPaymentMemoOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<string[]> labelsOption = new("--label", Strings.RequestPaymentLabelOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<string[]> messagesOption = new("--message", Strings.RequestPaymentMessageOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<string> saveQRCodeOption = new Option<string>("--output", Strings.RequestPaymentSaveQRCodeOption).LegalFilePathsOnly();

		Command command = new("invoice", Strings.RequestPaymentCommandDescription)
		{
			payeesArgument,
			amountsOption,
			memosOption,
			labelsOption,
			messagesOption,
			saveQRCodeOption,
		};

		command.AddValidator(v =>
		{
			int payeeCount = v.FindResultFor(payeesArgument)?.Tokens.Count ?? 0;
			int amountsCount = v.FindResultFor(amountsOption)?.Tokens.Count ?? 0;
			int memosCount = v.FindResultFor(memosOption)?.Tokens.Count ?? 0;
			int labelsCount = v.FindResultFor(labelsOption)?.Tokens.Count ?? 0;
			int messagesCount = v.FindResultFor(messagesOption)?.Tokens.Count ?? 0;

			if (amountsCount > 0 && payeeCount != amountsCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(amountsOption.Name, payeeCount, amountsCount);
				return;
			}

			if (memosCount > 0 && payeeCount != memosCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(memosOption.Name, payeeCount, memosCount);
				return;
			}

			if (labelsCount > 0 && payeeCount != labelsCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(labelsOption.Name, payeeCount, labelsCount);
				return;
			}

			if (messagesCount > 0 && payeeCount != messagesCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(messagesOption.Name, payeeCount, messagesCount);
				return;
			}
		});

		command.SetHandler(parseResult =>
		{
			return new RequestPaymentCommand
			{
				Payees = parseResult.GetValue(payeesArgument),
				Amounts = parseResult.GetValue(amountsOption),
				Memos = parseResult.GetValue(memosOption),
				Labels = parseResult.GetValue(labelsOption),
				Messages = parseResult.GetValue(messagesOption),
				SaveQRCodePath = parseResult.GetValue(saveQRCodeOption),
			}.Execute();
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
