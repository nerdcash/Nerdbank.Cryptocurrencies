// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// A particular crypto or fiat currency.
/// </summary>
/// <param name="TickerSymbol">The ticker symbol for the security (e.g. BTC, ZEC, or USD).</param>
/// <param name="Name">The conversational name of the security (e.g. Bitcoin, Zcash, or US Dollars).</param>
/// <param name="Precision">The number of digits after the decimal point that may be required to represent the smallest divisible fraction of the security.</param>
/// <param name="IsTestNet">A value indicating whether this security is only used on test networks.</param>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public record Security(string TickerSymbol, string? Name = null, int Precision = 8, bool IsTestNet = false)
{
#pragma warning disable CS1591, SA1600
	public static readonly Security ACM = new("ACM", "Actinium");
	public static readonly Security ADA = new("ADA", "Cardano");
	public static readonly Security AEON = new("AEON", "Aeon");
	public static readonly Security ALGO = new("ALGO", "Algorand");
	public static readonly Security ARQ = new("ARQ", "ArQmA");
	public static readonly Security ARRR = new("ARRR", "Pirate", 8);
	public static readonly Security ATOM = new("ATOM", "Cosmos");
	public static readonly Security AUD = new("AUD", "Australian Dollar", 2);
	public static readonly Security BAND = new("BAND", "Band Protocol");
	public static readonly Security BAT = new("BAT", "Basic Attention Token");
	public static readonly Security BBS = new("BBS", "BBSCoin");
	public static readonly Security BCH = new("BCH", "Bitcoin Cash");
	public static readonly Security BCHA = new("BCHA", "Bitcoin Cash ABC");
	public static readonly Security BCN = new("BCN", "Bytecoin");
	public static readonly Security BEAM = new("BEAM", "Beam");
	public static readonly Security BITC = new("BITC", "BitCash");
	public static readonly Security BKC = new("BKC", "Balkancoin");
	public static readonly Security BLOC = new("BLOC", "BLOC.money");
	public static readonly Security BNB = new("BNB", "Binance Coin", 2);
	public static readonly Security BTC = new("BTC", "Bitcoin", 8);
	public static readonly Security BTCP = new("BTCP", "Bitcoin Private");
	public static readonly Security BUSD = new("BUSD", "BUSD");
	public static readonly Security CASH = new("CASH", "Litecash");
	public static readonly Security CCX = new("CCX", "Conceal");
	public static readonly Security CIV = new("CIV", "Civitas");
	public static readonly Security COAL = new("COAL", "BitCoal");
	public static readonly Security COMP = new("COMP", "Compound");
	public static readonly Security CUT = new("CUT", "CUTcoin");
	public static readonly Security D = new("D", "Denarius");
	public static readonly Security DAI = new("DAI", "DAI");
	public static readonly Security DASH = new("DASH", "Dash");
	public static readonly Security DERO = new("DERO", "Dero");
	public static readonly Security DOGE = new("DOGE", "Dogecoin");
	public static readonly Security EGLD = new("EGLD", "Elrond");
	public static readonly Security ENJ = new("ENJ", "Enjin Coin");
	public static readonly Security EOS = new("EOS", "EOS");
	public static readonly Security ERG = new("ERG", "Ergo");
	public static readonly Security ETC = new("ETC", "Ethereum Classic");
	public static readonly Security ETH = new("ETH", "Ethereum");
	public static readonly Security ETN = new("ETN", "Electroneum");
	public static readonly Security ETNX = new("ETNX", "Electronero");
	public static readonly Security ETNXP = new("ETNXP", "ElectroneroPulse");
	public static readonly Security EUR = new("EUR", "Euro", 2);
	public static readonly Security FBF = new("FBF", "FreelaBit");
	public static readonly Security FIL = new("FIL", "Filecoin");
	public static readonly Security FLUX = new("FLUX", "Flux");
	public static readonly Security GHOST = new("GHOST", "Ghost");
	public static readonly Security GPKR = new("GPKR", "Gold Poker");
	public static readonly Security GRFT = new("GRFT", "Graft");
	public static readonly Security GRIMM = new("GRIMM", "Grimm");
	public static readonly Security GRIN = new("GRIN", "Grin");
	public static readonly Security GRLC = new("GRLC", "Garlicoin");
	public static readonly Security HBAR = new("HBAR", "Hedera Hashgraph");
	public static readonly Security HNT = new("HNT", "Helium");
	public static readonly Security ICX = new("ICX", "ICON");
	public static readonly Security INC = new("INC", "Incognito");
	public static readonly Security INTU = new("INTU", "INTUcoin");
	public static readonly Security IOTA = new("IOTA", "MIOTA");
	public static readonly Security IRD = new("IRD", "Iridium");
	public static readonly Security KNC = new("KNC", "KyberNetwork");
	public static readonly Security KNCL = new("KNCL", "KyberNetwork Crystal Legacy");
	public static readonly Security KRB = new("KRB", "Karbo");
	public static readonly Security LINK = new("LINK", "ChainLink");
	public static readonly Security LNS = new("LNS", "Lines");
	public static readonly Security LTC = new("LTC", "Litecoin");
	public static readonly Security LTHN = new("LTHN", "Lethean");
	public static readonly Security LUX = new("LUX", "LUXCoin");
	public static readonly Security MANA = new("MANA", "Decentraland");
	public static readonly Security MAT = new("MAT", "Matka");
	public static readonly Security MATIC = new("MATIC", "Polygon");
	public static readonly Security MKR = new("MKR", "Maker");
	public static readonly Security MSR = new("MSR", "Masari");
	public static readonly Security MWC = new("MWC", "MWC");
	public static readonly Security NAH = new("NAH", "Strayacoin");
	public static readonly Security NBR = new("NBR", "Niobio");
	public static readonly Security NCP = new("NCP", "Newton");
	public static readonly Security NEO = new("NEO", "NEO");
	public static readonly Security OMB = new("OMB", "Ombre");
	public static readonly Security OMG = new("OMG", "OMG Network");
	public static readonly Security ONE = new("ONE", "Harmony");
	public static readonly Security ONION = new("ONION", "DeepOnion");
	public static readonly Security ONT = new("ONT", "Ontology");
	public static readonly Security OXEN = new("OXEN", "Oxen");
	public static readonly Security OXT = new("OXT", "Orchid");
	public static readonly Security PAXG = new("PAXG", "PAX Gold");
	public static readonly Security PCN = new("PCN", "PeepCoin");
	public static readonly Security PIVX = new("PIVX", "PIVX");
	public static readonly Security PLURA = new("PLURA", "PluraCoin");
	public static readonly Security POT = new("POT", "Potcoin");
	public static readonly Security PRCY = new("PRCY", "PRCYCoin");
	public static readonly Security PURK = new("PURK", "Purk");
	public static readonly Security QTUM = new("QTUM", "QTUM");
	public static readonly Security QUAN = new("QUAN", "Quantis");
	public static readonly Security REP = new("REP", "Augur v2");
	public static readonly Security REPV1 = new("REPV1", "Augur");
	public static readonly Security RTO = new("RTO", "Arto");
	public static readonly Security RVN = new("RVN", "Ravencoin");
	public static readonly Security RYO = new("RYO", "Ryo");
	public static readonly Security SHB = new("SHB", "SkyHub");
	public static readonly Security SHIB = new("SHIB", "Shiba Inu");
	public static readonly Security SIN = new("SIN", "SINOVATE");
	public static readonly Security SLD = new("SLD", "Soldo");
	public static readonly Security SOL = new("SOL", "Solana");
	public static readonly Security SOLACE = new("SOLACE", "Solacecoin");
	public static readonly Security STORJ = new("STORJ", "Storj");
	public static readonly Security SUMO = new("SUMO", "Sumokoin");
	public static readonly Security TAZ = new("TAZ", "Zcash (testnet)", IsTestNet: true);
	public static readonly Security TRTL = new("TRTL", "TurtleCoin");
	public static readonly Security TUBE = new("TUBE", "BitTube");
	public static readonly Security UNI = new("UNI", "Uniswap");
	public static readonly Security UPX = new("UPX", "uPlexa");
	public static readonly Security USD = new("USD", "US Dollar", 2);
	public static readonly Security USDC = new("USDC", "USD Coin");
	public static readonly Security USDT = new("USDT", "TetherUS");
	public static readonly Security VEIL = new("VEIL", "Veil");
	public static readonly Security VET = new("VET", "VeChain");
	public static readonly Security VTHO = new("VTHO", "VeThor Token");
	public static readonly Security WAE = new("WAE", "WeyCoin");
	public static readonly Security WAVES = new("WAVES", "Waves");
	public static readonly Security WOW = new("WOW", "Wownero");
	public static readonly Security WTIP = new("WTIP", "Worktips");
	public static readonly Security XAO = new("XAO", "Alloy");
	public static readonly Security XEQ = new("XEQ", "Equilibria");
	public static readonly Security XGM = new("XGM", "Defis");
	public static readonly Security XGS = new("XGS", "GenesisX");
	public static readonly Security XHV = new("XHV", "Haven");
	public static readonly Security XLA = new("XLA", "Scala");
	public static readonly Security XLM = new("XLM", "Stellar Lumens");
	public static readonly Security XMC = new("XMC", "Monero Classic");
	public static readonly Security XMR = new("XMR", "Monero");
	public static readonly Security XMV = new("XMV", "MoneroV");
	public static readonly Security XNO = new("XNO", "NANO");
	public static readonly Security XNV = new("XNV", "Nerva");
	public static readonly Security XPP = new("XPP", "PrivatePay");
	public static readonly Security XRN = new("XRN", "Saronite");
	public static readonly Security XRP = new("XRP", "XRP");
	public static readonly Security XTA = new("XTA", "Italo");
	public static readonly Security XTNC = new("XTNC", "XtendCash");
	public static readonly Security XTZ = new("XTZ", "Tezos");
	public static readonly Security XUN = new("XUN", "UltraNote");
	public static readonly Security XUSD = new("XUSD", "xUSD");
	public static readonly Security XVG = new("XVG", "Verge");
	public static readonly Security XWP = new("XWP", "Swap");
	public static readonly Security ZANO = new("ZANO", "Zano");
	public static readonly Security ZEC = new("ZEC", "Zcash", 8);
	public static readonly Security ZEN = new("ZEN", "Horizen");
	public static readonly Security ZIL = new("ZIL", "Zilliqa");
	public static readonly Security ZRX = new("ZRX", "0x");
#pragma warning restore CS1591, SA1600

	/// <summary>
	/// Gets a collection of well-known securities, keyed by their ticker symbol.
	/// </summary>
	/// <remarks>
	/// All static fields of type <see cref="Security"/> from this class are included in this collection.
	/// The dictionary is case-insensitive.
	/// </remarks>
	public static ImmutableDictionary<string, Security> WellKnown { get; } = typeof(Security)
		.GetFields(BindingFlags.Static | BindingFlags.Public)
		.Where(f => f.FieldType == typeof(Security))
		.ToImmutableDictionary(f => ((Security)f.GetValue(null)!).TickerSymbol, f => (Security)f.GetValue(null)!, StringComparer.OrdinalIgnoreCase);

	private string DebuggerDisplay => $"{this.Name} ({this.TickerSymbol})";

	/// <summary>
	/// Describes a raw amount as an amount of this security.
	/// </summary>
	/// <param name="amount">The amount of this security to represent.</param>
	/// <returns>The amount and units together.</returns>
	public SecurityAmount Amount(decimal amount) => new SecurityAmount(amount, this);
}
