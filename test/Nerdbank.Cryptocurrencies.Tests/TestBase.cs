// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

public abstract class TestBase
{
	/// <summary>
	/// The maximum length of time to wait for something that we expect will happen
	/// within the timeout.
	/// </summary>
	protected static readonly TimeSpan UnexpectedTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10);

	/// <summary>
	/// The maximum length of time to wait for something that we do not expect will happen
	/// within the timeout.
	/// </summary>
	protected static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(2);

	public TestBase(ITestOutputHelper logger)
	{
		this.Logger = logger;
	}

	/// <summary>
	/// Gets or sets the source of <see cref="TimeoutToken"/> that influences
	/// when tests consider themselves to be timed out.
	/// </summary>
	protected CancellationTokenSource TimeoutTokenSource { get; set; } = new CancellationTokenSource(UnexpectedTimeout);

	/// <summary>
	/// Gets a token that is canceled when the test times out,
	/// per the policy set by <see cref="TimeoutTokenSource"/>.
	/// </summary>
	protected CancellationToken TimeoutToken => this.TimeoutTokenSource.Token;

	protected ITestOutputHelper Logger { get; }
}
