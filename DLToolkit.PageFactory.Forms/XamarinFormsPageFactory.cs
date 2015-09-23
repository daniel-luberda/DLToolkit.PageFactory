﻿using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DLToolkit.PageFactory
{
	public class XamarinFormsPageFactory : IPageFactory
	{
		readonly Dictionary<Type, IBasePage<INotifyPropertyChanged>> pageCache = new Dictionary<Type, IBasePage<INotifyPropertyChanged>>();

		readonly Dictionary<Type, Type> viewModelsTypes = new Dictionary<Type, Type>();

		readonly ConditionalWeakTable<INotifyPropertyChanged, IBasePage<INotifyPropertyChanged>> weakPageCache = new ConditionalWeakTable<INotifyPropertyChanged, IBasePage<INotifyPropertyChanged>>();

		public void AddToWeakCacheIfNotExists(IBasePage<INotifyPropertyChanged> page)
		{
			if (page.ViewModel == null)
				return;

			IBasePage<INotifyPropertyChanged> weakExists;

			if (!weakPageCache.TryGetValue(page.ViewModel, out weakExists))
			{
				weakPageCache.Add(page.ViewModel, page);
			}
		}

		public void RemoveFromWeakCacheIfExists(IBasePage<INotifyPropertyChanged> page)
		{
			if (page.ViewModel == null)
				return;
			
			IBasePage<INotifyPropertyChanged> weakExists;

			if (weakPageCache.TryGetValue(page.ViewModel, out weakExists))
			{
				weakPageCache.Remove(page.ViewModel);
			}
		}
			
		public NavigationPage Init<TMainPageViewModel, TNavigationPage>(params Assembly[] additionalPagesAssemblies) where TMainPageViewModel : class, INotifyPropertyChanged where TNavigationPage : PFNavigationPage
		{
			PF.SetPageFactory(this);

			viewModelsTypes.Clear();

			var pagesAssemblies = additionalPagesAssemblies.ToList();
			pagesAssemblies.Add(typeof(TMainPageViewModel).GetTypeInfo().Assembly);
			pagesAssemblies.Add(typeof(TNavigationPage).GetTypeInfo().Assembly);

			foreach (var assembly in pagesAssemblies.Distinct())
			{
				foreach(var typeInfo in assembly.DefinedTypes.Where(t => t.IsClass && !t.IsAbstract && t.ImplementedInterfaces != null))
				{
					var found = typeInfo.ImplementedInterfaces.FirstOrDefault(t => t.IsConstructedGenericType && 
						t.GetGenericTypeDefinition() == typeof(IBasePage<>));

					if (found != default(Type))
					{
						var viewModelType = found.GenericTypeArguments.First();
						var pageType = typeInfo.AsType();

						if(!viewModelsTypes.ContainsKey(viewModelType))
						{
							var parameterlessCtors =
								(viewModelType.GetTypeInfo().DeclaredConstructors.Where(c => c.IsPublic && c.GetParameters().Length == 0));

							if (!parameterlessCtors.Any())
							{
								throw new ArgumentException(string.Format("ViewModel '{0}' has no public parameterless constructor", viewModelType));
							}

							viewModelsTypes.Add(viewModelType, pageType);
						}
						else
						{
							throw new ArgumentOutOfRangeException(
								string.Format("ViewModel {0} has multiple Page definitions", viewModelType.ToString()));
						}
					}
				}	
			}

			var page = GetPageFromCache(typeof(TMainPageViewModel));
			navigationPage = (PFNavigationPage)Activator.CreateInstance(typeof(TNavigationPage), page, true);

			return NavigationPage;
		}

		PFNavigationPage navigationPage = null;
		public PFNavigationPage NavigationPage
		{
			get
			{
				if (navigationPage == null)
					throw new NullReferenceException("NavigationPage is null. Please set NavigationPage with Init method");

				return navigationPage;	
			}
		}

		Type GetPageType(Type viewModelType)
		{
			Type pageType;

			if (viewModelsTypes.TryGetValue(viewModelType, out pageType))
			{
				return pageType;
			}

			throw new KeyNotFoundException(
				string.Format("Page definition for {0} ViewModel could not be found", viewModelType.ToString()));
		}

//		void CheckViewModelType(Type viewModelType, bool messagable = false)
//		{
//			if (!viewModelType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(INotifyPropertyChanged)))
//			{
//				throw new InvalidCastException("ViewModel must implement INotifyPropertyChanged interface");
//			}
//		}
//
//		void CheckMessagableViewModelType(Type viewModelType)
//		{
//			if (!viewModelType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IBaseMessagable)))
//			{
//				throw new InvalidCastException("Messagable ViewModel must implement IBaseMessagable interface");
//			}
//		}
			
		#region IPageFactory implementation

		public IBasePage<INotifyPropertyChanged> GetPageByViewModel<TViewModel>(TViewModel viewModelInstance) where TViewModel : class, INotifyPropertyChanged, new()
		{
			IBasePage<INotifyPropertyChanged> page;

			if (weakPageCache.TryGetValue(viewModelInstance, out page))
			{
				return page;
			}

			return null;
		}

		public void ReplacePageViewModel<TViewModel>(IBasePage<INotifyPropertyChanged> page, TViewModel newViewModel) where TViewModel : class, INotifyPropertyChanged
		{
			RemoveFromWeakCacheIfExists(page);	
			((Page)page).BindingContext = newViewModel;
			AddToWeakCacheIfNotExists(page);
		}

		public void ResetPageViewModel(IBasePage<INotifyPropertyChanged> page)
		{
			var viewModelType = page.ViewModel.GetType();
			INotifyPropertyChanged newViewModel = (INotifyPropertyChanged)Activator.CreateInstance(viewModelType);
			ReplacePageViewModel(page, newViewModel);
		}

		#endregion

		#region IPageFactoryCaching implementation

		public IBasePage<INotifyPropertyChanged> GetPageFromCache<TViewModel>(bool resetViewModel = false) where TViewModel : class, INotifyPropertyChanged, new()
		{
			return GetPageFromCache(typeof(TViewModel), resetViewModel);
		}

		public IBasePage<INotifyPropertyChanged> GetPageFromCache(Type viewModelType, bool resetViewModel = false)
		{
			var pageType = GetPageType(viewModelType);

			if (!pageCache.ContainsKey(viewModelType))
			{
				IBasePage<INotifyPropertyChanged> page = Activator.CreateInstance(pageType) as IBasePage<INotifyPropertyChanged>;
				pageCache.Add(viewModelType, page);
			}

			if (resetViewModel)
			{
				ResetPageViewModel(pageCache[viewModelType]);
			}

			return pageCache[viewModelType];
		}

		public IBasePage<INotifyPropertyChanged> GetPageAsNewInstance<TViewModel>(bool saveOrReplaceInCache = false) where TViewModel : class, INotifyPropertyChanged, new()
		{
			return GetPageAsNewInstance(typeof(TViewModel), saveOrReplaceInCache);
		}

		public IBasePage<INotifyPropertyChanged> GetPageAsNewInstance(Type viewModelType, bool saveOrReplaceInCache = false)
		{
			var pageType = GetPageType(viewModelType);

			IBasePage<INotifyPropertyChanged> page = (IBasePage<INotifyPropertyChanged>)Activator.CreateInstance(pageType);

			if (saveOrReplaceInCache && pageCache.ContainsKey(viewModelType))
			{
				pageCache.Remove(viewModelType);
				pageCache.Add(viewModelType, page);
			}

			return page;
		}

		public bool ReplaceCachedPageViewModel<TViewModel>(TViewModel newViewModel) where TViewModel : class, INotifyPropertyChanged, new()
		{
			if (pageCache.ContainsKey(typeof(TViewModel)))
			{
				var page = GetPageFromCache<TViewModel>();
				ReplacePageViewModel(page, newViewModel);
				return true;
			}

			return false;
		}

		public bool ResetCachedPageViewModel<TViewModel>() where TViewModel : class, INotifyPropertyChanged, new()
		{
			if (pageCache.ContainsKey(typeof(TViewModel)))
			{
				var page = GetPageFromCache<TViewModel>();
				ResetPageViewModel(page);
				return true;
			}

			return false;
		}

		public bool RemovePageTypeFromCache(Type viewModelType)
		{
			IBasePage<INotifyPropertyChanged> page;

			if (pageCache.TryGetValue(viewModelType, out page))
			{
				page.PageFactoryRemovingFromCache();
				pageCache.Remove(viewModelType);
				return true;
			}

			return false;
		}

		public bool RemovePageTypeFromCache<TViewModel>() where TViewModel : class, INotifyPropertyChanged, new()
		{
			return RemovePageTypeFromCache(typeof(TViewModel));
		}

		public bool RemovePageInstanceFromCache(IBasePage<INotifyPropertyChanged> page)
		{
			IBasePage<INotifyPropertyChanged> pageExists;

			if (pageCache.TryGetValue(page.ViewModel.GetType(), out pageExists))
			{
				if (pageExists == page)
				{
					page.PageFactoryRemovingFromCache();
					pageCache.Remove(page.ViewModel.GetType());
				}
					
				return true;
			}

			return false;
		}

		public void ClearCache()
		{
			pageCache.Clear();
		}

		#endregion

		#region IPageFactoryMessaging implementation

		public IBasePage<IBaseMessagable> GetMessagablePageFromCache<TViewModel>(bool resetViewModel = false) where TViewModel : class, IBaseMessagable, INotifyPropertyChanged, new()
		{
			return (IBasePage<IBaseMessagable>)GetPageFromCache<TViewModel>(resetViewModel);
		}

		public IBasePage<IBaseMessagable> GetMessagablePageFromCache(Type viewModelType, bool resetViewModel = false)
		{
			return (IBasePage<IBaseMessagable>)GetPageFromCache(viewModelType, resetViewModel);
		}

		public IBasePage<IBaseMessagable> GetMessagablePageAsNewInstance<TViewModel>(bool saveOrReplaceInCache = false) where TViewModel : class, IBaseMessagable, INotifyPropertyChanged, new()
		{
			return (IBasePage<IBaseMessagable>)GetPageAsNewInstance<TViewModel>(saveOrReplaceInCache);
		}

		public IBasePage<IBaseMessagable> GetMessagablePageAsNewInstance(Type viewModelType, bool saveOrReplaceInCache = false)
		{
			return (IBasePage<IBaseMessagable>)GetPageAsNewInstance(viewModelType, saveOrReplaceInCache);
		}

		public IBasePage<IBaseMessagable> GetMessagablePageByViewModel<TViewModel>(TViewModel viewModelInstance) where TViewModel : class, IBaseMessagable, INotifyPropertyChanged, new()
		{
			return (IBasePage<IBaseMessagable>)GetPageByViewModel<TViewModel>(viewModelInstance);
		}

		public bool SendMessageToPage<TPage>(TPage page, string message, object sender = null, object arg = null) where TPage : class, IBasePage<INotifyPropertyChanged>
		{
			if (page != null)
			{
				page.PageFactoryMessageReceived(message, sender, arg);
				return true;
			}

			return false;
		}

		public bool SendMessageByPage<TPage>(MessageConsumer consumer, TPage page, string message, object sender = null, object arg = null) where TPage : class, IBasePage<IBaseMessagable>
		{
			if (page != null)
			{
				if (consumer == MessageConsumer.Page || consumer == MessageConsumer.PageAndViewModel)
				{
					page.PageFactoryMessageReceived(message, sender, arg);
				}
				if (consumer == MessageConsumer.ViewModel || consumer == MessageConsumer.PageAndViewModel)
				{
					page.ViewModel.PageFactoryMessageReceived(message, sender, arg);
				}

				return true;
			}

			return false;
		}

		public bool SendMessageByViewModel<TViewModel>(MessageConsumer consumer, TViewModel viewModelInstance, string message, object sender = null, object arg = null) where TViewModel : class, INotifyPropertyChanged, IBaseMessagable, new()
		{
			var page = GetMessagablePageByViewModel(viewModelInstance);

			if (page != null)
			{
				if (consumer == MessageConsumer.Page || consumer == MessageConsumer.PageAndViewModel)
				{
					page.PageFactoryMessageReceived(message, sender, arg);
				}
				if (consumer == MessageConsumer.ViewModel || consumer == MessageConsumer.PageAndViewModel)
				{
					page.ViewModel.PageFactoryMessageReceived(message, sender, arg);
				}

				return true;
			}

			return false;
		}

		public bool SendMessageToCached<TViewModel>(MessageConsumer consumer, string message, object sender = null, object arg = null, bool createPageIfNotExists = true) where TViewModel : class, INotifyPropertyChanged, IBaseMessagable, new()
		{
			if (pageCache.ContainsKey(typeof(TViewModel)) || createPageIfNotExists)
			{
				var page = GetMessagablePageFromCache<TViewModel>(false);

				if (consumer == MessageConsumer.Page || consumer == MessageConsumer.PageAndViewModel)
				{
					page.PageFactoryMessageReceived(message, sender, arg);
				}
				if (consumer == MessageConsumer.ViewModel || consumer == MessageConsumer.PageAndViewModel)
				{
					page.ViewModel.PageFactoryMessageReceived(message, sender, arg);
				}

				return true;
			}

			return false;
		}

		#endregion

		#region IPageFactoryNavigation implementation

		public async Task<bool> PushPageAsync(IBasePage<INotifyPropertyChanged> page, bool animated = true)
		{
			if (!page.PageFactoryPushing())
				return false;

			await NavigationPage.Navigation.PushAsync((Page)page, animated);

			page.PageFactoryPushed();

			return true;
		}

		public async Task<bool> PushModalPageAsync(IBasePage<INotifyPropertyChanged> page, bool animated = true)
		{
			if (!page.PageFactoryPushing())
				return false;

			await NavigationPage.Navigation.PushModalAsync((Page)page, animated);

			page.PageFactoryPushed();

			return true;
		}

		public async Task<bool> PushPageFromCacheAsync<TViewModel>(bool resetViewModel = false, bool animated = true) where TViewModel : class, INotifyPropertyChanged, new()
		{
			var page = GetPageFromCache<TViewModel>(resetViewModel);
			return await PushPageAsync(page, animated);
		}

		public async Task<bool> PushModalPageFromCacheAsync<TViewModel>(bool resetViewModel = false, bool animated = true) where TViewModel : class, INotifyPropertyChanged, new()
		{
			var page = GetPageFromCache<TViewModel>(resetViewModel);
			return await PushModalPageAsync(page, animated);
		}
			
		public async Task<bool> PushPageAsNewAsync<TViewModel>(bool saveOrReplaceInCache = false, bool animated = true) where TViewModel : class, INotifyPropertyChanged, new()
		{
			var page = GetPageAsNewInstance<TViewModel>(saveOrReplaceInCache);
			return await PushPageAsync(page, animated);
		}

		public async Task<bool> PushModalPageAsNewAsync<TViewModel>(bool saveOrReplaceInCache = false, bool animated = true) where TViewModel : class, INotifyPropertyChanged, new()
		{
			var page = GetPageAsNewInstance<TViewModel>(saveOrReplaceInCache);
			return await PushModalPageAsync(page, animated);
		}

		public bool InsertPageBefore(IBasePage<INotifyPropertyChanged> page, IBasePage<INotifyPropertyChanged> before)
		{
			if (!page.PageFactoryInserting())
				return false;

			NavigationPage.Navigation.InsertPageBefore((Page)page, (Page)before);
			page.PageFactoryInserted();

			return true;
		}

		public bool InsertPageBeforeFromCache<TViewModel, TBeforeViewModel>(bool resetViewModel = false) where TViewModel : class, INotifyPropertyChanged, new() where TBeforeViewModel : class, INotifyPropertyChanged, new()
		{
			var page = GetPageFromCache<TViewModel>(resetViewModel);
			var before = GetPageFromCache<TBeforeViewModel>(false);

			return InsertPageBefore(page, before);
		}

		public bool InsertPageBeforeAsNew<TViewModel, TBeforeViewModel>(bool saveOrReplaceInCache = false) where TViewModel : class, INotifyPropertyChanged, new() where TBeforeViewModel : class, INotifyPropertyChanged, new()
		{
			var page = GetPageAsNewInstance<TViewModel>(saveOrReplaceInCache);
			var before = GetPageFromCache<TBeforeViewModel>(false);

			return InsertPageBefore(page, before);
		}

		public async Task<bool> PopPageAsync(bool resetViewModel = false, bool animated = true)
		{
			var page = NavigationPage.Navigation.NavigationStack.LastOrDefault() as IBasePage<INotifyPropertyChanged>;

			if (page != null && page.PageFactoryPopping())
			{
				if (resetViewModel)
					ResetPageViewModel(page);

				await NavigationPage.Navigation.PopAsync(animated);	
				page.PageFactoryPopped();

				return true;
			}

			return false;
		}

		public async Task<bool> PopModalPageAsync(bool resetViewModel = false, bool animated = true)
		{
			var page = NavigationPage.Navigation.ModalStack.LastOrDefault() as IBasePage<INotifyPropertyChanged>;

			if (page != null && page.PageFactoryPopping())
			{
				if (resetViewModel)
					ResetPageViewModel(page);

				await NavigationPage.Navigation.PopModalAsync(animated);	
				page.PageFactoryPopped();

				return true;
			}

			return false;
		}

		public bool RemovePage(IBasePage<INotifyPropertyChanged> pageToRemove)
		{
			var exists = NavigationPage.Navigation.NavigationStack.Contains((Page)pageToRemove);

			if (exists && pageToRemove.PageFactoryRemoving())
			{
				NavigationPage.Navigation.RemovePage((Page)pageToRemove);
				pageToRemove.PageFactoryRemoved();
			}

			return false;
		}

		public bool RemoveCachedPage<TViewModel>(bool removeFromCache = false, bool resetViewModel = false) where TViewModel : class, INotifyPropertyChanged, new()
		{
			if (pageCache.ContainsKey(typeof(TViewModel)))
			{
				var page = GetPageFromCache<TViewModel>(resetViewModel);

				if (removeFromCache)
					RemovePageTypeFromCache<TViewModel>();

				return RemovePage(page);
			}

			return false;
		}

		public async Task PopPagesToRootAsync(bool clearCache = false, bool animated = true)
		{
			await NavigationPage.Navigation.PopToRootAsync(animated);

			if (clearCache)
			{
				ClearCache();
			}
		}

		public void SetNewRootAndReset<TViewModel>() where TViewModel : class, INotifyPropertyChanged, new()
		{
			ClearCache();
			var page = GetPageAsNewInstance<TViewModel>(true);
			var navPageType = NavigationPage.GetType();
			navigationPage = (PFNavigationPage)Activator.CreateInstance(navPageType, page, true);

			Application.Current.MainPage = NavigationPage;	
		}

		#endregion
	}
}

