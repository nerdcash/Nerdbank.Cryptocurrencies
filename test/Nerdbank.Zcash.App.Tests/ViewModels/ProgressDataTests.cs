// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ProgressDataTests
{
	[Fact]
	public void VisiblyApparentStepSize_Default()
	{
		Assert.Null(new ProgressData().VisiblyApparentStepSize);
	}

	[Fact]
	public void ProgressTextFormat_NoDecimalPoint()
	{
		PrecisionProgressData progress = new()
		{
			To = 100_000,
		};
		Assert.Equal("{1:N0}%", progress.ProgressTextFormat);
		progress.To = 3;
		Assert.Equal("{1:N0}%", progress.ProgressTextFormat);
	}

	[Theory]
	[InlineData(10, 0, 0)]
	[InlineData(10_000, 7, 0)]
	[InlineData(10, 10, 0)]
	[InlineData(10, 100, 0)]
	[InlineData(10, 1_000, 0)]
	[InlineData(10, 10_000, 1)]
	[InlineData(10, 20_000, 2)]
	[InlineData(10, 99_999, 2)]
	[InlineData(10, 100_000, 2)]
	[InlineData(10, 100_001, 3)]
	public void ProgressTextFormat_HasDecimalPointAppropriately(uint stepSize, uint amount, uint precision)
	{
		const ulong startingPoint = 50_000;
		PrecisionProgressData progress = new()
		{
			StepSize = stepSize,
			From = startingPoint,
			To = startingPoint + amount,
		};

		string expected = $"{{1:N{precision}}}%";
		Assert.Equal(expected, progress.ProgressTextFormat);
	}

	[Fact]
	public void ProgressTextFormat_PropertyChanged()
	{
		PrecisionProgressData progress = new()
		{
			To = 1_000_000,
		};
		Assert.PropertyChanged(progress, nameof(ProgressData.ProgressTextFormat), () => progress.From = 1000);
		Assert.PropertyChanged(progress, nameof(ProgressData.ProgressTextFormat), () => progress.To *= 1000);
	}

	private class PrecisionProgressData : ProgressData
	{
		public override uint? VisiblyApparentStepSize => this.StepSize;

		internal uint StepSize { get; set; }
	}
}
