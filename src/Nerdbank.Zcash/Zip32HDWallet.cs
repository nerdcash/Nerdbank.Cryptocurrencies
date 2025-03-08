// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash;

/// <summary>
/// Shielded Hierarchical Deterministic Wallets as defined in
/// <see href="https://zips.z.cash/zip-0032">ZIP-32</see>.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public partial class Zip32HDWallet : IEquatable<Zip32HDWallet>
{
	/// <summary>
	/// The number of bits required in the original source (the entropy) used for the seeds used to derive master keys.
	/// </summary>
	/// <remarks>
	/// A 24 word seed phrase meets this requirement.
	/// </remarks>
	public const int MinimumEntropyLengthInBits = 256;

	/// <summary>
	/// The coin type to use in the key derivation path for <see cref="ZcashNetwork.MainNet"/>.
	/// </summary>
	private const uint MainNetCoinType = 133;

	/// <summary>
	/// The coin type to use in the key derivation path for <see cref="ZcashNetwork.TestNet"/>.
	/// </summary>
	private const uint TestNetCoinType = 1;

	/// <summary>
	/// The value of the <c>purpose</c> position in the key path.
	/// </summary>
	private const uint Purpose = 32;

	/// <summary>
	/// The required length (in bytes) of the seed used to derive master keys.
	/// </summary>
	private static readonly (int Min, int Max) MasterSeedAllowedLength = (32, 252);

	private Orchard.ExtendedSpendingKey masterOrchardKey;

	private Sapling.ExtendedSpendingKey masterSaplingKey;

	private Transparent.ExtendedSpendingKey masterTransparentKey;

	/// <summary>
	/// Initializes a new instance of the <see cref="Zip32HDWallet"/> class.
	/// </summary>
	/// <param name="mnemonic">The BIP-39 mnemonic used to generate this HD wallet.</param>
	/// <param name="network">The network this key should be used with.</param>
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
	public Zip32HDWallet(Bip39Mnemonic mnemonic, ZcashNetwork network = ZcashNetwork.MainNet)
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
		: this(Requires.NotNull(mnemonic).Seed, network)
	{
		this.Mnemonic = mnemonic;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Zip32HDWallet"/> class.
	/// </summary>
	/// <param name="seed">The seed used for master keys created within this wallet.</param>
	/// <param name="network">The network this key should be used with.</param>
	public Zip32HDWallet(ReadOnlySpan<byte> seed, ZcashNetwork network)
	{
		this.Network = network;
		this.Seed = seed.ToArray();
		this.masterOrchardKey = Orchard.Create(this.Seed.Span, this.Network);
		this.masterSaplingKey = Sapling.Create(this.Seed.Span, this.Network);
		this.masterTransparentKey = Transparent.Create(this.Seed.Span, this.Network);
	}

	/// <summary>
	/// Gets the network this wallet is meant to be used with.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the mnemonic that was used to create this wallet, if applicable.
	/// </summary>
	public Bip39Mnemonic? Mnemonic { get; }

	/// <summary>
	/// Gets the seed used to create this wallet.
	/// </summary>
	public ReadOnlyMemory<byte> Seed { get; }

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private string DebuggerDisplay => $"ZIP-32 HD wallet: \"{this.Mnemonic?.SeedPhrase}\"";

	/// <summary>
	/// Gets the coin type to use, considering the <see cref="Network"/> this wallet operates on.
	/// </summary>
	private uint CoinType => this.Network == ZcashNetwork.MainNet ? MainNetCoinType : TestNetCoinType;

	/// <summary>
	/// Checks whether a given mnemonic is long enough to meet Zcash security recommendations.
	/// </summary>
	/// <param name="mnemonic">The mnemonic to test.</param>
	/// <returns><see langword="true" /> if the mnemonic is long enough; <see langword="false"/> otherwise.</returns>
	public static bool HasAtLeastRecommendedEntropy(Bip39Mnemonic mnemonic) => Requires.NotNull(mnemonic).Entropy.Length * 8 >= MinimumEntropyLengthInBits;

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://zips.z.cash/zip-0032#specification-wallet-usage">ZIP-32</see> specification
	/// of <c>m / purpose' / coin_type' / account'</c> <em>for shielded accounts</em>.
	/// </summary>
	/// <inheritdoc cref="CreateKeyPath(uint, uint)"/>
	/// <remarks>
	/// With Zcash shielded accounts the recommended pattern is to have just one spending authority per account and issue
	/// unique receiving addresses via unique diversifiers to each party that may send ZEC to avoid correlation.
	/// This method creates a key path that will create the default key for the given account.
	/// </remarks>
	public Bip32KeyPath CreateKeyPath(uint account) => new(account | Bip32KeyPath.HardenedBit, new(this.CoinType | Bip32KeyPath.HardenedBit, new(Purpose | Bip32KeyPath.HardenedBit, Bip32KeyPath.Root)));

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://zips.z.cash/zip-0032#specification-wallet-usage">ZIP-32</see> specification
	/// of <c>m / purpose' / coin_type' / account' / address_index</c> <em>for shielded accounts</em>.
	/// </summary>
	/// <param name="account">
	/// <para>This level splits the key space into independent user identities, so the wallet never mixes the coins across different accounts.</para>
	/// <para>Users can use these accounts to organize the funds in the same fashion as bank accounts; for donation purposes (where all addresses are considered public), for saving purposes, for common expenses etc.</para>
	/// <para>Accounts are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>Hardened derivation is used at this level. The <see cref="Bip32KeyPath.HardenedBit"/> is added automatically if necessary.</para>
	/// <para>Software should prevent a creation of an account if a previous account does not have a transaction history (meaning none of its addresses have been used before).</para>
	/// <para>Software needs to discover all used accounts after importing the seed from an external source. Such an algorithm is described in "Account discovery" chapter.</para>
	/// </param>
	/// <param name="addressIndex">
	/// <para>The address index. Increment this to get a new receiving address that belongs to the same logical account.</para>
	/// <para>Addresses are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>This number should <em>not</em> include the <see cref="Bip32KeyPath.HardenedBit"/>.</para>
	/// </param>
	/// <returns>The key derivation path.</returns>
	/// <remarks>
	/// <para>zcashd 4.6.0 and later use <paramref name="account"/> <c>0x7fffffff</c> and hardened values for <paramref name="addressIndex"/>
	/// to generate "legacy" Sapling addresses.</para>
	/// <para>Ordinary Zcash accounts with shielded addresses should typically not use this overload
	/// because multiple spending authorities within the same account increases sync time and complexity
	/// where diversifiers typically suffice.
	/// Instead, the <see cref="CreateKeyPath(uint)"/> overload should be used, and provide a unique diversifier when creating the receiver for unique addresses.</para>
	/// </remarks>
	public Bip32KeyPath CreateKeyPath(uint account, uint addressIndex) => new(addressIndex, this.CreateKeyPath(account));

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see> specification
	/// of <c>m / purpose' / coin_type' / account'</c> <em>for transparent accounts</em>.
	/// The <see href="https://zips.z.cash/zip-0032#specification-wallet-usage">ZIP-32</see> specification suggests transparent accounts be created using BIP-44 rules.
	/// </summary>
	/// <param name="account"><inheritdoc cref="Bip44MultiAccountHD.CreateKeyPath(uint, uint, Bip44MultiAccountHD.Change, uint)" path="/param[@name='account']"/></param>
	/// <returns><inheritdoc cref="Bip44MultiAccountHD.CreateKeyPath(uint, uint, Bip44MultiAccountHD.Change, uint)" path="/returns"/></returns>
	public Bip32KeyPath CreateTransparentKeyPath(uint account) => Bip44MultiAccountHD.CreateKeyPath(this.CoinType | Bip32KeyPath.HardenedBit, account);

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see> specification
	/// of <c>m / purpose' / coin_type' / account' / change / address_index</c> <em>for transparent accounts</em>.
	/// The <see href="https://zips.z.cash/zip-0032#specification-wallet-usage">ZIP-32</see> specification suggests transparent accounts be created using BIP-44 rules.
	/// </summary>
	/// <param name="account"><inheritdoc cref="Bip44MultiAccountHD.CreateKeyPath(uint, uint, Bip44MultiAccountHD.Change, uint)" path="/param[@name='account']"/></param>
	/// <param name="change"><inheritdoc cref="Bip44MultiAccountHD.CreateKeyPath(uint, uint, Bip44MultiAccountHD.Change, uint)" path="/param[@name='change']"/></param>
	/// <param name="addressIndex"><inheritdoc cref="Bip44MultiAccountHD.CreateKeyPath(uint, uint, Bip44MultiAccountHD.Change, uint)" path="/param[@name='addressIndex']"/></param>
	/// <returns><inheritdoc cref="Bip44MultiAccountHD.CreateKeyPath(uint, uint, Bip44MultiAccountHD.Change, uint)" path="/returns"/></returns>
	public Bip32KeyPath CreateTransparentKeyPath(uint account, Bip44MultiAccountHD.Change change, uint addressIndex) => Bip44MultiAccountHD.CreateKeyPath(this.CoinType | Bip32KeyPath.HardenedBit, account, change, addressIndex);

	/// <summary>
	/// Creates a new orchard account.
	/// </summary>
	/// <param name="account">The account index. Use 0 for the first account and increment by one only after completing a transaction in the previous account so that account discovery can find all accounts.</param>
	/// <returns>The account spending key.</returns>
	public Orchard.ExtendedSpendingKey CreateOrchardAccount(uint account = 0) => this.masterOrchardKey.Derive(this.CreateKeyPath(account));

	/// <summary>
	/// Creates a new sapling account.
	/// </summary>
	/// <param name="account">The account index. Use 0 for the first account and increment by one only after completing a transaction in the previous account so that account discovery can find all accounts.</param>
	/// <returns>The account spending key.</returns>
	public Sapling.ExtendedSpendingKey CreateSaplingAccount(uint account = 0) => this.masterSaplingKey.Derive(this.CreateKeyPath(account));

	/// <summary>
	/// Creates a new transparent account.
	/// </summary>
	/// <param name="account">The account index. Use 0 for the first account and increment by one only after completing a transaction in the previous account so that account discovery can find all accounts.</param>
	/// <returns>The account spending key.</returns>
	public Transparent.ExtendedSpendingKey CreateTransparentAccount(uint account = 0) => this.masterTransparentKey.Derive(this.CreateTransparentKeyPath(account));

	/// <inheritdoc/>
	public bool Equals(Zip32HDWallet? other)
	{
		return other is not null
			&& this.Seed.Span.SequenceEqual(other.Seed.Span)
			&& this.Network == other.Network;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => this.Equals(obj as Zip32HDWallet);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return HashCode.Combine(this.Network, BitConverter.ToInt32(this.Seed.Span));
	}

	/// <summary>
	/// Encodes a <see cref="BigInteger"/> as a byte sequence in little-endian order.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <param name="output">
	/// A buffer to fill with the encoded integer.
	/// Any excess bytes will be 0-padded.
	/// </param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always its length.</returns>
	/// <remarks>This is the inverse operation to <see cref="LEOS2IP(ReadOnlySpan{byte})"/>.</remarks>
	/// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="output"/> is not large enough to store <paramref name="value"/>.</exception>
	private static int I2LEOSP(BigInteger value, Span<byte> output)
	{
		if (!value.TryWriteBytes(output, out int bytesWritten, isUnsigned: true))
		{
			throw new ArgumentException("Insufficient length", nameof(output));
		}

		return bytesWritten;
	}

	/// <summary>
	/// Decodes a <see cref="BigInteger"/> that has been encoded as a byte array in little-endian order.
	/// </summary>
	/// <param name="input">A little-endian ordered encoding of an integer.</param>
	/// <remarks>This is the inverse operation to <see cref="I2LEOSP(BigInteger, Span{byte})"/>.</remarks>
	private static BigInteger LEOS2IP(ReadOnlySpan<byte> input) => new(input, isUnsigned: true);

	/// <summary>
	/// Throws an <see cref="ArgumentException"/> if a given span has a length outside the range allowed by ZIP-32 for purposes of creating a master key.
	/// </summary>
	private static void ThrowIfSeedHasDisallowedSize(ReadOnlySpan<byte> seed)
	{
		if (seed.Length < MasterSeedAllowedLength.Min || seed.Length > MasterSeedAllowedLength.Max)
		{
			throw new ArgumentException(Strings.FormatLengthRangeNotMet(MasterSeedAllowedLength.Min, MasterSeedAllowedLength.Max), nameof(seed));
		}
	}
}
