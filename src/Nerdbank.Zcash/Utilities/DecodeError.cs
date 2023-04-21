// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Utilities;

/// <summary>
/// The failure modes that may occur while decoding.
/// </summary>
internal enum DecodeError
{
    /// <summary>
    /// A disallowed character was found in the encoded string.
    /// </summary>
    InvalidCharacter,

    /// <summary>
    /// The checksum failed to match.
    /// </summary>
    InvalidChecksum,

    /// <summary>
    /// The buffer to decode into was too small.
    /// </summary>
    BufferTooSmall,
}
