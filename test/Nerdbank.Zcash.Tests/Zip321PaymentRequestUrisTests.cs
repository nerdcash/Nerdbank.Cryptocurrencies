// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using static Nerdbank.Zcash.Zip321PaymentRequestUris;

public class Zip321PaymentRequestUrisTests
{
	private const string ValidUri1 = "zcash:ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez?amount=1&memo=VGhpcyBpcyBhIHNpbXBsZSBtZW1vLg&message=Thank%20you%20for%20your%20purchase";
	private const string ValidUri2 = "zcash:?address=tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU&amount=123.456&address.1=ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez&amount.1=0.789&memo.1=VGhpcyBpcyBhIHVuaWNvZGUgbWVtbyDinKjwn6aE8J-PhvCfjok";

	private static readonly PaymentRequest ValidPaymentRequest1 = new(new PaymentRequestDetails(ZcashAddress.Parse("ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez"))
	{
		Memo = Memo.FromMessage("This is a simple memo."),
		Message = "Thank you for your purchase",
		Amount = 1,
	});

	private static readonly PaymentRequest ValidPaymentRequest2 = new(ImmutableArray.Create(
		new PaymentRequestDetails(ZcashAddress.Parse("tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU"))
		{
			Amount = 123.456m,
		},
		new PaymentRequestDetails(ZcashAddress.Parse("ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez"))
		{
			Memo = Memo.FromMessage("This is a unicode memo ✨🦄🏆🎉"),
			Amount = 0.789m,
		}));

	private static readonly string[] InvalidUris = new[]
	{
		// This is missing a payment address with empty paramindex.
		"zcash:?amount=3491405.05201255&address.1=ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez&amount.1=5740296.87793245",

		// This is missing a payment address with empty paramindex.
		"zcash:?address=tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU&amount=1&amount.1=2&address.2=ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez",

		// address.0= and amount.0= are not permitted as leading 0s are forbidden in paramindex.
		"zcash:?address.0=ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez&amount.0=2",

		// duplicate amount= field.
		"zcash:?amount=1.234&amount=2.345&address=tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU",

		// duplicate amount.1= field.
		"zcash:?amount.1=1.234&amount.1=2.345&address.1=tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU",

		// percent encoding is only allowed in qchar productions, which do not include addresses, amounts, or parameter names.
		"zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?amount=1%30",

		// percent encoding is only allowed in qchar productions, which do not include addresses, amounts, or parameter names.
		"zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?%61mount=1",

		// percent encoding is only allowed in qchar productions, which do not include addresses, amounts, or parameter names.
		"zcash:%74mEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?amount=1",

		// the grammar does not allow //. ZIP 321 URIs are not "hierarchical URIs" in the sense defined in 3 section 1.2.3, and do not have an "authority component".
		"zcash://tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?amount=1",
	};

	private readonly ITestOutputHelper logger;

	public Zip321PaymentRequestUrisTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void Ctor_One()
	{
		PaymentRequestDetails payment = ValidPaymentRequest1.Payments[0];
		PaymentRequest req = new(new PaymentRequestDetails(payment.Address));
		PaymentRequestDetails actual = Assert.Single(req.Payments);
		Assert.Equal(payment.Address, actual.Address);
		Assert.Null(actual.Message);
		Assert.Null(actual.Amount);
		Assert.Equal(Zip302MemoFormat.MemoFormat.NoMemo, actual.Memo.MemoFormat);
		Assert.Null(actual.Label);
	}

	[Fact]
	public void Ctor_Zero()
	{
		Assert.Throws<ArgumentException>(() => new PaymentRequest(ImmutableArray<PaymentRequestDetails>.Empty));
	}

	[Fact]
	public void ToString_MatchesUri()
	{
		Assert.Equal(ValidUri1, ValidPaymentRequest1.ToString());
	}

	[Fact]
	public void ToString_1Payment()
	{
		Assert.Equal(ValidUri1, ValidPaymentRequest1.ToString());
	}

	[Fact]
	public void ToString_2Payments()
	{
		string actual = ValidPaymentRequest2.ToString();
		this.logger.WriteLine($"Expected: {ValidUri2}");
		this.logger.WriteLine($"Actual:   {actual}");
		Assert.Equal(ValidUri2, actual);
	}

	[Fact]
	public void Parse_String()
	{
		Assert.Equal(ValidPaymentRequest1, PaymentRequest.Parse(ValidUri1));
		Assert.Equal(ValidPaymentRequest2, PaymentRequest.Parse(ValidUri2));
	}

	[Fact]
	public void Parse_1Payment()
	{
		Assert.Equal(ValidPaymentRequest1, PaymentRequest.Parse(ValidUri1));
	}

	[Fact]
	public void Parse_2Payments()
	{
		Assert.Equal(ValidPaymentRequest2, PaymentRequest.Parse(ValidUri2));
	}

	[Fact]
	public void Parse_Invalid()
	{
		foreach (string invalidUri in InvalidUris)
		{
			this.logger.WriteLine($"Testing {invalidUri}");
			UriFormatException ex = Assert.Throws<UriFormatException>(() => PaymentRequest.Parse(invalidUri));
			this.logger.WriteLine(ex.Message);
		}
	}

	[Fact]
	public void TryParse_Invalid()
	{
		foreach (string invalidUri in InvalidUris)
		{
			this.logger.WriteLine($"Testing {invalidUri}");
			Assert.False(PaymentRequest.TryParse(invalidUri, out _, out ParseError? errorCode, out string? errorMessage));
			this.logger.WriteLine($"{errorCode}: {errorMessage}");
		}
	}

	[Fact]
	public void TryParse_Null() => Assert.Throws<ArgumentNullException>(() => PaymentRequest.TryParse((string)null!, out _));

	[Fact]
	public void Parse_Null() => Assert.Throws<ArgumentNullException>(() => PaymentRequest.Parse((string)null!));

	[Fact]
	public void Label()
	{
		PaymentRequest requestWithLabel = new(ValidPaymentRequest1.Payments[0] with { Label = "Some label" });
		AssertRoundtrip(requestWithLabel);
	}

	[Fact]
	public void TryParse_WithRecognizedRequiredParameters()
	{
		Assert.True(PaymentRequest.TryParse("zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?req-amount=5", out PaymentRequest? parsed, out _, out _));
		Assert.Equal(5m, parsed.Payments[0].Amount);
	}

	[Fact]
	public void TryParse_WithRecognizedParameterAsOptionalAndRequired()
	{
		Assert.False(PaymentRequest.TryParse("zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?req-amount=5&amount=5", out _, out ParseError? errorCode, out string? errorMessage));
		this.logger.WriteLine(errorMessage);
		Assert.Equal(ParseError.InvalidParam, errorCode);
	}

	[Fact]
	public void TryParse_WithUnrecognizedRequiredParameters()
	{
		Assert.False(PaymentRequest.TryParse("zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?req-donotignore=true", out _, out ParseError? errorCode, out string? errorMessage));
		this.logger.WriteLine(errorMessage);
		Assert.Equal(ParseError.UnrecognizedRequiredParameter, errorCode);
	}

	[Fact]
	public void TryParse_WithUnrecognizedNonRequiredParameters()
	{
		Assert.True(PaymentRequest.TryParse("zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?donotignore=true", out PaymentRequest? parsed, out _, out _));
		Assert.Equal("tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU", parsed.Payments[0].Address);
	}

	[Fact]
	public void TryParse_InvalidBase64Encoding()
	{
		Assert.False(PaymentRequest.TryParse("zcash:ztestsapling10yy2ex5dcqkclhc7z7yrnjq2z6feyjad56ptwlfgmy77dmaqqrl9gyhprdx59qgmsnyfska2kez?memo=()", out _, out ParseError? errorCode, out string? errorMessage));
		this.logger.WriteLine(errorMessage);
	}

	[Fact]
	public void TryParse_MemoWithTransparentAddress()
	{
		Assert.False(PaymentRequest.TryParse("zcash:tmEZhbWHTpdKMw5it8YDspUXSMGQyFwovpU?memo=VGhpcyBpcyBhIHNpbXBsZSBtZW1vLg", out _, out ParseError? errorCode, out string? errorMessage));
		Assert.Equal(ParseError.InvalidParam, errorCode);
		this.logger.WriteLine(errorMessage);
	}

	private static void AssertRoundtrip(PaymentRequest request) => Assert.Equal(request, PaymentRequest.Parse(request.ToString()));
}
