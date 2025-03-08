// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Bitcoin.Bip32HDWallet;

public class ExtendedKeyBaseTests : Bip32HDWalletTestBase
{
	public ExtendedKeyBaseTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	[Theory]
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6LBpB85b3D2yc8sfvZU521AAwdZafEz7mnzBBsz4wKY5fTtTQBm", DecodeError.InvalidKey)] // pubkey version / prvkey mismatch
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFGTQQD3dC4H2D5GBj7vWvSQaaBv5cxi9gafk7NF3pnBju6dwKvH", DecodeError.InvalidKey)] // prvkey version / pubkey mismatch
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6Txnt3siSujt9RCVYsx4qHZGc62TG4McvMGcAUjeuwZdduYEvFn", DecodeError.InvalidKey)] // invalid pubkey prefix 04
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFGpWnsj83BHtEy5Zt8CcDr1UiRXuWCmTQLxEK9vbz5gPstX92JQ", DecodeError.InvalidKey)] // invalid prvkey prefix 04
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6N8ZMMXctdiCjxTNq964yKkwrkBJJwpzZS4HS2fxvyYUA4q2Xe4", DecodeError.InvalidKey)] // invalid pubkey prefix 01
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFAzHGBP2UuGCqWLTAPLcMtD9y5gkZ6Eq3Rjuahrv17fEQ3Qen6J", DecodeError.InvalidKey)] // invalid prvkey prefix 01
	[InlineData("xprv9s2SPatNQ9Vc6GTbVMFPFo7jsaZySyzk7L8n2uqKXJen3KUmvQNTuLh3fhZMBoG3G4ZW1N2kZuHEPY53qmbZzCHshoQnNf4GvELZfqTUrcv", DecodeError.InvalidDerivationData)] // zero depth with non-zero parent fingerprint
	[InlineData("xpub661no6RGEX3uJkY4bNnPcw4URcQTrSibUZ4NqJEw5eBkv7ovTwgiT91XX27VbEXGENhYRCf7hyEbWrR3FewATdCEebj6znwMfQkhRYHRLpJ", DecodeError.InvalidDerivationData)] // zero depth with non-zero parent fingerprint
	[InlineData("xprv9s21ZrQH4r4TsiLvyLXqM9P7k1K3EYhA1kkD6xuquB5i39AU8KF42acDyL3qsDbU9NmZn6MsGSUYZEsuoePmjzsB3eFKSUEh3Gu1N3cqVUN", DecodeError.InvalidDerivationData)] // zero depth with non-zero index
	[InlineData("xpub661MyMwAuDcm6CRQ5N4qiHKrJ39Xe1R1NyfouMKTTWcguwVcfrZJaNvhpebzGerh7gucBvzEQWRugZDuDXjNDRmXzSZe4c7mnTK97pTvGS8", DecodeError.InvalidDerivationData)] // zero depth with non-zero index
	[InlineData("DMwo58pR1QLEFihHiXPVykYB6fJmsTeHvyTp7hRThAtCX8CvYzgPcn8XnmdfHGMQzT7ayAmfo4z3gY5KfbrZWZ6St24UVf2Qgo6oujFktLHdHY4", DecodeError.UnrecognizedVersion)] // unknown extended key version
	[InlineData("DMwo58pR1QLEFihHiXPVykYB6fJmsTeHvyTp7hRThAtCX8CvYzgPcn8XnmdfHPmHJiEDXkTiJTVV9rHEBUem2mwVbbNfvT2MTcAqj3nesx8uBf9", DecodeError.UnrecognizedVersion)] // unknown extended key version
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzF93Y5wvzdUayhgkkFoicQZcP3y52uPPxFnfoLZB21Teqt1VvEHx", DecodeError.InvalidKey)] // private key 0 not in 1..n-1
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFAzHGBP2UuGCqWLTAPLcMtD5SDKr24z3aiUvKr9bJpdrcLg1y3G", DecodeError.InvalidKey)] // private key n not in 1..n-1
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6Q5JXayek4PRsn35jii4veMimro1xefsM58PgBMrvdYre8QyULY", DecodeError.InvalidKey)] // invalid pubkey 020000000000000000000000000000000000000000000000000000000000000007
	[InlineData("xprv9s21ZrQH143K3QTDL4LXw2F7HEK3wJUD2nW2nRk4stbPy6cq3jPPqjiChkVvvNKmPGJxWUtg6LnF5kejMRNNU3TGtRBeJgk33yuGBxrMPHL", DecodeError.InvalidChecksum)] // invalid checksum
	public void ExtendedKey_TryDecode(string base58Encoded, DecodeError expectedDecodeError)
	{
		this.logger.WriteLine($"Decoding {base58Encoded}");
		Assert.False(ExtendedKeyBase.TryDecode(base58Encoded, out DecodeError? decodeError, out string? errorMessage, out _));
		this.logger.WriteLine($"DecodeError {decodeError}: {errorMessage}");
		Assert.Equal(expectedDecodeError, decodeError.Value);
	}

	[Fact]
	public void ExtendedKeyBase_TryDecode_Empty()
	{
		Assert.False(ExtendedKeyBase.TryDecode(string.Empty, out DecodeError? decodeError, out string? errorMessage, out _));
		this.logger.WriteLine($"DecodeError {decodeError}: {errorMessage}");
	}

	[Fact]
	public void ExtendedKeyBase_Decode()
	{
		Assert.Throws<FormatException>(() => ExtendedKeyBase.Decode("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFGpWnsj83BHtEy5Zt8CcDr1UiRXuWCmTQLxEK9vbz5gPstX92JQ"));
	}
}
