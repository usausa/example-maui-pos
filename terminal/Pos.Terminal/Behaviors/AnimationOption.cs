namespace Pos.Terminal.Behaviors;

public enum EnterAnimationType
{
    None,
    FadeUp
}

// 注意を引く小さな動き。Pulse は表示中ずっと 5% の拡縮を繰り返し、Highlight は値が変わったときだけ背景を光らせて元に戻す。
// Enter は面や一覧が現れるときに一度だけ (遅延をずらすと順に) 浮き上がらせる
public static class AnimationOption
{
    private const string PulseAnimationName = "AnimationOptionPulse";

    private const string HighlightAnimationName = "AnimationOptionHighlight";

    private const string EnterAnimationName = "AnimationOptionEnter";

    //--------------------------------------------------------------------------------
    // Pulse
    //--------------------------------------------------------------------------------

    public static readonly BindableProperty PulseProperty = BindableProperty.CreateAttached(
        "Pulse",
        typeof(bool),
        typeof(AnimationOption),
        false,
        propertyChanged: OnPulseChanged);

    public static bool GetPulse(BindableObject bindable) => (bool)bindable.GetValue(PulseProperty);

    public static void SetPulse(BindableObject bindable, bool value) => bindable.SetValue(PulseProperty, value);

    private static void OnPulseChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement element)
        {
            return;
        }

        if ((bool)newValue)
        {
            element.Loaded += OnPulseLoaded;
            element.Unloaded += OnPulseUnloaded;
            if (element.IsLoaded)
            {
                StartPulse(element);
            }
        }
        else
        {
            element.Loaded -= OnPulseLoaded;
            element.Unloaded -= OnPulseUnloaded;
            element.AbortAnimation(PulseAnimationName);
            element.Scale = 1.0;
        }
    }

    private static void OnPulseLoaded(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            StartPulse(element);
        }
    }

    private static void OnPulseUnloaded(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            element.AbortAnimation(PulseAnimationName);
        }
    }

    private static void StartPulse(VisualElement element)
    {
        element.AbortAnimation(PulseAnimationName);

        // 正弦カーブで 1.0 → 1.05 → 1.0 を繰り返す
        element.Animate(
            PulseAnimationName,
            v => element.Scale = 1.0 + (0.05 * Math.Sin(v * Math.PI)),
            16,
            3000,
            Easing.Linear,
            repeat: () => GetPulse(element));
    }

    //--------------------------------------------------------------------------------
    // Highlight
    //--------------------------------------------------------------------------------

    public static readonly BindableProperty HighlightTriggerProperty = BindableProperty.CreateAttached(
        "HighlightTrigger",
        typeof(object),
        typeof(AnimationOption),
        null,
        propertyChanged: OnHighlightTriggerChanged);

    public static object? GetHighlightTrigger(BindableObject bindable) => bindable.GetValue(HighlightTriggerProperty);

    public static void SetHighlightTrigger(BindableObject bindable, object? value) => bindable.SetValue(HighlightTriggerProperty, value);

    public static readonly BindableProperty HighlightColorProperty = BindableProperty.CreateAttached(
        "HighlightColor",
        typeof(Color),
        typeof(AnimationOption),
        null);

    public static Color? GetHighlightColor(BindableObject bindable) => (Color?)bindable.GetValue(HighlightColorProperty);

    public static void SetHighlightColor(BindableObject bindable, Color? value) => bindable.SetValue(HighlightColorProperty, value);

    private static void OnHighlightTriggerChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        // 最初のバインドでは光らせない (値が変わったときだけ)
        if ((bindable is not VisualElement element) || (oldValue is null) || (newValue is null))
        {
            return;
        }

        var highlight = GetHighlightColor(bindable);
        if (highlight is null)
        {
            return;
        }

        var original = element.BackgroundColor ?? Colors.Transparent;

        element.AbortAnimation(HighlightAnimationName);
        element.Animate(
            HighlightAnimationName,
            v => element.BackgroundColor = LerpColor(highlight, original, v),
            16,
            600,
            Easing.CubicOut,
            (_, _) => element.BackgroundColor = original);
    }

    private static Color LerpColor(Color from, Color to, double t) =>
        new(
            (float)(from.Red + ((to.Red - from.Red) * t)),
            (float)(from.Green + ((to.Green - from.Green) * t)),
            (float)(from.Blue + ((to.Blue - from.Blue) * t)),
            (float)(from.Alpha + ((to.Alpha - from.Alpha) * t)));

    //--------------------------------------------------------------------------------
    // Enter
    //--------------------------------------------------------------------------------

    public static readonly BindableProperty EnterAnimationProperty = BindableProperty.CreateAttached(
        "EnterAnimation",
        typeof(EnterAnimationType),
        typeof(AnimationOption),
        EnterAnimationType.None,
        propertyChanged: OnEnterAnimationChanged);

    public static EnterAnimationType GetEnterAnimation(BindableObject bindable) => (EnterAnimationType)bindable.GetValue(EnterAnimationProperty);

    public static void SetEnterAnimation(BindableObject bindable, EnterAnimationType value) => bindable.SetValue(EnterAnimationProperty, value);

    public static readonly BindableProperty EnterDelayProperty = BindableProperty.CreateAttached(
        "EnterDelay",
        typeof(int),
        typeof(AnimationOption),
        0);

    public static int GetEnterDelay(BindableObject bindable) => (int)bindable.GetValue(EnterDelayProperty);

    public static void SetEnterDelay(BindableObject bindable, int value) => bindable.SetValue(EnterDelayProperty, value);

    // 表示中に中身が入れ替わったとき (一覧の読み込み) にも再生する
    public static readonly BindableProperty EnterTriggerProperty = BindableProperty.CreateAttached(
        "EnterTrigger",
        typeof(object),
        typeof(AnimationOption),
        null,
        propertyChanged: OnEnterTriggerChanged);

    public static object? GetEnterTrigger(BindableObject bindable) => bindable.GetValue(EnterTriggerProperty);

    public static void SetEnterTrigger(BindableObject bindable, object? value) => bindable.SetValue(EnterTriggerProperty, value);

    private static void OnEnterAnimationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement element)
        {
            return;
        }

        if ((EnterAnimationType)newValue != EnterAnimationType.None)
        {
            element.Loaded += OnEnterLoaded;
            element.Unloaded += OnEnterUnloaded;
            if (element.IsLoaded)
            {
                PrepareEnter(element);
                StartEnterDelayed(element);
            }
        }
        else
        {
            element.Loaded -= OnEnterLoaded;
            element.Unloaded -= OnEnterUnloaded;
            element.AbortAnimation(EnterAnimationName);
            ResetEnter(element);
        }
    }

    private static void OnEnterTriggerChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        // 最初のバインドでは再生しない (表示時の再生は Loaded で行う)
        if ((bindable is not VisualElement element) || (oldValue is null) || (newValue is null))
        {
            return;
        }

        if ((GetEnterAnimation(element) == EnterAnimationType.None) || !element.IsLoaded)
        {
            return;
        }

        PrepareEnter(element);
        StartEnterDelayed(element);
    }

    private static void OnEnterLoaded(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            // 遅延の間に見えないように先に初期状態にする
            PrepareEnter(element);
            StartEnterDelayed(element);
        }
    }

    private static void OnEnterUnloaded(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            element.AbortAnimation(EnterAnimationName);
            ResetEnter(element);
        }
    }

    private static void PrepareEnter(VisualElement element)
    {
        element.AbortAnimation(EnterAnimationName);
        element.Opacity = 0;
        element.TranslationY = 16;
    }

    private static void StartEnterDelayed(VisualElement element)
    {
        var delay = GetEnterDelay(element);
        if (delay > 0)
        {
            element.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), () =>
            {
                if (element.IsLoaded && (GetEnterAnimation(element) != EnterAnimationType.None))
                {
                    StartEnter(element);
                }
            });
        }
        else
        {
            StartEnter(element);
        }
    }

    private static void StartEnter(VisualElement element)
    {
        element.AbortAnimation(EnterAnimationName);

        // 下から浮き上がりながら現れる
        element.Animate(
            EnterAnimationName,
            v =>
            {
                element.Opacity = v;
                element.TranslationY = 16 * (1 - v);
            },
            16,
            250,
            Easing.CubicOut,
            (_, _) => ResetEnter(element));
    }

    private static void ResetEnter(VisualElement element)
    {
        element.Opacity = 1;
        element.TranslationY = 0;
    }
}
