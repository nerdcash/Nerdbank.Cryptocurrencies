// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;

namespace Nerdbank.Zcash.App.ViewModels;

public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo
{
	private readonly Dictionary<string, ValidationResult[]> errors = new(StringComparer.Ordinal);
	private bool hasErrors;

	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

	public bool HasErrors
	{
		get => this.hasErrors;
		private set => this.RaiseAndSetIfChanged(ref this.hasErrors, value);
	}

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
				this.OnErrorsChanged(new DataErrorsChangedEventArgs(propertyName));
			}
		}
		else
		{
			this.errors[propertyName] = new ValidationResult[] { new(message) };
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

	private protected static IDisposable WrapModels<TModelCollection, TModel, TViewModel>(TModelCollection models, ObservableCollection<TViewModel> viewModels, Func<TModel, TViewModel> wrapper)
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
}
