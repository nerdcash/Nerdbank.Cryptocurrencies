// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ViewModelBase : ReactiveObject, INotifyDataErrorInfo
{
	private readonly Dictionary<string, ValidationResult[]> errors = new(StringComparer.Ordinal);
	private readonly HashSet<string> propertiesWithAnnotationErrors = new(StringComparer.Ordinal);

	public ViewModelBase()
	{
		this.IsValid = this.WhenAnyValue(vm => vm.HasAnyErrors, hasErrors => !hasErrors);
		this.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName is null or nameof(this.HasAnyErrors))
			{
				return;
			}

			if (e.PropertyName is nameof(this.HasErrors))
			{
				this.RaisePropertyChanged(nameof(this.HasAnyErrors));
				return;
			}

			if (this.GetType().GetProperty(e.PropertyName, BindingFlags.Instance | BindingFlags.Public) is PropertyInfo property)
			{
				bool oldHasAnyErrorsValue = this.HasAnyErrors;
				if (this.CheckPropertyAnnotations(property) && oldHasAnyErrorsValue != this.HasAnyErrors)
				{
					this.RaisePropertyChanged(nameof(this.HasAnyErrors));
				}
			}
		};

		// Initialize default validity state.
		foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			this.CheckPropertyAnnotations(property);
		}
	}

	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

	public bool HasErrors => this.errors.Count > 0;

	/// <summary>
	/// Gets a value indicating whether this view model has any errors, including from validation attributes
	/// on its members.
	/// </summary>
	protected bool HasAnyErrors => this.HasErrors || this.propertiesWithAnnotationErrors.Count > 0;

	/// <summary>
	/// Gets an observable value indicating whether everything in this view model is valid.
	/// </summary>
	/// <remarks>
	/// This is always the inverse of <see cref="HasAnyErrors"/>.
	/// </remarks>
	protected IObservable<bool> IsValid { get; }

	public IEnumerable GetErrors(string? propertyName)
	{
		if (propertyName is not null && this.errors.TryGetValue(propertyName, out ValidationResult[]? results))
		{
			return results;
		}

		return Enumerable.Empty<ValidationResult>();
	}

	protected virtual void OnErrorsChanged(DataErrorsChangedEventArgs e) => this.ErrorsChanged?.Invoke(this, e);

	protected void RecordValidationError(string? message, [CallerMemberName] string? propertyName = null)
	{
		Requires.NotNull(propertyName!);
		if (message is null)
		{
			if (this.errors.Remove(propertyName))
			{
				this.RaisePropertyChanged(nameof(this.HasErrors));
				this.OnErrorsChanged(new DataErrorsChangedEventArgs(propertyName));
			}
		}
		else
		{
			this.errors[propertyName] = [new(message)];
			this.RaisePropertyChanged(nameof(this.HasErrors));
			this.OnErrorsChanged(new DataErrorsChangedEventArgs(propertyName));
		}
	}

	protected void LinkProperty(string basePropertyName, string dependentPropertyName)
	{
		this.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanged(dependentPropertyName);
			}
		};

		this.PropertyChanging += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanging(dependentPropertyName);
			}
		};
	}

	protected void LinkProperty(INotifyPropertyChanged model, string basePropertyName, string dependentPropertyName)
	{
		model.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanged(dependentPropertyName);
			}
		};
	}

	private protected static IDisposable WrapModels<TModelCollection, TModel, TViewModel>(TModelCollection models, IList<TViewModel> viewModels, Func<TModel, TViewModel> wrapper)
		where TModelCollection : IEnumerable<TModel>, INotifyCollectionChanged
		where TModel : class
		where TViewModel : class, IViewModel<TModel>
	{
		models.CollectionChanged += OnCollectionChanged;

		foreach (TModel model in models)
		{
			viewModels.Add(wrapper(model));
		}

		return Disposable.Create(() => models.CollectionChanged -= OnCollectionChanged);

		void OnCollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems is not null)
			{
				foreach (TModel model in e.NewItems)
				{
					viewModels.Add(wrapper(model));
				}
			}

			if (e.OldItems is not null)
			{
				foreach (TModel model in e.OldItems)
				{
					if (viewModels.FirstOrDefault(vm => EqualityComparer<TModel>.Default.Equals(vm.Model, model)) is TViewModel viewModel)
					{
						viewModels.Remove(viewModel);
					}
				}
			}
		}
	}

	private bool CheckPropertyAnnotations(PropertyInfo property)
	{
		ValidationContext context = new(this)
		{
			DisplayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name,
			MemberName = property.Name,
		};

		bool hasErrors = property.GetCustomAttributes<ValidationAttribute>(inherit: true)
			.Any(att => att.GetValidationResult(property.GetValue(this), context) is { ErrorMessage: not null });
		return hasErrors
			? this.propertiesWithAnnotationErrors.Add(property.Name)
			: this.propertiesWithAnnotationErrors.Remove(property.Name);
	}
}
