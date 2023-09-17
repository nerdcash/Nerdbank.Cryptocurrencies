// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Grpc.Core;
using Grpc.Net.Client;
using Lightwalletd;

namespace Nerdbank.Zcash;

/// <summary>
/// A managed implementation of a lightwallet client.
/// </summary>
public class ManagedLightWalletClient : IDisposable
{
	private readonly GrpcChannel grpcChannel;
	private readonly CompactTxStreamer.CompactTxStreamerClient client;

	/// <summary>
	/// Initializes a new instance of the <see cref="ManagedLightWalletClient"/> class.
	/// </summary>
	/// <param name="grpcChannel">The gRPC channel shared with the server.</param>
	/// <param name="network">The network the server operates on.</param>
	private ManagedLightWalletClient(GrpcChannel grpcChannel, ZcashNetwork network)
	{
		this.grpcChannel = grpcChannel;
		this.client = new(grpcChannel);
		this.Network = network;
	}

	/// <summary>
	/// Gets the Zcash network that this server operates on.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ManagedLightWalletClient"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of the lightwalletd server to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The constructed lightwallet client.</returns>
	/// <exception cref="NotSupportedException">Thrown if the server operates on an unrecognized chain.</exception>
	public static async ValueTask<ManagedLightWalletClient> CreateAsync(Uri serverUrl, CancellationToken cancellationToken)
	{
		GrpcChannel channel = GrpcChannel.ForAddress(serverUrl);
		try
		{
			try
			{
				CompactTxStreamer.CompactTxStreamerClient client = new(channel);
				LightdInfo info = await client.GetLightdInfoAsync(new Empty(), cancellationToken: cancellationToken);
				ZcashNetwork network = info.ChainName switch
				{
					"main" => ZcashNetwork.MainNet,
					"test" => ZcashNetwork.TestNet,
					_ => throw new NotSupportedException(Strings.FormatUnrecognizedNetwork(info.ChainName)),
				};
				return new(channel, network);
			}
			catch (RpcException ex)
			{
				throw new LightWalletException(Strings.ErrorInGetLightWalletServerInfo, ex);
			}
		}
		catch
		{
			channel.Dispose();
			throw;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.grpcChannel.Dispose();
	}

	/// <summary>
	/// Gets the blockchain length as tracked by the lightwallet server.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The blockchain length.</returns>
	/// <exception cref="LightWalletException">Thrown if an error occurs in communicating with the server.</exception>
	public async ValueTask<ulong> GetLatestBlockHeightAsync(CancellationToken cancellationToken)
	{
		try
		{
			LightdInfo info = await this.client.GetLightdInfoAsync(new Empty(), cancellationToken: cancellationToken);
			return info.BlockHeight;
		}
		catch (RpcException ex)
		{
			throw new LightWalletException(Strings.ErrorInGetLightWalletServerInfo, ex);
		}
	}
}
