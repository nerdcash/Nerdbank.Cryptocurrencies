// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A store of URLs to well-known lightwalletd servers.
/// </summary>
/// <remarks>
/// This class's only stable API is its <see cref="GetDefaultServer(ZcashNetwork)"/> method.
/// All nested classes may be removed or changed at any time depending on availability, reliability,
/// public sentiment, and other factors.
/// </remarks>
public static class LightWalletServers
{
	/// <summary>
	/// Gets a reasonable, publicly accessible lightwalletd server to connect to for a given network.
	/// </summary>
	/// <param name="network">The Zcash network that the server must operate on.</param>
	/// <returns>The Uri for the server.</returns>
	/// <remarks>
	/// For a robust Zcash application, you should research all the public lightwalletd servers available,
	/// considering their performance, up-time, and geographic location.
	/// You may also consider running your own lightwalletd server.
	/// Use your selected server directly rather than using this method, which is meant for convenience only
	/// to get started.
	/// </remarks>
	public static Uri GetDefaultServer(ZcashNetwork network) =>
		network switch
		{
			ZcashNetwork.MainNet => Nerdbank.MainNet,
			ZcashNetwork.TestNet => Nerdbank.TestNet,
			_ => throw new ArgumentOutOfRangeException(nameof(network)),
		};

	/// <summary>
	/// Servers hosted by the same authors as this library.
	/// </summary>
	public static class Nerdbank
	{
		/// <summary>
		/// Gets the URI to the MainNet server.
		/// </summary>
		public static readonly Uri MainNet = new("https://zcash.mysideoftheweb.com:9067/");

		/// <summary>
		/// Gets the URI to the TestNet server.
		/// </summary>
		public static readonly Uri TestNet = new("https://zcash.mysideoftheweb.com:19067/");
	}

	/// <summary>
	/// Servers hosted by Hanh, as discoverable at <see href="https://status.zcash-infra.com/">this status page</see>.
	/// </summary>
	public static class Hanh
	{
		private static readonly Uri[] Servers = Enumerable.Range(1, 8).Select(n => new Uri($"https://lwd{n}.zcash-infra.com:9067/")).ToArray();

		/// <summary>
		/// Gets the URI to one of the MainNet servers hosted by Hanh.
		/// </summary>
		public static Uri MainNet => Servers[Random.Shared.Next(Servers.Length)];
	}
}
