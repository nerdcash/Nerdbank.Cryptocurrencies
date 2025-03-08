// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Nerdbank.Zcash;

/// <summary>
/// Creates and consumes URIs that can communicate a Zcash payment address and amount,
/// as is useful for merchants and customers at a point of sale system.
/// </summary>
/// <remarks>
/// This class implements <see href="https://zips.z.cash/zip-0321">ZIP-321</see>.
/// </remarks>
public static class Zip321PaymentRequestUris
{
	/// <summary>
	/// Payment details for one element in a <see cref="PaymentRequest"/>.
	/// </summary>
	/// <param name="Address">A Zcash address to receive payment.</param>
	public record PaymentRequestDetails(ZcashAddress Address)
	{
		/// <summary>
		/// Gets the label for an address (e.g. name of receiver).
		/// </summary>
		/// <remarks>
		/// If a label is present, a client rendering a payment for inspection by the user SHOULD
		/// display this label (if possible) as well as the associated address.
		/// If the label is displayed, it MUST be identifiable as distinct from the address.
		/// </remarks>
		public string? Label { get; init; }

		/// <summary>
		/// Gets or sets the memo that may be required to include in the payment.
		/// </summary>
		public Memo Memo { get; set; } = Memo.NoMemo;

		/// <summary>
		/// Gets the message to display from the requester to the payer.
		/// </summary>
		public string? Message { get; init; }

		/// <summary>
		/// Gets the requested amount (in ZEC).
		/// </summary>
		public decimal? Amount { get; init; }
	}

	/// <summary>
	/// Describes a payment request.
	/// </summary>
	/// <remarks>
	/// Use the <see cref="ToString()"/> method to access the <c>zcash:</c> URI that encodes the data in this object.
	/// </remarks>
	public record PaymentRequest
	{
		private const string Scheme = "zcash:";
		private const string AmountParam = "amount";
		private const string LabelParam = "label";
		private const string AddressParam = "address";
		private const string MemoParam = "memo";
		private const string MessageParam = "message";
		private const string RequiredParamPrefix = "req-";

		/// <summary>
		/// A lazily-initialized field that backs the <see cref="Uri"/> property.
		/// </summary>
		private string? uri;

		/// <summary>
		/// Initializes a new instance of the <see cref="PaymentRequest"/> class.
		/// </summary>
		/// <param name="payment">A single payment requested.</param>
		public PaymentRequest(PaymentRequestDetails payment)
		{
			Requires.NotNull(payment);
			this.Payments = ImmutableArray.Create(payment);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PaymentRequest"/> class.
		/// </summary>
		/// <param name="payments">One or more payments requested.</param>
		public PaymentRequest(ImmutableArray<PaymentRequestDetails> payments)
		{
			if (payments.Length is 0 or > 2109)
			{
				throw new ArgumentException(Strings.FormatLengthOutsideExpectedRange(1, 2109, payments.Length), nameof(payments));
			}

			this.Payments = payments;
		}

		/// <summary>
		/// Gets the details for the requested payments.
		/// </summary>
		public ImmutableArray<PaymentRequestDetails> Payments { get; init; }

		/// <inheritdoc cref="TryParse(string, out PaymentRequest?, out ParseError?, out string?)"/>
		public static bool TryParse(string uri, [NotNullWhen(true)] out PaymentRequest? paymentRequest) => TryParse(uri, out paymentRequest, out _, out _);

		/// <summary>
		/// Tries to parse a <c>zcash:</c> payment URI.
		/// </summary>
		/// <param name="uri">The URI to parse.</param>
		/// <param name="paymentRequest">The payment request details, if parsing is successful.</param>
		/// <param name="errorCode">The error code for the parsing error, if parsing failed.</param>
		/// <param name="errorMessage">An explanatory error message from the parsing failure, if applicable.</param>
		/// <returns>A value indicating whether parsing was successful.</returns>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" />.</exception>
		public static bool TryParse(string uri, [NotNullWhen(true)] out PaymentRequest? paymentRequest, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
		{
			Requires.NotNull(uri);

			ReadOnlySpan<char> remainingUri = uri;
			paymentRequest = null;
			if (!remainingUri.StartsWith(Scheme, StringComparison.Ordinal))
			{
				errorCode = ParseError.UnrecognizedScheme;
				int schemeSeparatorIndex = remainingUri.IndexOf(':');
				string actualScheme = schemeSeparatorIndex == -1 ? string.Empty : remainingUri[..schemeSeparatorIndex].ToString();
				errorMessage = Strings.FormatUnrecognizedScheme(Scheme, actualScheme);
				return false;
			}

			remainingUri = remainingUri.Slice(Scheme.Length);
			if (remainingUri.Length == 0)
			{
				errorCode = ParseError.MissingRequiredParameter;
				errorMessage = "This URI carries no data.";
				return false;
			}

			List<PaymentRequestDetails> payments = new();
			if (remainingUri[0] != '?')
			{
				int questionMarkIndex = remainingUri.IndexOf('?');
				ReadOnlySpan<char> addressChars = questionMarkIndex is -1 ? remainingUri : remainingUri[..questionMarkIndex];
				if (addressChars.IndexOf('%') >= 0)
				{
					errorCode = ParseError.InvalidParam;
					errorMessage = $"Address used illegal escaping.";
					return false;
				}

				if (!ZcashAddress.TryDecode(addressChars.ToString(), out DecodeError? decodeError, out errorMessage, out ZcashAddress? address))
				{
					errorCode = decodeError.ToParseError();
					return false;
				}

				payments.Add(new PaymentRequestDetails(address));
				remainingUri = questionMarkIndex is -1 ? default : remainingUri.Slice(questionMarkIndex + 1);
			}
			else
			{
				// Remove '?'
				remainingUri = remainingUri.Slice(1);
			}

			Span<byte> memoBytes = stackalloc byte[512];
			while (!remainingUri.IsEmpty)
			{
				int equalsIndex = remainingUri.IndexOf('=');
				if (equalsIndex == -1)
				{
					errorCode = ParseError.InvalidUri;
					errorMessage = "Query parameter name has no value.";
					return false;
				}
				else if (equalsIndex == 0)
				{
					errorCode = ParseError.InvalidUri;
					errorMessage = "Query parameter name is empty.";
					return false;
				}

				ReadOnlySpan<char> key = remainingUri[..equalsIndex];
				if (key.IndexOf('%') >= 0)
				{
					errorCode = ParseError.InvalidParam;
					errorMessage = $"Parameter {key} used illegal escaping in its name.";
					return false;
				}

				remainingUri = remainingUri.Slice(equalsIndex + 1);
				int amperstandIndex = remainingUri.IndexOf('&');
				ReadOnlySpan<char> rawValue = amperstandIndex is -1 ? remainingUri : remainingUri[..amperstandIndex];
				remainingUri = amperstandIndex is -1 ? default : remainingUri.Slice(amperstandIndex + 1);
				string decodedValue = Uri.UnescapeDataString(rawValue.ToString());

				int indexPosition = key.IndexOf('.');
				ReadOnlySpan<char> paramName = indexPosition is -1 ? key : key[..indexPosition];
				bool requiredParameter = paramName.StartsWith("req-", StringComparison.Ordinal);
				if (requiredParameter)
				{
					paramName = paramName.Slice(RequiredParamPrefix.Length);
				}

				int paymentIndex;
				if (indexPosition is -1)
				{
					paymentIndex = 0;
				}
				else
				{
					int lengthOfIndex = key.Length - indexPosition - 1;
					ReadOnlySpan<char> paramIndexChars = key.Slice(indexPosition + 1);
					if (lengthOfIndex is 0 or > 4 || key[indexPosition + 1] == '0' || !int.TryParse(paramIndexChars, NumberStyles.None, CultureInfo.InvariantCulture, out int paramIndex))
					{
						errorCode = ParseError.InvalidParam;
						errorMessage = $"Query parameter '{key}' contains invalid paramindex.";
						return false;
					}

					paymentIndex = paramIndex;
				}

				while (payments.Count <= paymentIndex)
				{
					payments.Add(new PaymentRequestDetails(UnknownAddress.Sentinel));
				}

				PaymentRequestDetails payment = payments[paymentIndex];
				switch (paramName)
				{
					case AddressParam:
						if (!ZcashAddress.TryDecode(decodedValue, out DecodeError? decodeError, out errorMessage, out ZcashAddress? address))
						{
							errorCode = decodeError.ToParseError();
							return false;
						}

						if (payment.Address != UnknownAddress.Sentinel)
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"The value for '{key}' has already been specified.";
							return false;
						}

						payment = payment with { Address = address };
						break;

					case AmountParam:
						if (rawValue.IndexOf('%') >= 0)
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"Parameter {key} used illegal escaping in value.";
							return false;
						}

						if (!decimal.TryParse(decodedValue, out decimal amount))
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"The {key} argument failed to parse.";
							return false;
						}

						if (payment.Amount is not null)
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"The value for '{key}' has already been specified.";
							return false;
						}

						payment = payment with { Amount = amount };
						break;

					case MemoParam:
						if (payment.Memo.MemoFormat != Zip302MemoFormat.MemoFormat.NoMemo)
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"The value for '{key}' has already been specified.";
							return false;
						}

						if (!TryBase64UrlDecode(decodedValue, memoBytes, out int bytesWritten))
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"Base64url decoding failure for value of {paramName} parameter.";
							return false;
						}

						payment = payment with { Memo = new(memoBytes[..bytesWritten]) };
						break;

					case LabelParam:
						if (payment.Label is not null)
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"The value for '{key}' has already been specified.";
							return false;
						}

						payment = payment with { Label = decodedValue };
						break;

					case MessageParam:
						if (payment.Message is not null)
						{
							errorCode = ParseError.InvalidParam;
							errorMessage = $"The value for '{key}' has already been specified.";
							return false;
						}

						payment = payment with { Message = decodedValue };
						break;

					default:
						if (requiredParameter)
						{
							errorCode = ParseError.UnrecognizedRequiredParameter;
							errorMessage = $"{paramName} is not a recognized parameter name but is required.";
							return false;
						}

						break;
				}

				payments[paymentIndex] = payment;
			}

			// Ensure that every payment has an address and other aspects are valid.
			for (int i = 0; i < payments.Count; i++)
			{
				if (payments[i].Address == UnknownAddress.Sentinel)
				{
					errorCode = ParseError.MissingRequiredParameter;
					errorMessage = $"The Zcash address for payment {i} is missing.";
					return false;
				}

				if (payments[i] is { Memo.IsEmpty: false, Address.HasShieldedReceiver: false })
				{
					errorCode = ParseError.InvalidParam;
					errorMessage = $"Payment {i} includes a memo field, but the address is not shielded.";
					return false;
				}
			}

			errorCode = null;
			errorMessage = null;
			paymentRequest = new(payments.ToImmutableArray());
			return true;
		}

		/// <summary>
		/// Parses a zcash: payment URI.
		/// </summary>
		/// <param name="uri">The <c>zcash:</c> URI to parse.</param>
		/// <returns>The data parsed out of the URI.</returns>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" />.</exception>
		/// <exception cref="UriFormatException">Thrown if the URI fails to parse.</exception>
		public static PaymentRequest Parse(string uri)
		{
			if (TryParse(uri, out PaymentRequest? paymentRequest, out _, out string? errorMessage))
			{
				return paymentRequest;
			}

			throw new UriFormatException(errorMessage);
		}

		/// <summary>
		/// Gets the URI that encodes the information in this payment request.
		/// </summary>
		/// <returns>A <c>zcash:</c> payment URI.</returns>
		public override string ToString()
		{
			if (this.uri is null)
			{
				StringBuilder uriBuilder = new();
				uriBuilder.Append(Scheme);

				if (this.Payments.Length == 1)
				{
					uriBuilder.Append(this.Payments[0].Address);
				}

				bool parametersAdded = false;
				for (int i = 0; i < this.Payments.Length; i++)
				{
					PaymentRequestDetails payment = this.Payments[i];

					if (this.Payments.Length > 1)
					{
						AppendParameter(AddressParam, payment.Address.Address, escapeValue: false);
					}

					if (payment.Amount is not null)
					{
						AppendParameter(AmountParam, payment.Amount.Value.ToString(CultureInfo.InvariantCulture), escapeValue: false);
					}

					if (payment.Memo.MemoFormat is not Zip302MemoFormat.MemoFormat.NoMemo && payment.Memo.Message != string.Empty)
					{
						ReadOnlySpan<byte> trimmedMemo = payment.Memo.RawBytes.TrimEnd((byte)0);
						AppendParameter(MemoParam, Base64UrlEncode(trimmedMemo), escapeValue: false);
					}

					AppendParameter(MessageParam, payment.Message, escapeValue: true);

					AppendParameter(LabelParam, payment.Label, escapeValue: true);

					void AppendParameter(string parameterName, string? value, bool escapeValue)
					{
						if (string.IsNullOrEmpty(value))
						{
							return;
						}

						if (parametersAdded)
						{
							uriBuilder.Append('&');
						}
						else
						{
							uriBuilder.Append('?');
						}

						parametersAdded = true;
						uriBuilder.Append(parameterName);
						if (i > 0)
						{
							uriBuilder.Append('.');
							uriBuilder.Append(i);
						}

						uriBuilder.Append('=');
						uriBuilder.Append(escapeValue ? Uri.EscapeDataString(value) : value);
					}
				}

				this.uri = uriBuilder.ToString();
			}

			return this.uri;
		}

		/// <inheritdoc/>
		public virtual bool Equals(PaymentRequest? other)
		{
			return other is not null
				&& this.Payments.SequenceEqual(other.Payments);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			HashCode result = default;
			foreach (PaymentRequestDetails payment in this.Payments)
			{
				result.Add(payment);
			}

			return result.ToHashCode();
		}

		private static bool TryBase64UrlDecode(ReadOnlySpan<char> urlEncoding, Span<byte> data, out int bytesWritten)
		{
			Span<char> base64Encoding = stackalloc char[urlEncoding.Length + 3];

			for (int i = 0; i < urlEncoding.Length; i++)
			{
				base64Encoding[i] = urlEncoding[i] switch
				{
					'-' => '+',
					'_' => '/',
					char ch => ch,
				};
			}

			int length = urlEncoding.Length;
			int paddingRequired = (4 - (length % 4)) % 4;
			base64Encoding = base64Encoding[..(length + paddingRequired)];
			base64Encoding[^paddingRequired..].Fill('=');

			return Convert.TryFromBase64Chars(base64Encoding, data, out bytesWritten);
		}

		private static string Base64UrlEncode(ReadOnlySpan<byte> data)
		{
			StringBuilder base64 = new(Convert.ToBase64String(data));
			while (base64[^1] == '=')
			{
				base64.Length--;
			}

			base64.Replace('+', '-');
			base64.Replace('/', '_');

			return base64.ToString();
		}

		private class UnknownAddress : ZcashAddress
		{
			internal static readonly UnknownAddress Sentinel = new UnknownAddress();

			private UnknownAddress()
				: base("***UNKNOWN***")
			{
			}

			public override ZcashNetwork Network => throw new NotImplementedException();

			public override bool HasShieldedReceiver => throw new NotImplementedException();

			internal override byte UnifiedTypeCode => throw new NotImplementedException();

			internal override int ReceiverEncodingLength => throw new NotImplementedException();

			public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>()
			{
				throw new NotImplementedException();
			}

			internal override int GetReceiverEncoding(Span<byte> output)
			{
				throw new NotImplementedException();
			}
		}
	}
}
