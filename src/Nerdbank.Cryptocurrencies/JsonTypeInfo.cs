// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Source generated JSON converters for NativeAOT safety.
/// </summary>
[JsonSourceGenerationOptions]
[JsonSerializable(typeof(Coinbase.ResponseItem[]))]
[JsonSerializable(typeof(Coinbase.Product[]))]
internal partial class JsonSerializationInfo : JsonSerializerContext;
