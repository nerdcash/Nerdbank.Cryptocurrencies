// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Nerdbank.Zcash.App;

/// <summary>
/// An <see cref="ILogger"/> that stores messages in memory for in-app presentation.
/// </summary>
public class MemoryLogger : ILogger
{
	private readonly AsyncLocal<Scope?> currentScope = new();

	private ImmutableList<LogEvent> messages = ImmutableList<LogEvent>.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryLogger"/> class.
	/// </summary>
	/// <param name="categoryName">A category for messages sent to this logger.</param>
	public MemoryLogger(string categoryName)
	{
		this.CategoryName = categoryName;
	}

	/// <summary>
	/// Gets the category name for which this logger was created.
	/// </summary>
	public string CategoryName { get; }

	/// <summary>
	/// Gets or sets the minimum log level for which messages will be stored.
	/// </summary>
	public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

	/// <summary>
	/// Gets the messages that have been logged to this logger so far.
	/// </summary>
	public ImmutableList<LogEvent> Messages => this.messages;

	/// <inheritdoc/>
	public bool IsEnabled(LogLevel logLevel) => logLevel >= this.MinimumLevel;

	/// <inheritdoc/>
	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull
	{
		return new ScopeWithState<TState>(this, state);
	}

	/// <inheritdoc/>
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!this.IsEnabled(logLevel))
		{
			return;
		}

		string message = formatter(state, exception);
		if (string.IsNullOrEmpty(message))
		{
			return;
		}

		LogEvent evt = new(logLevel, DateTimeOffset.Now, eventId, message, exception, this.currentScope.Value);

		ImmutableInterlocked.Update(ref this.messages, list => list.Add(evt));
	}

	/// <summary>
	/// Describes a scope that was begun with <see cref="BeginScope{TState}(TState)"/>.
	/// </summary>
	/// <remarks>
	/// The <c>TState</c> object is recorded in the derived <see cref="ScopeWithState{TState}"/> class.
	/// </remarks>
	public abstract class Scope : IDisposable
	{
		private readonly MemoryLogger logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="Scope"/> class.
		/// </summary>
		/// <param name="logger">The owner of this instance.</param>
		private protected Scope(MemoryLogger logger)
		{
			this.logger = logger;
			this.Parent = logger.currentScope.Value;
			logger.currentScope.Value = this;
		}

		/// <summary>
		/// Gets the parent scope, if any.
		/// </summary>
		public Scope? Parent { get; }

		public void Dispose()
		{
			this.logger.currentScope.Value = this.Parent;
		}

		/// <summary>
		/// Gets a string representation of the state associated with this scope.
		/// </summary>
		/// <returns>The string rendering of the state.</returns>
		internal abstract string? GetStateString();
	}

	public class ScopeWithState<TState> : Scope
		where TState : notnull
	{
		internal ScopeWithState(MemoryLogger logger, TState state)
			: base(logger)
		{
			this.State = state;
		}

		public TState State { get; }

		internal override string? GetStateString() => this.State.ToString();
	}

	public class Provider : ILoggerProvider
	{
		private readonly ConcurrentDictionary<string, ConcurrentBag<MemoryLogger>> loggersByCategory = new();

		/// <summary>
		/// Gets the categories for which loggers have been created.
		/// </summary>
		public ICollection<string> Categories => this.loggersByCategory.Keys;

		public ILogger CreateLogger(string categoryName)
		{
			MemoryLogger logger = new(categoryName);

			ConcurrentBag<MemoryLogger> loggers = this.loggersByCategory.GetOrAdd(categoryName, key => new());
			loggers.Add(logger);

			return logger;
		}

		/// <summary>
		/// Gets all the loggers created for a given category.
		/// </summary>
		/// <param name="categoryName">The category.</param>
		/// <returns>The loggers created for that category. May be empty.</returns>
		public IReadOnlyCollection<MemoryLogger> GetLoggers(string categoryName) =>
			this.loggersByCategory.TryGetValue(categoryName, out ConcurrentBag<MemoryLogger>? loggers)
				? loggers
				: ImmutableList<MemoryLogger>.Empty;

		public void Dispose()
		{
		}
	}
}

#pragma warning disable SA1402 // File may only contain a single type
public record LogEvent(LogLevel Level, DateTimeOffset Timestamp, EventId EventId, string Message, Exception? Exception, MemoryLogger.Scope? Scope);
