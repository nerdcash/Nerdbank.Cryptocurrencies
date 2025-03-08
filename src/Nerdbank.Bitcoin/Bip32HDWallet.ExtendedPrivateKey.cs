﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using NBitcoin.Secp256k1;

namespace Nerdbank.Bitcoin;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// A BIP-39 extended private key.
	/// </summary>
	[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
	public class ExtendedPrivateKey : ExtendedKeyBase, IDisposable
	{
		/// <summary>
		/// Backing field for the <see cref="PublicKey"/> property.
		/// </summary>
		private ExtendedPublicKey? publicKey;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedPrivateKey"/> class.
		/// </summary>
		/// <param name="key">The private key backing this extended key.</param>
		/// <param name="chainCode">The chain code.</param>
		/// <param name="testNet"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in ParentFingerprint, byte, uint, bool)" path="/param[@name='testNet']"/></param>
		protected internal ExtendedPrivateKey(ECPrivKey key, in ChainCode chainCode, bool testNet)
			: this(key, chainCode, parentFingerprint: default, depth: 0, childIndex: 0, testNet)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedPrivateKey"/> class.
		/// </summary>
		/// <param name="key">The private key.</param>
		/// <param name="chainCode"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in ParentFingerprint, byte, uint, bool)" path="/param[@name='chainCode']"/></param>
		/// <param name="parentFingerprint"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in ParentFingerprint, byte, uint, bool)" path="/param[@name='parentFingerprint']"/></param>
		/// <param name="depth"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in ParentFingerprint, byte, uint, bool)" path="/param[@name='depth']"/></param>
		/// <param name="childIndex"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in ParentFingerprint, byte, uint, bool)" path="/param[@name='childIndex']"/></param>
		/// <param name="testNet"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in ParentFingerprint, byte, uint, bool)" path="/param[@name='testNet']"/></param>
		protected internal ExtendedPrivateKey(ECPrivKey key, in ChainCode chainCode, in ParentFingerprint parentFingerprint, byte depth, uint childIndex, bool testNet)
			: base(chainCode, parentFingerprint, depth, childIndex, testNet)
		{
			this.Key = new(key, testNet);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedPrivateKey"/> class.
		/// </summary>
		/// <param name="copyFrom">The key to copy from.</param>
		protected ExtendedPrivateKey(ExtendedPrivateKey copyFrom)
			: base(copyFrom)
		{
			this.Key = new(copyFrom.CryptographicKey, this.IsTestNet);
		}

		/// <summary>
		/// Gets the EC private key.
		/// </summary>
		public ECPrivKey CryptographicKey => this.Key.CryptographicKey;

		/// <summary>
		/// Gets the underlying private key that this object extends.
		/// </summary>
		public PrivateKey Key { get; }

		/// <summary>
		/// Gets the public extended key counterpart to this private key.
		/// </summary>
		public ExtendedPublicKey PublicKey => this.publicKey ??= new ExtendedPublicKey(this.CryptographicKey.CreatePubKey(), this.ChainCode, this.ParentFingerprint, this.Depth, this.ChildIndex, this.IsTestNet) { DerivationPath = this.DerivationPath };

		/// <inheritdoc/>
		public override ReadOnlySpan<byte> Identifier => this.PublicKey.Identifier;

		/// <summary>
		/// Gets the version header for private keys on mainnet.
		/// </summary>
		internal static ReadOnlySpan<byte> MainNet => new byte[] { 0x04, 0x88, 0xAD, 0xE4 };

		/// <summary>
		/// Gets the version header for private keys on testnet.
		/// </summary>
		internal static ReadOnlySpan<byte> TestNet => new byte[] { 0x04, 0x35, 0x83, 0x94 };

		/// <inheritdoc/>
		protected override ReadOnlySpan<byte> Version => this.IsTestNet ? TestNet : MainNet;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
		/// <summary>
		/// Creates an extended key based on a <see cref="Bip39Mnemonic"/>.
		/// </summary>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <param name="testNet"><see langword="true" /> when the generated key will be used to interact with the zcash testnet; <see langword="false" /> otherwise.</param>
		/// <returns>The extended key.</returns>
		public static ExtendedPrivateKey Create(Bip39Mnemonic mnemonic, bool testNet = false) => Create(Requires.NotNull(mnemonic).Seed, testNet);

		/// <summary>
		/// Creates an extended key based on a seed.
		/// </summary>
		/// <param name="seed">The seed from which to generate the master key.</param>
		/// <param name="testNet"><see langword="true" /> when the generated key will be used to interact with the zcash testnet; <see langword="false" /> otherwise.</param>
		/// <returns>The extended key.</returns>
		public static ExtendedPrivateKey Create(ReadOnlySpan<byte> seed, bool testNet = false)
		{
			Span<byte> hmac = stackalloc byte[512 / 8];
			HMACSHA512.HashData("Bitcoin seed"u8, seed, hmac);
			ReadOnlySpan<byte> masterKey = hmac[..32];
			ref readonly ChainCode chainCode = ref ChainCode.From(hmac[32..]);

			return new ExtendedPrivateKey(ECPrivKey.Create(masterKey), chainCode, testNet)
			{
				DerivationPath = Bip32KeyPath.Root,
			};
		}
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters

		/// <inheritdoc/>
		public override ExtendedPrivateKey Derive(uint childIndex)
		{
			Span<byte> hashInput = stackalloc byte[PublicKeyLength + sizeof(uint)];
			BitUtilities.WriteBE(childIndex, hashInput[PublicKeyLength..]);
			if ((childIndex & Bip32KeyPath.HardenedBit) != 0)
			{
				this.Key.CryptographicKey.WriteToSpan(hashInput[1..]);
			}
			else
			{
				this.PublicKey.Key.CryptographicKey.WriteToSpan(true, hashInput, out _);
			}

			Span<byte> hashOutput = stackalloc byte[512 / 8];
			HMACSHA512.HashData(this.ChainCode, hashInput, hashOutput);
			Span<byte> childKeyAdd = hashOutput[..32];
			ref readonly ChainCode childChainCode = ref ChainCode.From(hashOutput[32..]);

			// From the spec:
			// In case parse256(IL) ≥ n or ki = 0, the resulting key is invalid,
			// and one should proceed with the next value for i.
			// (Note: this has probability lower than 1 in 2^127.)
			if (!this.Key.CryptographicKey.TryTweakAdd(childKeyAdd, out NBitcoin.Secp256k1.ECPrivKey? pvk))
			{
				throw new InvalidKeyException(Strings.VeryUnlikelyInvalidChildKey);
			}

			byte childDepth = checked((byte)(this.Depth + 1));

			Assumes.NotNull(pvk); // bad null ref annotation in the Secp256k1 library.
			return new ExtendedPrivateKey(pvk, childChainCode, ParentFingerprint.From(this.Identifier[..4]), childDepth, childIndex, this.IsTestNet)
			{
				DerivationPath = this.DerivationPath?.Append(childIndex),
			};
		}

		/// <inheritdoc/>
		public void Dispose() => this.Key.Dispose();

		/// <inheritdoc/>
		protected override int WriteKeyMaterial(Span<byte> destination)
		{
			destination[0] = 0;
			this.Key.CryptographicKey.WriteToSpan(destination[1..]);
			return 33;
		}
	}
}
