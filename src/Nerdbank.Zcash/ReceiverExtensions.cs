// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// Extension methods on the <see cref="IPoolReceiver"/> structs.
/// </summary>
internal static class ReceiverExtensions
{
    /// <summary>
    /// Gets a span encompassing the entire receiver.
    /// </summary>
    /// <typeparam name="T">The type of the receiver.</typeparam>
    /// <param name="receiver">The receiver.</param>
    /// <returns>The byte span of the receiver.</returns>
    internal static Span<byte> GetSpan<T>(this ref T receiver)
        where T : unmanaged, IPoolReceiver
    {
        return MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref receiver, 1));
    }

    /// <summary>
    /// Gets a span encompassing the entire receiver.
    /// </summary>
    /// <typeparam name="T">The type of the receiver.</typeparam>
    /// <param name="receiver">The receiver.</param>
    /// <returns>The byte span of the receiver.</returns>
    internal static ReadOnlySpan<byte> GetReadOnlySpan<T>(this T receiver)
        where T : unmanaged, IPoolReceiver
    {
        return MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateReadOnlySpan(ref receiver, 1));
    }
}
