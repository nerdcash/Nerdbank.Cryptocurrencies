﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// A spending key.
/// </summary>
public class SpendingKey : ISpendingKey, IUnifiedEncodingElement
{
	private const string Bech32mMainNetworkHRP = "secret-orchard-sk-main";
	private const string Bech32mTestNetworkHRP = "secret-orchard-sk-test";

	private readonly Bytes32 value;
	private string? textEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="SpendingKey"/> class.
	/// </summary>
	/// <param name="value">The 32-byte secret.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal SpendingKey(ReadOnlySpan<byte> value, ZcashNetwork network)
	{
		this.value = new(value);
		this.Network = network;
		this.FullViewingKey = this.CreateFullViewingKey();
	}

	/// <summary>
	/// Gets the full viewing key.
	/// </summary>
	public FullViewingKey FullViewingKey { get; }

	/// <inheritdoc/>
	IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

	/// <summary>
	/// Gets the incoming viewing key.
	/// </summary>
	public IncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

	/// <summary>
	/// Gets the Zcash network this key operates on.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the Bech32m encoding of the spending key.
	/// </summary>
	public string TextEncoding
	{
		get
		{
			if (this.textEncoding is null)
			{
				Span<char> encodedChars = stackalloc char[512];
				string hrp = this.Network switch
				{
					ZcashNetwork.MainNet => Bech32mMainNetworkHRP,
					ZcashNetwork.TestNet => Bech32mTestNetworkHRP,
					_ => throw new NotSupportedException(),
				};
				int charLength = Bech32.Bech32m.Encode(hrp, this.value, encodedChars);
				this.textEncoding = new string(encodedChars[..charLength]);
			}

			return this.textEncoding;
		}
	}

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Orchard;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => this.Value.Length;

	/// <summary>
	/// Gets the buffer. Always 32 bytes in length.
	/// </summary>
	internal ReadOnlySpan<byte> Value => this.value;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination) => this.Value.CopyToRetLength(destination);

	/// <summary>
	/// Reads the key from its representation in a unified key.
	/// </summary>
	/// <param name="keyContribution">The data that would have been written by <see cref="IUnifiedEncodingElement.WriteUnifiedData(Span{byte})"/>.</param>
	/// <param name="network">The network the key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IUnifiedEncodingElement DecodeUnifiedKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network) => new SpendingKey(keyContribution, network);

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	private FullViewingKey CreateFullViewingKey()
	{
		Span<byte> fvk = stackalloc byte[96];
		if (NativeMethods.TryDeriveOrchardFullViewingKeyFromSpendingKey(this.Value, fvk) != 0)
		{
			throw new ArgumentException(Strings.InvalidKey);
		}

		return new(fvk, this.Network);
	}
}
