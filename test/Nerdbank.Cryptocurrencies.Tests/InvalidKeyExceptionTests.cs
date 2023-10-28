// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class InvalidKeyExceptionTests
{
	[Fact]
	public void Constructor_Default_NoException()
	{
		// Act
		var exception = new InvalidKeyException();

		// Assert
		Assert.NotNull(exception);
	}

	[Fact]
	public void Constructor_WithMessage_NoException()
	{
		// Arrange
		var message = "Test message";

		// Act
		var exception = new InvalidKeyException(message);

		// Assert
		Assert.NotNull(exception);
		Assert.Equal(message, exception.Message);
	}

	[Fact]
	public void Constructor_WithInnerException_NoException()
	{
		// Arrange
		var innerException = new InvalidOperationException();

		// Act
		var exception = new InvalidKeyException("Test message", innerException);

		// Assert
		Assert.NotNull(exception);
		Assert.Equal("Test message", exception.Message);
		Assert.Equal(innerException, exception.InnerException);
	}

	[Fact]
	public void KeyPath_GetSet_KeyPathValue()
	{
		// Arrange
		var keyPath = new Bip32KeyPath(0);
		var exception = new InvalidKeyException() { KeyPath = keyPath };

		// Assert
		Assert.Equal(keyPath, exception.KeyPath);
	}
}
