// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// A BIP-39 extended private key.
	/// </summary>
	public class ExtendedPrivateKey : ExtendedKeyBase, IDisposable
	{
		private readonly PrivateKey key;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedPrivateKey"/> class.
		/// </summary>
		/// <param name="key">The private key backing this extended key.</param>
		/// <param name="chainCode">The chain code.</param>
		/// <param name="testNet"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='testNet']"/></param>
		internal ExtendedPrivateKey(PrivateKey key, ReadOnlySpan<byte> chainCode, bool testNet = false)
			: this(key, chainCode, parentFingerprint: default, depth: 0, childNumber: 0, testNet)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedPrivateKey"/> class.
		/// </summary>
		/// <param name="key">The private key.</param>
		/// <param name="chainCode"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='chainCode']"/></param>
		/// <param name="parentFingerprint"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='parentFingerprint']"/></param>
		/// <param name="depth"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='depth']"/></param>
		/// <param name="childNumber"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='childNumber']"/></param>
		/// <param name="testNet"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='testNet']"/></param>
		internal ExtendedPrivateKey(PrivateKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFingerprint, byte depth, uint childNumber, bool testNet = false)
			: base(chainCode, parentFingerprint, depth, childNumber, testNet)
		{
			this.key = key;
			this.PublicKey = new ExtendedPublicKey(this.key.CreatePublicKey(), this.ChainCode, this.ParentFingerprint, this.Depth, this.ChildNumber, this.IsTestNet);
		}

		/// <summary>
		/// Gets the public extended key counterpart to this private key.
		/// </summary>
		public ExtendedPublicKey PublicKey { get; }

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

		/// <summary>
		/// Creates an extended key based on a <see cref="Bip39Mnemonic"/>.
		/// </summary>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <returns>The extended key.</returns>
		public static ExtendedPrivateKey Create(Bip39Mnemonic mnemonic) => Create(Requires.NotNull(mnemonic).Seed);

		/// <summary>
		/// Creates an extended key based on a seed.
		/// </summary>
		/// <param name="seed">The seed from which to generate the master key.</param>
		/// <returns>The extended key.</returns>
		public static ExtendedPrivateKey Create(ReadOnlySpan<byte> seed)
		{
			Span<byte> hmac = stackalloc byte[512 / 8];
			HMACSHA512.HashData("Bitcoin seed"u8, seed, hmac);
			ReadOnlySpan<byte> masterKey = hmac[..32];
			ReadOnlySpan<byte> chainCode = hmac[32..];

			return new ExtendedPrivateKey(new PrivateKey(NBitcoin.Secp256k1.ECPrivKey.Create(masterKey)), chainCode);
		}

		/// <summary>
		/// Derives a new extended private key that is a direct child of this one.
		/// </summary>
		/// <param name="childNumber">The child key number to derive. This may include the <see cref="HardenedBit"/> to derive a hardened key.</param>
		/// <returns>A derived extended private key.</returns>
		/// <exception cref="InvalidKeyException">
		/// Thrown in a statistically extremely unlikely event of the derived key being invalid.
		/// Callers should handle this exception by requesting a new key with an incremented value
		/// for <paramref name="childNumber"/>.
		/// </exception>
		public ExtendedPrivateKey Derive(uint childNumber)
		{
			Span<byte> hashInput = stackalloc byte[PublicKeyLength + sizeof(uint)];
			BitUtilities.WriteBE(childNumber, hashInput[PublicKeyLength..]);
			if ((childNumber & HardenedBit) != 0)
			{
				this.key.Key.WriteToSpan(hashInput[1..]);
			}
			else
			{
				this.PublicKey.Key.Key.WriteToSpan(true, hashInput, out _);
			}

			Span<byte> hashOutput = stackalloc byte[512 / 8];
			HMACSHA512.HashData(this.ChainCode, hashInput, hashOutput);
			Span<byte> childKeyAdd = hashOutput[..32];
			Span<byte> childChainCode = hashOutput[32..];

			// From the spec:
			// In case parse256(IL) ≥ n or ki = 0, the resulting key is invalid,
			// and one should proceed with the next value for i.
			// (Note: this has probability lower than 1 in 2^127.)
			if (!this.key.Key.TryTweakAdd(childKeyAdd, out NBitcoin.Secp256k1.ECPrivKey? pvk))
			{
				throw new InvalidKeyException(Strings.VeryUnlikelyInvalidChildKey);
			}

			byte childDepth = checked((byte)(this.Depth + 1));

			Assumes.NotNull(pvk); // bad null ref annotation in the Secp256k1 library.
			return new ExtendedPrivateKey(new(pvk), childChainCode, this.Identifier[..4], childDepth, childNumber, this.IsTestNet);
		}

		/// <summary>
		/// Derives a new extended private key by following the steps in the specified path.
		/// </summary>
		/// <param name="keyPath">The derivation path to follow to produce the new key.</param>
		/// <returns>A derived extended private key.</returns>
		/// <exception cref="InvalidKeyException">
		/// Thrown in a statistically extremely unlikely event of the derived key being invalid.
		/// Callers should handle this exception by requesting a new key with an incremented value
		/// for the child number at the failing position in the key path.
		/// </exception>
		public ExtendedPrivateKey Derive(KeyPath keyPath)
		{
			Requires.NotNull(keyPath);

			if (this.Depth > 0 && keyPath.IsRooted)
			{
				throw new NotSupportedException("Deriving with a rooted key path from a non-rooted key is not supported.");
			}

			ExtendedPrivateKey result = this;
			ExtendedPrivateKey? intermediate = null;
			foreach (KeyPath step in keyPath.Steps)
			{
				try
				{
					result = result.Derive(step.Index);

					// If this isn't our first time around, dispose of the previous intermediate key,
					// taking care to not dispose of the original key.
					intermediate?.Dispose();
					intermediate = result;
				}
				catch (InvalidKeyException ex)
				{
					throw new InvalidKeyException(Strings.FormatVeryUnlikelyUnvalidChildKeyOnPath(step), ex) { KeyPath = step };
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public void Dispose() => this.key.Dispose();

		/// <inheritdoc/>
		protected override int WriteKeyMaterial(Span<byte> destination)
		{
			destination[0] = 0;
			this.key.Key.WriteToSpan(destination[1..]);
			return 33;
		}
	}
}
