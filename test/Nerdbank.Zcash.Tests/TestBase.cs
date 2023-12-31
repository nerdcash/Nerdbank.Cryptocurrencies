// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nerdbank.Bitcoin;

public abstract class TestBase
{
	// Some of these addresses are real. Others are made up. Don't send ZEC to any of these addresses.
	protected const string ValidUnifiedAddressOrchardSaplingTransparentP2PKH = "u1vv2ws6xhs72faugmlrasyeq298l05rrj6wfw8hr3r29y3czev5qt4ugp7kylz6suu04363ze92dfg8ftxf3237js0x9p5r82fgy47xkjnw75tqaevhfh0rnua72hurt22v3w3f7h8yt6mxaa0wpeeh9jcm359ww3rl6fj5ylqqv54uuwrs8q4gys9r3cxdm3yslsh3rt6p7wznzhky7";
	protected const string ValidUnifiedAddressOrchardSaplingTransparentP2PKHTestNet = "utest10ha2348dhgtt6rx8wkq8pf0pneuc5egww80z7f0tphpst8zwta4jlllm5eq2qcumhak0kwzd5lnyduxtlppdv4m7g92x8q0vqncxyz9al5czsahuqtgu445tw34nerza3grckyx3szwd3rad0w3gkqq5wt3vd4falra7kqk97tv03cch2c37vh22clntp3yc5elryevtlthx26s67pw";
	protected const string ValidUnifiedAddressOrchardSaplingTransparentP2SH = "u1lmh6aqeg03rhuslr3jyv56cacmgamzrnzfl009cj50x8j6q5rdcqcc43wznhumqm0tuqe0mffak66tzwxy7u5apfrx99z3l2pt7t45s3usymxk70q2gzgxedz9vknrs8x8dhx69v4z6aghfervkxcpwy7jj2uh80e8xxmg2drwy4nn29s8t7xarq6d7pxwam8v05j9je8trd58jk95v";
	protected const string ValidUnifiedAddressOrchardSapling = "u10p78pgwpatn9n5zsut79577c78yt59cerl0ymdk7m4ug3hd7cw0fj2c7k20q3ndt2x49zzy69xgl22wr7tgl652lxflaex79xgpg2kyk9m83nzerccpvkxfy47v7xz6g5fqaz3x4tvl6lnkh58j6mj60synt2kr5rgxcpdm3qq9u0nm2";
	protected const string ValidUnifiedAddressOrchardSaplingTestNet = "utest1z068qyffv23yx9p3xgnwe47a23stg08y8yywedv69tne0rpn2adegeuau4nfyc346rxwh2zxm8dtyj8yz2s7zsrj7ymxg7xwupuy3qlgk6xrv3zvt44qnf4gs5ksv858ms79s3l4jvkjtayulpff52znp50ea9nytdkjepy0kqkpg6de";
	protected const string ValidUnifiedAddressOrchard = "u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67";
	protected const string ValidUnifiedAddressOrchardTestNet = "utest1429m7wc0m5hkkcqmqyzjnl5yuxzfxte0pr2f4vfu8wt2ru0eprd8g8jjskkk9zzekef34vkrlruncdmwal7n4g0qft2el322ccqseutk";
	protected const string ValidUnifiedAddressSapling = "u12s5xnr2r6jj4xt72qjx35mru9mq3w3v0mxkvtd67e9fsr4tzf682983qn752kf5fvcdva79pr2udwhg5sm4pw6np90t8q6q8tcu97k6c";
	protected const string ValidUnifiedAddressSaplingTestNet = "utest10c5kutapazdnf8ztl3pu43nkfsjx89fy3uuff8tsmxm6s86j37pe7uz94z5jhkl49pqe8yz75rlsaygexk6jpaxwx0esjr8wm5ut7d5s";
	protected const string ValidSaplingAddress = "zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy";
	protected const string ValidSaplingAddressTestNet = "ztestsapling15740genxvp99m3vut5q7dqm0da9l8nst2njae3kpu6e406peeypk0n78zue0hgxt5gmasaznnm0";
	protected const string ValidSaplingAddress2 = "zs128vyqqzav2kvhm6zc5gagacm9c3cgrhkejatf5pn6pla6365rdgwn0kk0pmkwd36xwug77fkmhm";
	protected const string ValidSproutAddress = "zc8E5gYid86n4bo2Usdq1cpr7PpfoJGzttwBHEEgGhGkLUg7SPPVFNB2AkRFXZ7usfphup5426dt1buMmY3fkYeRrQGLa8y";
	protected const string ValidSproutAddressTestNet = "ztJ1EWLKcGwF2S4NA17pAJVdco8Sdkz4AQPxt1cLTEfNuyNswJJc2BbBqYrsRZsp31xbVZwhF7c7a2L9jsF3p3ZwRWpqqyS";
	protected const string ValidTransparentP2PKHAddress = "t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF";
	protected const string ValidTransparentP2SHAddress = "t3JZcvsuaXE6ygokL4XUiZSTrQBUoPYFnXJ";

	protected static readonly Uri LightWalletServerMainNet = new("https://zcash.mysideoftheweb.com:9067/");

	protected static readonly Uri LightWalletServerTestNet = new("https://zcash.mysideoftheweb.com:19067/");

	protected static readonly Bip39Mnemonic Mnemonic = Bip39Mnemonic.Parse("weapon solid program critic you long skill foot damp kingdom west history car crunch park increase excite hidden bless spot matter razor memory garbage");

	/// <summary>
	/// The maximum length of time to wait for something that we expect will happen
	/// within the timeout.
	/// </summary>
	protected static readonly TimeSpan UnexpectedTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10);

	/// <summary>
	/// The maximum length of time to wait for something that we do not expect will happen
	/// within the timeout.
	/// </summary>
	protected static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Gets or sets the source of <see cref="TimeoutToken"/> that influences
	/// when tests consider themselves to be timed out.
	/// </summary>
	protected CancellationTokenSource TimeoutTokenSource { get; set; } = new CancellationTokenSource(UnexpectedTimeout);

	/// <summary>
	/// Gets a token that is canceled when the test times out,
	/// per the policy set by <see cref="TimeoutTokenSource"/>.
	/// </summary>
	protected CancellationToken TimeoutToken => this.TimeoutTokenSource.Token;

	protected static bool TryDecodeViaInterface<T>(string encoded, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
		where T : IKeyWithTextEncoding
	{
		return T.TryDecode(encoded, out decodeError, out errorMessage, out key);
	}
}
