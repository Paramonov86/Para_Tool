using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ParaTool.App.Localization;

namespace ParaTool.App.Controls;

public partial class UpdateButton : UserControl
{
    public static readonly StyledProperty<UpdateState> StateProperty =
        AvaloniaProperty.Register<UpdateButton, UpdateState>(nameof(State));

    public static readonly StyledProperty<string?> UpdateVersionProperty =
        AvaloniaProperty.Register<UpdateButton, string?>(nameof(UpdateVersion));

    public static readonly StyledProperty<int> ProgressProperty =
        AvaloniaProperty.Register<UpdateButton, int>(nameof(Progress));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<UpdateButton, string?>(nameof(ErrorMessage));

    public UpdateState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string? UpdateVersion
    {
        get => GetValue(UpdateVersionProperty);
        set => SetValue(UpdateVersionProperty, value);
    }

    public int Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    private static readonly SolidColorBrush IdleBrush = new(Color.Parse("#888888"));
    private static readonly SolidColorBrush AvailableBrush = new(Color.Parse("#27AE60"));
    private static readonly SolidColorBrush SpinningBrush = new(Color.Parse("#6C5CE7"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#E74C3C"));

    public UpdateButton()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty ||
            change.Property == UpdateVersionProperty ||
            change.Property == ProgressProperty ||
            change.Property == ErrorMessageProperty)
        {
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        var icon = this.FindControl<Avalonia.Controls.Shapes.Path>("RefreshIcon");
        var badge = this.FindControl<Border>("VersionBadge");
        var versionText = this.FindControl<TextBlock>("VersionText");
        var progressText = this.FindControl<TextBlock>("ProgressText");
        var btn = this.FindControl<Button>("UpdateBtn");

        if (icon == null || badge == null || versionText == null || progressText == null || btn == null)
            return;

        // Reset
        badge.IsVisible = false;
        progressText.IsVisible = false;
        icon.Classes.Remove("spinning");

        switch (State)
        {
            case UpdateState.Idle:
                icon.Fill = IdleBrush;
                ToolTip.SetTip(btn, Loc.Instance.UpdateCheckTooltip);
                break;

            case UpdateState.Checking:
                icon.Fill = SpinningBrush;
                icon.Classes.Add("spinning");
                ToolTip.SetTip(btn, Loc.Instance.UpdateCheckingTooltip);
                break;

            case UpdateState.Available:
                icon.Fill = AvailableBrush;
                badge.IsVisible = true;
                versionText.Text = $"v{UpdateVersion}";
                ToolTip.SetTip(btn, Loc.Instance.UpdateAvailableTooltip(UpdateVersion ?? "?"));
                break;

            case UpdateState.Downloading:
                icon.Fill = SpinningBrush;
                icon.Classes.Add("spinning");
                progressText.IsVisible = true;
                progressText.Text = $"{Progress}%";
                ToolTip.SetTip(btn, Loc.Instance.UpdateDownloadingTooltip(Progress));
                break;

            case UpdateState.UpToDate:
                icon.Fill = IdleBrush;
                ToolTip.SetTip(btn, Loc.Instance.UpdateUpToDateTooltip);
                break;

            case UpdateState.Error:
                icon.Fill = ErrorBrush;
                ToolTip.SetTip(btn, ErrorMessage ?? Loc.Instance.UpdateFailedTooltip);
                break;
        }
    }
}

public enum UpdateState
{
    Idle,
    Checking,
    Available,
    Downloading,
    UpToDate,
    Error
}
