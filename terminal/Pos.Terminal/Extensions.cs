namespace Pos.Terminal;

using System.Reflection;

#pragma warning disable CA1724
public static class Extensions
{
    //--------------------------------------------------------------------------------
    // Resource
    //--------------------------------------------------------------------------------

    public static T FindResource<T>(this ResourceDictionary resource, string key) =>
        resource.TryGetValue(key, out var value) ? (T)value : default!;

    public static IEnumerable<(string Key, T Value)> EnumValues<T>(this ResourceDictionary resource)
    {
        if (resource is { } resources)
        {
            foreach (var key in resources.Keys)
            {
                if (resources[key] is T value)
                {
                    yield return (key, value);
                }
            }

            if (resources.MergedDictionaries is not null)
            {
                foreach (var dictionary in resources.MergedDictionaries)
                {
                    foreach (var key in dictionary.Keys)
                    {
                        if (resources[key] is T value)
                        {
                            yield return (key, value);
                        }
                    }
                }
            }
        }
    }

    public static IEnumerable<Type> UnderNamespaceTypes(this Assembly assembly, Type baseNamespaceType)
    {
        var ns = baseNamespaceType.Namespace!;
        return assembly.ExportedTypes.Where(x => x.Namespace?.StartsWith(ns, StringComparison.Ordinal) ?? false);
    }

    //--------------------------------------------------------------------------------
    // Dialog (日本語の既定ボタン)
    //--------------------------------------------------------------------------------

    public static ValueTask<bool> AskAsync(this IDialog dialog, string message, string? title = null, string ok = "OK") =>
        dialog.ConfirmAsync(message, title, ok, "キャンセル");

    public static ValueTask<int> ChooseAsync(this IDialog dialog, string[] items, string? title = null, int selected = -1) =>
        dialog.SelectAsync(items, selected, title, "キャンセル");

    public static ValueTask<T?> ChooseAsync<T>(this IDialog dialog, IList<T> items, Func<T, string> formatter, string? title = null, int selected = -1) =>
        dialog.SelectAsync(items, formatter, selected, title, "キャンセル");

    public static ValueTask<PromptResult> InputAsync(this IDialog dialog, string title, string? defaultValue = null, string? placeHolder = null, PromptParameter? parameter = null) =>
        dialog.PromptAsync(defaultValue, null, title, "OK", "キャンセル", placeHolder, parameter);

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    public static async ValueTask PostForwardAsync(this INavigator navigator, object viewId, NavigationParameter? parameter = null)
    {
        if (navigator.Executing)
        {
            // ReSharper disable once AsyncVoidEventHandlerMethod
            async void ExecutingChanged(object? sender, EventArgs args)
            {
                if (!navigator.Executing)
                {
                    navigator.ExecutingChanged -= ExecutingChanged;
                    await navigator.ForwardAsync(viewId, parameter);
                }
            }

            navigator.ExecutingChanged += ExecutingChanged;
        }
        else
        {
            await navigator.ForwardAsync(viewId, parameter);
        }
    }

    // ReSharper disable once AsyncVoidMethod
    public static async ValueTask PostActionAsync(this INavigator navigator, Func<Task> task)
    {
        if (navigator.Executing)
        {
            // ReSharper disable once AsyncVoidEventHandlerMethod
            async void ExecutingChanged(object? sender, EventArgs args)
            {
                if (!navigator.Executing)
                {
                    navigator.ExecutingChanged -= ExecutingChanged;
                    await task();
                }
            }

            navigator.ExecutingChanged += ExecutingChanged;
        }
        else
        {
            await task();
        }
    }

    //--------------------------------------------------------------------------------
    // Text
    //--------------------------------------------------------------------------------

    // 空白だけなら null、それ以外は前後の空白を除く
    public static string? TrimToNull(this string? value) =>
        String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    //--------------------------------------------------------------------------------
    // Collection
    //--------------------------------------------------------------------------------

    public static void Replace<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    //--------------------------------------------------------------------------------
    // Reactive
    //--------------------------------------------------------------------------------

    public static IObservable<EventArgs> TickAsObservable(this IDispatcherTimer timer) =>
        Observable.FromEvent<EventHandler, EventArgs>(static h => (_, e) => h(e), h => timer.Tick += h, h => timer.Tick -= h);

    public static IObservable<ScreenStateEventArgs> StateChangedAsObservable(this IScreen screen) =>
        Observable.FromEvent<EventHandler<ScreenStateEventArgs>, ScreenStateEventArgs>(static h => (_, e) => h(e), h => screen.ScreenStateChanged += h, h => screen.ScreenStateChanged -= h);
}
#pragma warning restore CA1724
