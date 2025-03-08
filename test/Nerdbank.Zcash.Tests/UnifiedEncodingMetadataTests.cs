// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class UnifiedEncodingMetadataTests
{
	[Fact]
	public void ExpirationDate_TruncatesToSecondPrecision()
	{
		DateTimeOffset secondPrecision = new(1971, 2, 3, 4, 5, 6, TimeSpan.Zero);
		DateTimeOffset msPrecision = secondPrecision.AddMilliseconds(5);
		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = msPrecision,
		};
		Assert.Equal(secondPrecision, metadata.ExpirationDate);
	}

	[Fact]
	public void Equality()
	{
		UnifiedEncodingMetadata metadata1a = new()
		{
			ExpirationDate = new(2021, 2, 3, 4, 5, 6, TimeSpan.Zero),
			ExpirationHeight = 123,
		};
		UnifiedEncodingMetadata metadata1b = new()
		{
			ExpirationDate = new(2021, 2, 3, 4, 5, 6, TimeSpan.Zero),
			ExpirationHeight = 123,
		};

		UnifiedEncodingMetadata metadata2 = new()
		{
			ExpirationDate = new(2022, 2, 3, 4, 5, 6, TimeSpan.Zero),
			ExpirationHeight = 123,
		};
		UnifiedEncodingMetadata metadata3 = new()
		{
			ExpirationDate = new(2021, 2, 3, 4, 5, 6, TimeSpan.Zero),
			ExpirationHeight = 124,
		};

		Assert.Equal(metadata1a, metadata1b);
		Assert.NotEqual(metadata1a, metadata2);
		Assert.NotEqual(metadata1a, metadata3);
	}
}
