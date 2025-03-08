// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Describes details about a Zcash network.
/// </summary>
/// <remarks>
/// <para>Sources of data include:</para>
/// <list type="bullet">
/// <item><see href="https://github.com/zcash/zcash/pull/3469">Zcash PR #3469</see></item>
/// <item><see href="https://zips.z.cash/zip-0252">ZIP-252</see></item>
/// </list>
/// </remarks>
public class ZcashNetworkParameters
{
	private ZcashNetworkParameters(ZcashNetwork network)
	{
		this.Network = network;
	}

	/// <summary>
	/// Gets the main network parameters.
	/// </summary>
	public static ZcashNetworkParameters MainNet { get; } = new ZcashNetworkParameters(ZcashNetwork.MainNet)
	{
		SaplingActivationHeight = 419_200,
		OrchardActivationHeight = 1_687_104,
	};

	/// <summary>
	/// Gets the test network parameters.
	/// </summary>
	public static ZcashNetworkParameters TestNet { get; } = new ZcashNetworkParameters(ZcashNetwork.TestNet)
	{
		SaplingActivationHeight = 280_000,
		OrchardActivationHeight = 1_842_420,
	};

	/// <summary>
	/// Gets the network that these parameters are for.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the sapling activation height on this network.
	/// </summary>
	public required uint SaplingActivationHeight { get; init; }

	/// <summary>
	/// Gets the orchard activation height on this network.
	/// </summary>
	public required uint OrchardActivationHeight { get; init; }

	/// <summary>
	/// Gets the network parameters for the given network.
	/// </summary>
	/// <param name="network">The network to get parameters for.</param>
	/// <returns>The network parameters.</returns>
	public static ZcashNetworkParameters GetParameters(ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => MainNet,
			ZcashNetwork.TestNet => TestNet,
			_ => throw new ArgumentOutOfRangeException(nameof(network)),
		};
	}
}
