using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using PetDude.Controls;
using PetDude.Models;
using PetDude.Services;
using PetDude.ViewModels;
using Forms = System.Windows.Forms;

namespace PetDude;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly MonitorService _monitorService = new();
    private readonly MouseTrackingService _mouseTrackingService = new();
    private readonly IdleDetectionService _idleDetectionService = new();
    private readonly StartupService _startupService = new();
    private readonly SystemStatsService _systemStatsService = new();
    private readonly PetViewModel _viewModel = new();
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _slowTimer;
    private readonly Random _random = new();
    private readonly List<PixelPetState> _pixelPets = [];
    private readonly List<FrameworkElement> _dynamicPetElements = [];

    private FullscreenDetectionService _fullscreenDetectionService = null!;
    private AppSettings _settings = null!;
    private Forms.NotifyIcon? _notifyIcon;
    private IntPtr _windowHandle;
    private DateTime _nextBlinkAt;
    private DateTime _blinkUntil;
    private DateTime _pokedUntil;
    private DateTime _nextMoveTargetAt;
    private DateTime _pauseUntil;
    private DateTime _hopUntil;
    private readonly AxisAngleRotation3D _pet3DYaw = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _pet3DPitch = new(new Vector3D(1, 0, 0), 0);
    private readonly AxisAngleRotation3D _pet3DRoll = new(new Vector3D(0, 0, 1), 0);
    private readonly TranslateTransform3D _pet3DBob = new();
    private readonly List<TranslateTransform3D> _pet3DEyeOffsets = [];
    private readonly List<(ScaleTransform3D Scale, double BaseY)> _pet3DEyeScales = [];
    private PetCharacter? _rendered3DCharacter;
    private double _targetPetX = 58;
    private double _targetPetY = 48;
    private double _motionPhase;
    private bool _hiddenByUser;
    private bool _hiddenByFullscreen;
    private const double PetSize = 96;
    private const double MinHabitatWidth = 480;
    private const double MinHabitatHeight = 270;
    private static readonly PetOption[] PetOptions =
    [
        new("Orange Cat", "Orange", ["!", "...", "Z"]),
        new("Gray Cat", "Gray", ["!", "?", "..."]),
        new("Cream Cat", "Cream", ["...", "!", "<3"]),
        new("Black Cat", "Black", ["...", "!", "Z"]),
        new("Calico Cat", "Calico", ["!", "<3", "?"]),
        new("White Cat", "White", ["?", "...", "!"])
    ];

    private static readonly string[] BackgroundOptions =
    [
        "Spring Farm",
        "Flower Meadow",
        "Forest Pond",
        "Mountain Ranch",
        "Autumn Orchard",
        "Night Garden"
    ];

    public MainWindow()
    {
        InitializeComponent();
        LogService.Write("MainWindow constructor after InitializeComponent");
        DataContext = _viewModel;

        _fullscreenDetectionService = new FullscreenDetectionService(_monitorService);
        _settings = _settingsService.Load();
        _settings.StartWithWindows = _startupService.IsEnabled();
        NormalizeVisualSettings();
        _viewModel.Character = _settings.Character;
        _viewModel.IsUnlocked = !_settings.Locked;
        FarmScene.Theme = _settings.Background;

        Topmost = _settings.AlwaysOnTop;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _animationTimer.Tick += (_, _) => UpdateAnimation();

        _slowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _slowTimer.Tick += (_, _) => UpdateSlowState();

        ScheduleNextBlink();
        ScheduleNextMoveTarget();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LogService.Write("Window loaded");
        ApplySizeForCurrentDpi();
        PositionFromSettings();
        LogService.Write($"Window positioned Left={Left}, Top={Top}, Width={Width}, Height={Height}");
        BuildContextMenu();
        BuildTrayIcon();
        PlacePetInsideHabitat();
        InitializePixelPets();
        _animationTimer.Start();
        _slowTimer.Start();
        Activate();
        Focus();
        LogService.Write("Window timers started");
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        LogService.Write($"Source initialized handle={_windowHandle}");
        ApplyNativeWindowStyles();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveCurrentPosition();
        _notifyIcon?.Dispose();
    }

    private void HitSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
        {
            ToggleHiddenByUser();
            return;
        }

        _viewModel.Mood = PetMood.Poked;
        _pokedUntil = DateTime.UtcNow.AddMilliseconds(700);
        _hopUntil = DateTime.UtcNow.AddMilliseconds(420);

        if (!_settings.Locked)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if the mouse button was released mid-message.
            }
        }
    }

    private void HitSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_settings.Locked)
        {
            SaveCurrentPosition();
        }
    }

    private void UpdateAnimation()
    {
        var now = DateTime.UtcNow;
        _viewModel.IsBlinking = now < _blinkUntil || _viewModel.IsSleeping;

        if (now >= _nextBlinkAt && !_viewModel.IsSleeping)
        {
            _blinkUntil = now.AddMilliseconds(130);
            ScheduleNextBlink();
        }

        if (_settings.LookAtMouse && !_viewModel.IsSleeping)
        {
            UpdateEyeTracking();
        }
        else
        {
            _viewModel.EyeOffsetX *= 0.75;
            _viewModel.EyeOffsetY *= 0.75;
        }

        if (_viewModel.Mood == PetMood.Poked && now > _pokedUntil)
        {
            _viewModel.Mood = PetMood.Idle;
        }

        UpdatePetMotion(now);
        UpdatePixelPets(now);
    }

    private void UpdateEyeTracking()
    {
        var mouse = _mouseTrackingService.GetCursorPosition();
        var dpi = VisualTreeHelper.GetDpi(this);
        var centerX = Left * dpi.DpiScaleX + (_viewModel.PetOffsetX + PetSize / 2.0) * dpi.DpiScaleX;
        var centerY = Top * dpi.DpiScaleY + (_viewModel.PetOffsetY + PetSize / 2.0) * dpi.DpiScaleY;
        var dx = mouse.X - centerX;
        var dy = mouse.Y - centerY;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < 0.001)
        {
            return;
        }

        var nearBoost = Math.Clamp(240.0 / distance, 0.0, 1.0);
        var maxOffset = 4.0 + nearBoost * 5.0;
        _viewModel.EyeOffsetX = dx / distance * maxOffset;
        _viewModel.EyeOffsetY = dy / distance * maxOffset;

        if (_viewModel.Mood != PetMood.Poked && IsMouseNearPet(mouse.X, mouse.Y))
        {
            _viewModel.Mood = PetMood.Alert;
        }
        else if (_viewModel.Mood == PetMood.Alert)
        {
            _viewModel.Mood = PetMood.Idle;
        }
    }

    private void UpdateSlowState()
    {
        if (_settings.HideDuringFullscreen)
        {
            var shouldHide = _fullscreenDetectionService.ShouldHideForFullscreen(_settings, _windowHandle);
            SetHiddenByFullscreen(shouldHide);
        }
        else
        {
            SetHiddenByFullscreen(false);
        }

        if (_hiddenByUser || _hiddenByFullscreen)
        {
            return;
        }

        var idle = _idleDetectionService.GetIdleDuration();
        var reaction = _settings.ReactToSystemStats ? _systemStatsService.GetReaction() : null;

        if (_viewModel.Mood == PetMood.Poked || _viewModel.Mood == PetMood.Alert)
        {
            return;
        }

        if (reaction is PetMood.CapsLock)
        {
            _viewModel.Mood = PetMood.CapsLock;
            _viewModel.StatusText = "CAPS!";
        }
        else if (reaction is PetMood.NoInternet)
        {
            _viewModel.Mood = PetMood.NoInternet;
            _viewModel.StatusText = "DNS?";
        }
        else if (_settings.SleepWhenIdle && idle >= TimeSpan.FromMinutes(2))
        {
            _viewModel.Mood = PetMood.Sleep;
            _viewModel.StatusText = string.Empty;
        }
        else if (_settings.SleepWhenIdle && idle >= TimeSpan.FromSeconds(30))
        {
            _viewModel.Mood = PetMood.Bored;
            _viewModel.StatusText = string.Empty;
        }
        else
        {
            _viewModel.Mood = PetMood.Idle;
            _viewModel.StatusText = string.Empty;
        }
    }

    private void PositionFromSettings()
    {
        var monitor = _monitorService.GetTargetMonitor(_settings);
        _settings.TargetMonitorDeviceName = monitor.DeviceName;
        var position = _monitorService.GetWindowPosition(_settings, monitor);
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = position.X / dpi.DpiScaleX;
        Top = position.Y / dpi.DpiScaleY;
    }

    private void SaveCurrentPosition()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var physicalLeft = Left * dpi.DpiScaleX;
        var physicalTop = Top * dpi.DpiScaleY;
        var physicalWidth = Width * dpi.DpiScaleX;
        var physicalHeight = Height * dpi.DpiScaleY;
        var monitor = _monitorService.GetMonitorForPoint(
            physicalLeft + physicalWidth / 2.0,
            physicalTop + physicalHeight / 2.0);

        _settings.Width = physicalWidth;
        _settings.Height = physicalHeight;
        _monitorService.CaptureRelativePosition(_settings, monitor, physicalLeft, physicalTop);
        _settingsService.Save(_settings);
    }

    private void NormalizeVisualSettings()
    {
        if (!BackgroundOptions.Contains(_settings.Background))
        {
            _settings.Background = BackgroundOptions[0];
        }

        var enabled = _settings.EnabledPets
            .Where(name => PetOptions.Any(option => option.Name == name))
            .Distinct()
            .ToList();

        _settings.EnabledPets = enabled;
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Pet Dude", IsEnabled = false, FontWeight = FontWeights.Bold });
        menu.Items.Add(new Separator());

        var monitorMenu = new MenuItem { Header = "Move to Monitor" };
        foreach (var monitor in _monitorService.GetMonitors())
        {
            var item = new MenuItem
            {
                Header = $"{monitor.DisplayName}{(monitor.IsPrimary ? " (Primary)" : string.Empty)}",
                IsCheckable = true,
                IsChecked = monitor.DeviceName == _settings.TargetMonitorDeviceName,
                Tag = monitor
            };
            item.Click += (_, _) =>
            {
                _settings.TargetMonitorDeviceName = ((MonitorInfo)item.Tag).DeviceName;
                PositionFromSettings();
                SaveCurrentPosition();
                BuildContextMenu();
                BuildTrayIcon();
            };
            monitorMenu.Items.Add(item);
        }
        menu.Items.Add(monitorMenu);

        var positionMenu = new MenuItem { Header = "Position" };
        positionMenu.Items.Add(CheckItem("Lock Habitat", _settings.Locked, value =>
        {
            _settings.Locked = value;
            _viewModel.IsUnlocked = !value;
        }));
        positionMenu.Items.Add(CommandItem("Save Current Position", SaveCurrentPosition));
        positionMenu.Items.Add(CommandItem("Reset Position", () =>
        {
            _monitorService.ResetToDefaultPosition(_settings);
            PositionFromSettings();
            SaveCurrentPosition();
        }));
        menu.Items.Add(positionMenu);

        menu.Items.Add(BuildPetMenu());
        menu.Items.Add(BuildBackgroundMenu());

        var behaviorMenu = new MenuItem { Header = "Behavior" };
        behaviorMenu.Items.Add(CheckItem("Sleep When Idle", _settings.SleepWhenIdle, value => _settings.SleepWhenIdle = value));
        behaviorMenu.Items.Add(CheckItem("Move Around Habitat", _settings.MoveAroundHabitat, value =>
        {
            _settings.MoveAroundHabitat = value;
            ScheduleNextMoveTarget();
        }));
        behaviorMenu.Items.Add(CheckItem("React to System Stats", _settings.ReactToSystemStats, value => _settings.ReactToSystemStats = value));
        behaviorMenu.Items.Add(CheckItem("Hide During Fullscreen Apps", _settings.HideDuringFullscreen, value => _settings.HideDuringFullscreen = value));
        behaviorMenu.Items.Add(BuildFullscreenModeMenu());
        menu.Items.Add(behaviorMenu);

        var visibilityMenu = new MenuItem { Header = "Visibility" };
        visibilityMenu.Items.Add(CheckItem("Always on Top", _settings.AlwaysOnTop, value =>
        {
            _settings.AlwaysOnTop = value;
            Topmost = value;
        }));
        visibilityMenu.Items.Add(CheckItem("Click Through", _settings.ClickThrough, value =>
        {
            _settings.ClickThrough = value;
            ApplyNativeWindowStyles();
        }));
        visibilityMenu.Items.Add(CommandItem(_hiddenByUser ? "Show" : "Hide", ToggleHiddenByUser));
        menu.Items.Add(visibilityMenu);

        var startupMenu = new MenuItem { Header = "Startup" };
        startupMenu.Items.Add(CheckItem("Start with Windows", _settings.StartWithWindows, value =>
        {
            _settings.StartWithWindows = value;
            _startupService.SetEnabled(value);
        }));
        menu.Items.Add(startupMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(CommandItem("Exit", Close));
        HitSurface.ContextMenu = menu;
    }

    private MenuItem BuildCharacterMenu()
    {
        var menu = new MenuItem { Header = "Character" };
        AddCharacterItem(menu, "Cat", PetCharacter.Cat);
        AddCharacterItem(menu, "Dog", PetCharacter.Dog);
        AddCharacterItem(menu, "Robot", PetCharacter.Robot);
        return menu;
    }

    private MenuItem BuildPetMenu()
    {
        var menu = new MenuItem { Header = "Pets" };
        foreach (var option in PetOptions)
        {
            var item = new MenuItem
            {
                Header = option.Name,
                IsCheckable = true,
                IsChecked = _settings.EnabledPets.Contains(option.Name)
            };
            item.Click += (_, _) =>
            {
                if (item.IsChecked)
                {
                    if (!_settings.EnabledPets.Contains(option.Name))
                    {
                        _settings.EnabledPets.Add(option.Name);
                    }
                }
                else
                {
                    _settings.EnabledPets.Remove(option.Name);
                }

                SyncPixelPetsFromSettings();
                _settingsService.Save(_settings);
                BuildContextMenu();
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(CommandItem("Clear Pets", () =>
        {
            _settings.EnabledPets.Clear();
            SyncPixelPetsFromSettings();
            _settingsService.Save(_settings);
            BuildContextMenu();
        }));
        menu.Items.Add(CommandItem("All Pets", () =>
        {
            _settings.EnabledPets = PetOptions.Select(option => option.Name).ToList();
            SyncPixelPetsFromSettings();
            _settingsService.Save(_settings);
            BuildContextMenu();
        }));
        return menu;
    }

    private MenuItem BuildBackgroundMenu()
    {
        var menu = new MenuItem { Header = "Background" };
        foreach (var background in BackgroundOptions)
        {
            var item = new MenuItem
            {
                Header = background,
                IsCheckable = true,
                IsChecked = _settings.Background == background
            };
            item.Click += (_, _) =>
            {
                _settings.Background = background;
                FarmScene.Theme = background;
                _settingsService.Save(_settings);
                BuildContextMenu();
            };
            menu.Items.Add(item);
        }

        return menu;
    }

    private void AddCharacterItem(MenuItem menu, string header, PetCharacter character)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = _settings.Character == character
        };
        item.Click += (_, _) =>
        {
            _settings.Character = character;
            _viewModel.Character = character;
            _settingsService.Save(_settings);
            BuildContextMenu();
        };
        menu.Items.Add(item);
    }

    private MenuItem BuildFullscreenModeMenu()
    {
        var menu = new MenuItem { Header = "Fullscreen Hide Mode" };
        AddModeItem(menu, "Off", FullscreenHideMode.Off);
        AddModeItem(menu, "Pet Monitor Only", FullscreenHideMode.PetMonitorOnly);
        AddModeItem(menu, "Any Monitor", FullscreenHideMode.AnyMonitor);
        return menu;
    }

    private void AddModeItem(MenuItem menu, string header, FullscreenHideMode mode)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = _settings.FullscreenHideMode == mode
        };
        item.Click += (_, _) =>
        {
            _settings.FullscreenHideMode = mode;
            _settings.HideDuringFullscreen = mode != FullscreenHideMode.Off;
            _settingsService.Save(_settings);
            BuildContextMenu();
            BuildTrayIcon();
        };
        menu.Items.Add(item);
    }

    private MenuItem CheckItem(string header, bool isChecked, Action<bool> onChanged)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
        item.Click += (_, _) =>
        {
            onChanged(item.IsChecked);
            _settingsService.Save(_settings);
            BuildContextMenu();
            BuildTrayIcon();
        };
        return item;
    }

    private static MenuItem CommandItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void BuildTrayIcon()
    {
        if (_notifyIcon is null)
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Pet Dude",
                Visible = true
            };
            _notifyIcon.DoubleClick += (_, _) => ToggleHiddenByUser();
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show / Hide", null, (_, _) => Dispatcher.Invoke(ToggleHiddenByUser));
        menu.Items.Add(_settings.Locked ? "Unlock Habitat" : "Lock Habitat", null, (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.Locked = !_settings.Locked;
            _viewModel.IsUnlocked = !_settings.Locked;
            _settingsService.Save(_settings);
            BuildContextMenu();
            BuildTrayIcon();
        }));
        menu.Items.Add(_settings.ClickThrough ? "Disable Click Through" : "Enable Click Through", null, (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.ClickThrough = !_settings.ClickThrough;
            ApplyNativeWindowStyles();
            _settingsService.Save(_settings);
            BuildContextMenu();
            BuildTrayIcon();
        }));
        menu.Items.Add("Save Position", null, (_, _) => Dispatcher.Invoke(SaveCurrentPosition));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Close));

        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
    }

    private void ToggleHiddenByUser()
    {
        _hiddenByUser = !_hiddenByUser;
        ApplyVisibility();
        BuildContextMenu();
        BuildTrayIcon();
    }

    private void SetHiddenByFullscreen(bool hidden)
    {
        if (_hiddenByFullscreen == hidden)
        {
            return;
        }

        _hiddenByFullscreen = hidden;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_hiddenByUser || _hiddenByFullscreen)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private void ApplyNativeWindowStyles()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GWL_EXSTYLE).ToInt64();
        style &= ~NativeMethods.WS_EX_TOOLWINDOW;
        style |= NativeMethods.WS_EX_APPWINDOW;

        if (_settings.ClickThrough)
        {
            style |= NativeMethods.WS_EX_TRANSPARENT;
        }
        else
        {
            style &= ~NativeMethods.WS_EX_TRANSPARENT;
        }

        NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    private void FieldViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(0, e.NewSize.Width);
        var height = Math.Max(0, e.NewSize.Height);

        TopHighlight.Width = width;
        TopShade.Width = width;

        DirtBase.Width = width;
        DirtRim.Width = width;
        DirtShadow.Width = width;

        Canvas.SetTop(DirtBase, Math.Max(0, height - DirtBase.Height));
        Canvas.SetTop(DirtRim, Math.Max(0, height - DirtBase.Height - DirtRim.Height));
        Canvas.SetTop(DirtShadow, Math.Max(0, height - DirtShadow.Height));
    }

    private void InitializePixelPets()
    {
        SyncPixelPetsFromSettings();
    }

    private void SyncPixelPetsFromSettings()
    {
        foreach (var element in _dynamicPetElements)
        {
            PixelPetLayer.Children.Remove(element);
        }

        _dynamicPetElements.Clear();
        _pixelPets.Clear();

        var selectedOptions = _settings.EnabledPets
            .Select(name => PetOptions.FirstOrDefault(option => option.Name == name))
            .Where(option => option is not null)
            .Cast<PetOption>()
            .ToList();

        for (var index = 0; index < selectedOptions.Count; index++)
        {
            var option = selectedOptions[index];
            var relativeX = selectedOptions.Count == 1 ? 0.48 : 0.14 + index * (0.72 / Math.Max(1, selectedOptions.Count - 1));
            var relativeY = 0.52 + (index % 3) * 0.08;
            _pixelPets.Add(CreatePixelPet(option, relativeX, relativeY, 0.72 + index % 3 * 0.08));
        }

        var now = DateTime.UtcNow;
        foreach (var pet in _pixelPets)
        {
            PickPixelPetTarget(pet, now);
            ApplyPixelPetPosition(pet);
        }
    }

    private PixelPetState CreatePixelPet(PetOption option, double relativeX, double relativeY, double speed)
    {
        var fieldWidth = Math.Max(MinHabitatWidth, FieldViewport.ActualWidth);
        var fieldHeight = Math.Max(MinHabitatHeight, FieldViewport.ActualHeight);
        var now = DateTime.UtcNow;
        var facing = new ScaleTransform(1, 1, PetSize / 2.0, PetSize / 2.0);
        var bob = new TranslateTransform();
        var sprite = new PixelPetSprite
        {
            Width = PetSize,
            Height = PetSize,
            Variant = option.Variant,
            Direction = "Down",
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    facing,
                    bob
                }
            }
        };

        var emoteText = new TextBlock
        {
            Text = "!",
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 23, 5)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        var emote = new Border
        {
            Width = 38,
            Height = 24,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 243, 196)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 23, 5)),
            BorderThickness = new Thickness(2),
            Child = emoteText,
            Visibility = Visibility.Collapsed
        };

        PixelPetLayer.Children.Add(sprite);
        PixelPetLayer.Children.Add(emote);
        _dynamicPetElements.Add(sprite);
        _dynamicPetElements.Add(emote);

        return new PixelPetState
        {
            Sprite = sprite,
            Facing = facing,
            Bob = bob,
            Emote = emote,
            EmoteText = emoteText,
            Emotes = option.Emotes,
            X = Math.Clamp(relativeX * fieldWidth, 8, Math.Max(8, fieldWidth - PetSize - 8)),
            Y = Math.Clamp(relativeY * fieldHeight, 72, Math.Max(72, fieldHeight - PetSize - 24)),
            Speed = speed,
            Phase = _random.NextDouble() * Math.PI * 2,
            WanderPhase = _random.NextDouble() * Math.PI * 2,
            HeadingAngle = Math.PI / 2.0,
            NextDirectionAt = now,
            NextEmoteAt = now.AddMilliseconds(_random.Next(1400, 5200)),
            HopUntil = now.AddMilliseconds(_random.Next(300, 1800)),
            NextPoseAt = now.AddMilliseconds(_random.Next(1500, 4800))
        };
    }

    private void UpdatePixelPets(DateTime now)
    {
        if (_pixelPets.Count == 0)
        {
            return;
        }

        var fieldWidth = Math.Max(MinHabitatWidth, FieldViewport.ActualWidth);
        var fieldHeight = Math.Max(MinHabitatHeight, FieldViewport.ActualHeight);
        var canMove = _settings.MoveAroundHabitat && !_viewModel.IsSleeping;

        foreach (var pet in _pixelPets)
        {
            pet.Phase += 0.12;
            pet.WanderPhase += 0.028 + pet.Speed * 0.01;
            UpdatePixelPetMood(pet, now);

            if (!canMove)
            {
                pet.VelocityX *= 0.80;
                pet.VelocityY *= 0.80;
                pet.Bob.Y = Math.Sin(pet.Phase * 0.35) * 1.0;
                pet.Facing.ScaleY = 1.0;
                pet.Sprite.IsWalking = false;
                pet.Sprite.Frame = (int)Math.Round(pet.Phase * 8);
                pet.Sprite.Pose = pet.Pose;
                pet.Sprite.Direction = pet.Direction;
                ApplyPixelPetPosition(pet);
                continue;
            }

            if (now >= pet.NextTargetAt)
            {
                PickPixelPetTarget(pet, now);
            }

            var dx = pet.TargetX - pet.X;
            var dy = pet.TargetY - pet.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance > 4.0)
            {
                var targetNormX = dx / distance;
                var targetNormY = dy / distance;
                var arc = Math.Sin(pet.WanderPhase) * Math.Clamp(distance / 260.0, 0.08, 0.32);
                var aimX = targetNormX - targetNormY * arc;
                var aimY = targetNormY + targetNormX * arc;
                AddPetSpacing(pet, ref aimX, ref aimY);
                Normalize(ref aimX, ref aimY);

                var targetSpeed = pet.Speed * Math.Clamp((distance - 4.0) / 70.0, 0.22, 1.0);
                var steer = Math.Clamp(0.045 + distance / 900.0, 0.045, 0.12);
                var desiredX = aimX * targetSpeed;
                var desiredY = aimY * targetSpeed;
                var currentSpeed = Math.Sqrt(pet.VelocityX * pet.VelocityX + pet.VelocityY * pet.VelocityY);

                if (currentSpeed < 0.03)
                {
                    pet.VelocityX = desiredX * 0.45;
                    pet.VelocityY = desiredY * 0.45;
                }
                else
                {
                    pet.VelocityX = Lerp(pet.VelocityX, desiredX, steer);
                    pet.VelocityY = Lerp(pet.VelocityY, desiredY, steer);
                }

                var velocityLength = Math.Sqrt(pet.VelocityX * pet.VelocityX + pet.VelocityY * pet.VelocityY);
                if (velocityLength > pet.Speed)
                {
                    pet.VelocityX = pet.VelocityX / velocityLength * pet.Speed;
                    pet.VelocityY = pet.VelocityY / velocityLength * pet.Speed;
                    velocityLength = pet.Speed;
                }

                if (velocityLength > distance)
                {
                    pet.X = pet.TargetX;
                    pet.Y = pet.TargetY;
                    pet.VelocityX *= 0.35;
                    pet.VelocityY *= 0.35;
                }
                else
                {
                    pet.X += pet.VelocityX;
                    pet.Y += pet.VelocityY;
                }

                var movingAngle = Math.Atan2(pet.VelocityY, pet.VelocityX);
                pet.HeadingAngle = SmoothAngle(pet.HeadingAngle, movingAngle, 0.13);
                var gait = Math.Abs(Math.Sin(pet.Phase * 1.8));
                UpdatePetDirection(pet, DirectionFromAngle(pet.HeadingAngle), now);
                pet.Facing.ScaleX = pet.Direction.Contains("Left", StringComparison.Ordinal) ? -1 : 1;
                pet.Facing.ScaleY = 1.0;
                pet.Bob.Y = -gait * 2.5;
                pet.IsWalking = true;
                pet.Pose = "Walk";
            }
            else
            {
                pet.VelocityX *= 0.72;
                pet.VelocityY *= 0.72;
                pet.IsWalking = false;
                if (now < pet.HopUntil)
                {
                    var hopProgress = 1.0 - Math.Max(0, (pet.HopUntil - now).TotalMilliseconds) / 360.0;
                    pet.Bob.Y = -Math.Sin(Math.Clamp(hopProgress, 0, 1) * Math.PI) * 4;
                    pet.Facing.ScaleY = 0.98;
                    pet.Pose = "Idle";
                }
                else
                {
                    pet.Bob.Y = Math.Sin(pet.Phase * 0.42) * 0.8;
                    pet.Facing.ScaleY = 1.0;
                    if (now >= pet.NextPoseAt)
                    {
                        pet.Pose = _random.NextDouble() < 0.45 ? "Loaf" : _random.NextDouble() < 0.65 ? "Sit" : "Idle";
                        pet.NextPoseAt = now.AddMilliseconds(_random.Next(2400, 6200));
                    }
                }

                if (now >= pet.PauseUntil)
                {
                    pet.PauseUntil = now.AddMilliseconds(_random.Next(600, 1700));
                    pet.NextTargetAt = pet.PauseUntil;
                    if (_random.NextDouble() < 0.38)
                    {
                        pet.Direction = RandomPetDirection();
                        pet.DirectionCandidate = pet.Direction;
                        pet.NextDirectionAt = now;
                        pet.Facing.ScaleX = pet.Direction.Contains("Left", StringComparison.Ordinal) ? -1 : 1;
                    }
                }
            }

            var minX = 8.0;
            var maxX = Math.Max(8, fieldWidth - PetSize - 8);
            var minY = 70.0;
            var maxY = Math.Max(70, fieldHeight - PetSize - 22);
            var clampedX = Math.Clamp(pet.X, minX, maxX);
            var clampedY = Math.Clamp(pet.Y, minY, maxY);
            if (Math.Abs(clampedX - pet.X) > 0.001)
            {
                pet.VelocityX *= -0.35;
            }

            if (Math.Abs(clampedY - pet.Y) > 0.001)
            {
                pet.VelocityY *= -0.35;
            }

            pet.X = clampedX;
            pet.Y = clampedY;
            pet.Sprite.IsWalking = pet.IsWalking;
            pet.Sprite.Frame = (int)Math.Round(pet.Phase * 10);
            pet.Sprite.Pose = pet.Pose;
            pet.Sprite.Direction = pet.Direction;
            ApplyPixelPetPosition(pet);
        }
    }

    private void UpdatePixelPetMood(PixelPetState pet, DateTime now)
    {
        if (now >= pet.NextEmoteAt)
        {
            pet.EmoteText.Text = pet.Emotes[_random.Next(pet.Emotes.Length)];
            pet.Emote.Visibility = Visibility.Visible;
            pet.EmoteUntil = now.AddMilliseconds(_random.Next(800, 1500));
            pet.NextEmoteAt = now.AddMilliseconds(_random.Next(3500, 8200));
            pet.HopUntil = now.AddMilliseconds(280);
            pet.Pose = "Idle";
        }

        if (pet.Emote.Visibility == Visibility.Visible && now > pet.EmoteUntil)
        {
            pet.Emote.Visibility = Visibility.Collapsed;
        }
    }

    private void PickPixelPetTarget(PixelPetState pet, DateTime now)
    {
        var fieldWidth = Math.Max(MinHabitatWidth, FieldViewport.ActualWidth);
        var fieldHeight = Math.Max(MinHabitatHeight, FieldViewport.ActualHeight);
        pet.TargetX = 12 + _random.NextDouble() * Math.Max(1, fieldWidth - PetSize - 24);
        pet.TargetY = 74 + _random.NextDouble() * Math.Max(1, fieldHeight - PetSize - 106);
        pet.NextTargetAt = now.AddMilliseconds(_random.Next(1800, 4200));
        pet.PauseUntil = now.AddMilliseconds(_random.Next(250, 900));
    }

    private static void ApplyPixelPetPosition(PixelPetState pet)
    {
        Canvas.SetLeft(pet.Sprite, Math.Round(pet.X));
        Canvas.SetTop(pet.Sprite, Math.Round(pet.Y));
        System.Windows.Controls.Panel.SetZIndex(pet.Sprite, (int)Math.Round(pet.Y));

        Canvas.SetLeft(pet.Emote, Math.Round(pet.X + 23));
        Canvas.SetTop(pet.Emote, Math.Round(pet.Y - 20 + pet.Bob.Y));
        System.Windows.Controls.Panel.SetZIndex(pet.Emote, (int)Math.Round(pet.Y + 100));
    }

    private void AddPetSpacing(PixelPetState pet, ref double aimX, ref double aimY)
    {
        foreach (var other in _pixelPets)
        {
            if (ReferenceEquals(pet, other))
            {
                continue;
            }

            var separationX = pet.X - other.X;
            var separationY = pet.Y - other.Y;
            var distanceSquared = separationX * separationX + separationY * separationY;
            if (distanceSquared is < 0.001 or > 10000)
            {
                continue;
            }

            var distance = Math.Sqrt(distanceSquared);
            var force = Math.Pow(1.0 - distance / 100.0, 2.0) * 0.85;
            aimX += separationX / distance * force;
            aimY += separationY / distance * force;
        }
    }

    private static void Normalize(ref double x, ref double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        if (length < 0.001)
        {
            x = 1;
            y = 0;
            return;
        }

        x /= length;
        y /= length;
    }

    private static void UpdatePetDirection(PixelPetState pet, string nextDirection, DateTime now)
    {
        if (nextDirection == pet.Direction)
        {
            pet.DirectionCandidate = nextDirection;
            pet.NextDirectionAt = now;
            return;
        }

        if (nextDirection != pet.DirectionCandidate)
        {
            pet.DirectionCandidate = nextDirection;
            pet.NextDirectionAt = now.AddMilliseconds(120);
            return;
        }

        if (now >= pet.NextDirectionAt)
        {
            pet.Direction = nextDirection;
        }
    }

    private static string DirectionFromAngle(double angleRadians)
    {
        var angle = angleRadians * 180.0 / Math.PI;
        return angle switch
        {
            >= -22.5 and < 22.5 => "Right",
            >= 22.5 and < 67.5 => "DownRight",
            >= 67.5 and < 112.5 => "Down",
            >= 112.5 and < 157.5 => "DownLeft",
            >= 157.5 or < -157.5 => "Left",
            >= -157.5 and < -112.5 => "UpLeft",
            >= -112.5 and < -67.5 => "Up",
            _ => "UpRight"
        };
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + (to - from) * amount;
    }

    private static double SmoothAngle(double current, double target, double amount)
    {
        var delta = Math.Atan2(Math.Sin(target - current), Math.Cos(target - current));
        return current + delta * amount;
    }

    private string RandomPetDirection()
    {
        return _random.Next(8) switch
        {
            0 => "Up",
            1 => "UpRight",
            2 => "Right",
            3 => "DownRight",
            4 => "Down",
            5 => "DownLeft",
            6 => "Left",
            _ => "UpLeft"
        };
    }

    private void Configure3DViewport()
    {
        Pet3DViewport.Camera = new PerspectiveCamera(
            new Point3D(0, 0.48, 5.2),
            new Vector3D(0, -0.04, -1),
            new Vector3D(0, 1, 0),
            33);
    }

    private void Build3DCharacter()
    {
        if (_rendered3DCharacter == _viewModel.Character && Pet3DViewport.Children.Count > 0)
        {
            return;
        }

        _rendered3DCharacter = _viewModel.Character;
        _pet3DEyeOffsets.Clear();
        _pet3DEyeScales.Clear();
        Pet3DViewport.Children.Clear();

        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(System.Windows.Media.Color.FromRgb(82, 78, 72)));
        scene.Children.Add(new DirectionalLight(System.Windows.Media.Color.FromRgb(255, 249, 232), new Vector3D(-0.55, -0.75, -0.45)));
        scene.Children.Add(new DirectionalLight(System.Windows.Media.Color.FromRgb(125, 185, 255), new Vector3D(0.7, 0.15, -0.55)));

        var pet = new Model3DGroup
        {
            Transform = CreatePet3DRootTransform()
        };

        switch (_viewModel.Character)
        {
            case PetCharacter.Dog:
                Build3DDog(pet);
                break;
            case PetCharacter.Robot:
                Build3DRobot(pet);
                break;
            default:
                Build3DCat(pet);
                break;
        }

        scene.Children.Add(pet);
        Pet3DViewport.Children.Add(new ModelVisual3D { Content = scene });
        Update3DAnimation();
    }

    private Transform3DGroup CreatePet3DRootTransform()
    {
        return new Transform3DGroup
        {
            Children =
            {
                new ScaleTransform3D(0.96, 0.96, 0.96),
                new RotateTransform3D(_pet3DYaw),
                new RotateTransform3D(_pet3DPitch),
                new RotateTransform3D(_pet3DRoll),
                _pet3DBob
            }
        };
    }

    private void Update3DAnimation()
    {
        if (Pet3DViewport.Children.Count == 0)
        {
            return;
        }

        _pet3DYaw.Angle = _viewModel.FaceScaleX < 0 ? -14 : 14;
        _pet3DPitch.Angle = Math.Clamp(_viewModel.EyeOffsetY * -1.2, -8, 8);
        _pet3DRoll.Angle = Math.Clamp(_viewModel.BodyTilt * -1.2, -8, 8);
        _pet3DBob.OffsetY = -_viewModel.BodyBobY / 36.0;

        var blinkScale = _viewModel.IsBlinking ? 0.12 : 1.0;
        foreach (var eye in _pet3DEyeScales)
        {
            eye.Scale.ScaleY = eye.BaseY * blinkScale;
        }

        var offsetX = Math.Clamp(_viewModel.EyeOffsetX / 58.0, -0.14, 0.14);
        var offsetY = Math.Clamp(-_viewModel.EyeOffsetY / 58.0, -0.10, 0.10);
        foreach (var eyeOffset in _pet3DEyeOffsets)
        {
            eyeOffset.OffsetX = offsetX;
            eyeOffset.OffsetY = offsetY;
        }
    }

    private void Build3DCat(Model3DGroup pet)
    {
        var fur = CreateMaterial(System.Windows.Media.Color.FromRgb(246, 180, 105), 64);
        var cream = CreateMaterial(System.Windows.Media.Color.FromRgb(255, 232, 194), 48);
        var shadowFur = CreateMaterial(System.Windows.Media.Color.FromRgb(209, 126, 72), 36);
        var stripe = CreateMaterial(System.Windows.Media.Color.FromRgb(132, 78, 51), 20);
        var pink = CreateMaterial(System.Windows.Media.Color.FromRgb(232, 112, 132), 55);
        var eye = CreateMaterial(System.Windows.Media.Color.FromRgb(68, 214, 190), 92);
        var pupil = CreateMaterial(System.Windows.Media.Color.FromRgb(18, 28, 32), 30);
        var white = CreateMaterial(System.Windows.Media.Color.FromRgb(255, 255, 248), 95);

        AddSphere(pet, new Point3D(0, -0.38, 0), new Vector3D(0.62, 0.54, 0.46), fur);
        AddSphere(pet, new Point3D(0.16, -0.47, -0.02), new Vector3D(0.43, 0.43, 0.36), shadowFur, 0.45);
        AddSphere(pet, new Point3D(-0.16, -0.34, 0.42), new Vector3D(0.32, 0.28, 0.10), cream, 0.74);
        AddSphere(pet, new Point3D(-0.36, -0.88, 0.28), new Vector3D(0.23, 0.13, 0.18), cream);
        AddSphere(pet, new Point3D(0.36, -0.88, 0.28), new Vector3D(0.23, 0.13, 0.18), cream);

        AddSphere(pet, new Point3D(0, 0.36, 0), new Vector3D(0.68, 0.61, 0.55), fur);
        AddSphere(pet, new Point3D(0.18, 0.24, -0.02), new Vector3D(0.50, 0.48, 0.42), shadowFur, 0.36);
        AddSphere(pet, new Point3D(-0.15, 0.25, 0.48), new Vector3D(0.38, 0.25, 0.14), cream, 0.82);
        AddSphere(pet, new Point3D(0.15, 0.25, 0.48), new Vector3D(0.38, 0.25, 0.14), cream, 0.82);
        AddSphere(pet, new Point3D(0, 0.08, 0.66), new Vector3D(0.22, 0.13, 0.12), cream);

        AddCone(pet, new Point3D(-0.39, 0.88, 0.03), new Vector3D(0.22, 0.34, 0.16), fur, new Vector3D(0, 0, 23));
        AddCone(pet, new Point3D(0.39, 0.88, 0.03), new Vector3D(0.22, 0.34, 0.16), fur, new Vector3D(0, 0, -23));
        AddCone(pet, new Point3D(-0.39, 0.87, 0.08), new Vector3D(0.12, 0.21, 0.08), pink, new Vector3D(0, 0, 23));
        AddCone(pet, new Point3D(0.39, 0.87, 0.08), new Vector3D(0.12, 0.21, 0.08), pink, new Vector3D(0, 0, -23));

        AddEye(pet, new Point3D(-0.25, 0.42, 0.55), new Vector3D(0.15, 0.18, 0.06), eye, pupil, white);
        AddEye(pet, new Point3D(0.25, 0.42, 0.55), new Vector3D(0.15, 0.18, 0.06), eye, pupil, white);
        AddSphere(pet, new Point3D(0, 0.18, 0.74), new Vector3D(0.08, 0.05, 0.04), pink);
        AddCylinder(pet, new Point3D(-0.08, 0.08, 0.77), new Vector3D(0.03, 0.11, 0.03), stripe, new Vector3D(0, 0, 58), 0.88);
        AddCylinder(pet, new Point3D(0.08, 0.08, 0.77), new Vector3D(0.03, 0.11, 0.03), stripe, new Vector3D(0, 0, -58), 0.88);

        AddCylinder(pet, new Point3D(-0.22, 0.82, 0.44), new Vector3D(0.025, 0.28, 0.025), stripe, new Vector3D(0, 0, 74), 0.85);
        AddCylinder(pet, new Point3D(0, 0.86, 0.46), new Vector3D(0.025, 0.24, 0.025), stripe, new Vector3D(0, 0, 90), 0.85);
        AddCylinder(pet, new Point3D(0.22, 0.82, 0.44), new Vector3D(0.025, 0.28, 0.025), stripe, new Vector3D(0, 0, 106), 0.85);

        AddTail(pet, fur, shadowFur);
    }

    private void Build3DDog(Model3DGroup pet)
    {
        var fur = CreateMaterial(System.Windows.Media.Color.FromRgb(207, 135, 73), 58);
        var darkFur = CreateMaterial(System.Windows.Media.Color.FromRgb(111, 67, 44), 34);
        var cream = CreateMaterial(System.Windows.Media.Color.FromRgb(255, 221, 174), 42);
        var collar = CreateMaterial(System.Windows.Media.Color.FromRgb(45, 150, 128), 54);
        var tag = CreateMaterial(System.Windows.Media.Color.FromRgb(255, 211, 98), 80);
        var nose = CreateMaterial(System.Windows.Media.Color.FromRgb(36, 28, 24), 35);
        var eye = CreateMaterial(System.Windows.Media.Color.FromRgb(78, 206, 180), 90);
        var pupil = CreateMaterial(System.Windows.Media.Color.FromRgb(16, 17, 17), 30);
        var white = CreateMaterial(System.Windows.Media.Color.FromRgb(255, 255, 248), 95);

        AddSphere(pet, new Point3D(0, -0.40, 0), new Vector3D(0.66, 0.55, 0.48), fur);
        AddSphere(pet, new Point3D(0.16, -0.48, -0.01), new Vector3D(0.46, 0.43, 0.38), darkFur, 0.28);
        AddSphere(pet, new Point3D(0, -0.35, 0.42), new Vector3D(0.42, 0.32, 0.12), cream, 0.82);
        AddSphere(pet, new Point3D(-0.37, -0.89, 0.27), new Vector3D(0.25, 0.13, 0.18), cream);
        AddSphere(pet, new Point3D(0.37, -0.89, 0.27), new Vector3D(0.25, 0.13, 0.18), cream);

        AddSphere(pet, new Point3D(0, 0.34, 0), new Vector3D(0.70, 0.60, 0.55), fur);
        AddSphere(pet, new Point3D(-0.18, 0.37, 0.22), new Vector3D(0.30, 0.46, 0.12), darkFur, 0.92);
        AddSphere(pet, new Point3D(0, 0.12, 0.61), new Vector3D(0.34, 0.22, 0.20), cream);
        AddSphere(pet, new Point3D(0, 0.18, 0.79), new Vector3D(0.10, 0.07, 0.05), nose);
        AddSphere(pet, new Point3D(0.04, 0.20, 0.84), new Vector3D(0.035, 0.02, 0.015), white, 0.7);

        AddSphere(pet, new Point3D(-0.58, 0.34, 0.03), new Vector3D(0.18, 0.46, 0.10), darkFur);
        AddSphere(pet, new Point3D(0.58, 0.34, 0.03), new Vector3D(0.18, 0.46, 0.10), darkFur);
        AddSphere(pet, new Point3D(-0.57, 0.48, 0.08), new Vector3D(0.09, 0.25, 0.05), cream, 0.32);
        AddSphere(pet, new Point3D(0.57, 0.48, 0.08), new Vector3D(0.09, 0.25, 0.05), cream, 0.32);

        AddEye(pet, new Point3D(-0.24, 0.43, 0.56), new Vector3D(0.15, 0.18, 0.06), eye, pupil, white);
        AddEye(pet, new Point3D(0.24, 0.43, 0.56), new Vector3D(0.15, 0.18, 0.06), eye, pupil, white);
        AddCylinder(pet, new Point3D(0, -0.03, 0.48), new Vector3D(0.44, 0.035, 0.035), collar, new Vector3D(0, 0, 90));
        AddSphere(pet, new Point3D(0, -0.11, 0.73), new Vector3D(0.09, 0.10, 0.035), tag);
        AddTail(pet, fur, darkFur);
    }

    private void Build3DRobot(Model3DGroup pet)
    {
        var metal = CreateMaterial(System.Windows.Media.Color.FromRgb(134, 196, 216), 120);
        var darkMetal = CreateMaterial(System.Windows.Media.Color.FromRgb(65, 84, 96), 80);
        var screen = CreateMaterial(System.Windows.Media.Color.FromRgb(82, 232, 255), 140);
        var glow = CreateEmissiveMaterial(System.Windows.Media.Color.FromRgb(115, 251, 255));
        var joint = CreateMaterial(System.Windows.Media.Color.FromRgb(55, 67, 76), 60);
        var pink = CreateEmissiveMaterial(System.Windows.Media.Color.FromRgb(255, 123, 156));
        var green = CreateEmissiveMaterial(System.Windows.Media.Color.FromRgb(117, 255, 165));
        var amber = CreateEmissiveMaterial(System.Windows.Media.Color.FromRgb(255, 220, 121));

        AddBox(pet, new Point3D(0, -0.42, 0), new Vector3D(0.70, 0.50, 0.42), metal);
        AddBox(pet, new Point3D(0.18, -0.46, 0.06), new Vector3D(0.43, 0.36, 0.08), darkMetal, 0.28);
        AddBox(pet, new Point3D(0, -0.32, 0.45), new Vector3D(0.42, 0.22, 0.06), screen, 0.82);
        AddSphere(pet, new Point3D(-0.36, -0.88, 0.22), new Vector3D(0.24, 0.11, 0.16), darkMetal);
        AddSphere(pet, new Point3D(0.36, -0.88, 0.22), new Vector3D(0.24, 0.11, 0.16), darkMetal);

        AddBox(pet, new Point3D(0, 0.34, 0), new Vector3D(0.82, 0.58, 0.46), metal);
        AddBox(pet, new Point3D(0.15, 0.30, 0.06), new Vector3D(0.55, 0.42, 0.08), darkMetal, 0.25);
        AddBox(pet, new Point3D(0, 0.36, 0.50), new Vector3D(0.58, 0.36, 0.06), screen, 0.9);
        AddBox(pet, new Point3D(-0.15, 0.53, 0.56), new Vector3D(0.24, 0.08, 0.03), glow, 0.7);

        AddSphere(pet, new Point3D(-0.26, 0.42, 0.60), new Vector3D(0.15, 0.16, 0.05), glow);
        AddSphere(pet, new Point3D(0.26, 0.42, 0.60), new Vector3D(0.15, 0.16, 0.05), glow);
        AddEye(pet, new Point3D(-0.26, 0.42, 0.64), new Vector3D(0.08, 0.08, 0.025), glow, darkMetal, glow);
        AddEye(pet, new Point3D(0.26, 0.42, 0.64), new Vector3D(0.08, 0.08, 0.025), glow, darkMetal, glow);
        AddCylinder(pet, new Point3D(0, 0.16, 0.64), new Vector3D(0.035, 0.25, 0.035), darkMetal, new Vector3D(0, 0, 90));
        AddCylinder(pet, new Point3D(0, 0.96, 0), new Vector3D(0.035, 0.28, 0.035), joint);
        AddSphere(pet, new Point3D(0, 1.25, 0), new Vector3D(0.12, 0.12, 0.12), pink);

        AddCylinder(pet, new Point3D(-0.66, -0.28, 0.06), new Vector3D(0.06, 0.32, 0.06), joint, new Vector3D(0, 0, -42));
        AddCylinder(pet, new Point3D(0.66, -0.28, 0.06), new Vector3D(0.06, 0.32, 0.06), joint, new Vector3D(0, 0, 42));
        AddSphere(pet, new Point3D(-0.82, -0.54, 0.08), new Vector3D(0.13, 0.11, 0.10), metal);
        AddSphere(pet, new Point3D(0.82, -0.54, 0.08), new Vector3D(0.13, 0.11, 0.10), metal);
        AddSphere(pet, new Point3D(-0.22, -0.36, 0.52), new Vector3D(0.06, 0.06, 0.025), green);
        AddSphere(pet, new Point3D(0, -0.36, 0.52), new Vector3D(0.06, 0.06, 0.025), amber);
        AddSphere(pet, new Point3D(0.22, -0.36, 0.52), new Vector3D(0.06, 0.06, 0.025), pink);
    }

    private void AddTail(Model3DGroup pet, Material baseMaterial, Material shadeMaterial)
    {
        AddSphere(pet, new Point3D(0.67, -0.32, -0.05), new Vector3D(0.14, 0.16, 0.13), baseMaterial);
        AddSphere(pet, new Point3D(0.88, -0.14, -0.02), new Vector3D(0.13, 0.15, 0.12), baseMaterial);
        AddSphere(pet, new Point3D(0.98, 0.10, 0.01), new Vector3D(0.12, 0.14, 0.11), baseMaterial);
        AddSphere(pet, new Point3D(0.90, 0.30, 0.04), new Vector3D(0.11, 0.12, 0.10), shadeMaterial);
    }

    private void AddEye(Model3DGroup group, Point3D center, Vector3D scale, Material iris, Material pupil, Material highlight)
    {
        AddDynamicSphere(group, center, scale, iris, trackBlink: true, trackEye: true);
        AddDynamicSphere(group, new Point3D(center.X, center.Y - scale.Y * 0.05, center.Z + scale.Z * 0.85), scale * 0.48, pupil, trackBlink: true, trackEye: true);
        AddDynamicSphere(group, new Point3D(center.X - scale.X * 0.27, center.Y + scale.Y * 0.24, center.Z + scale.Z * 1.28), scale * 0.22, highlight, trackBlink: false, trackEye: true, opacity: 0.88);
    }

    private void AddSphere(Model3DGroup group, Point3D center, Vector3D scale, Material material, double opacity = 1.0)
    {
        group.Children.Add(new GeometryModel3D(CreateSphereMesh(24, 14), material)
        {
            BackMaterial = material,
            Transform = CreateTransform(center, scale),
        });
    }

    private void AddDynamicSphere(Model3DGroup group, Point3D center, Vector3D scale, Material material, bool trackBlink, bool trackEye, double opacity = 1.0)
    {
        var scaleTransform = new ScaleTransform3D(scale.X, scale.Y, scale.Z);
        var eyeOffset = new TranslateTransform3D();
        var transform = new Transform3DGroup();
        transform.Children.Add(scaleTransform);
        if (trackEye)
        {
            transform.Children.Add(eyeOffset);
            _pet3DEyeOffsets.Add(eyeOffset);
        }

        transform.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
        if (trackBlink)
        {
            _pet3DEyeScales.Add((scaleTransform, scale.Y));
        }

        group.Children.Add(new GeometryModel3D(CreateSphereMesh(24, 14), material)
        {
            BackMaterial = material,
            Transform = transform
        });
    }

    private void AddCylinder(Model3DGroup group, Point3D center, Vector3D scale, Material material, Vector3D? rotation = null, double opacity = 1.0)
    {
        group.Children.Add(new GeometryModel3D(CreateCylinderMesh(24), material)
        {
            BackMaterial = material,
            Transform = CreateTransform(center, scale, rotation ?? new Vector3D())
        });
    }

    private void AddCone(Model3DGroup group, Point3D center, Vector3D scale, Material material, Vector3D? rotation = null)
    {
        group.Children.Add(new GeometryModel3D(CreateConeMesh(28), material)
        {
            BackMaterial = material,
            Transform = CreateTransform(center, scale, rotation ?? new Vector3D())
        });
    }

    private void AddBox(Model3DGroup group, Point3D center, Vector3D scale, Material material, double opacity = 1.0)
    {
        group.Children.Add(new GeometryModel3D(CreateBoxMesh(), material)
        {
            BackMaterial = material,
            Transform = CreateTransform(center, scale)
        });
    }

    private static Transform3D CreateTransform(Point3D center, Vector3D scale, Vector3D? rotation = null)
    {
        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(scale.X, scale.Y, scale.Z));

        var rotate = rotation ?? new Vector3D();
        if (Math.Abs(rotate.X) > 0.001)
        {
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), rotate.X)));
        }

        if (Math.Abs(rotate.Y) > 0.001)
        {
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), rotate.Y)));
        }

        if (Math.Abs(rotate.Z) > 0.001)
        {
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), rotate.Z)));
        }

        transform.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
        return transform;
    }

    private static Material CreateMaterial(System.Windows.Media.Color color, double specularPower)
    {
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 245)), specularPower));
        return material;
    }

    private static Material CreateEmissiveMaterial(System.Windows.Media.Color color)
    {
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        material.Children.Add(new EmissiveMaterial(new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, color.R, color.G, color.B))));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(System.Windows.Media.Colors.White), 110));
        return material;
    }

    private static MeshGeometry3D CreateSphereMesh(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        for (var stack = 0; stack <= stacks; stack++)
        {
            var phi = -Math.PI / 2.0 + Math.PI * stack / stacks;
            var y = Math.Sin(phi);
            var radius = Math.Cos(phi);

            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = 2.0 * Math.PI * slice / slices;
                var x = radius * Math.Cos(theta);
                var z = radius * Math.Sin(theta);
                mesh.Positions.Add(new Point3D(x, y, z));
                mesh.Normals.Add(new Vector3D(x, y, z));
            }
        }

        for (var stack = 0; stack < stacks; stack++)
        {
            for (var slice = 0; slice < slices; slice++)
            {
                var first = stack * (slices + 1) + slice;
                var second = first + slices + 1;
                mesh.TriangleIndices.Add(first);
                mesh.TriangleIndices.Add(second);
                mesh.TriangleIndices.Add(first + 1);
                mesh.TriangleIndices.Add(first + 1);
                mesh.TriangleIndices.Add(second);
                mesh.TriangleIndices.Add(second + 1);
            }
        }

        return mesh;
    }

    private static MeshGeometry3D CreateCylinderMesh(int sides)
    {
        var mesh = new MeshGeometry3D();
        for (var i = 0; i <= sides; i++)
        {
            var theta = 2.0 * Math.PI * i / sides;
            var x = Math.Cos(theta);
            var z = Math.Sin(theta);
            mesh.Positions.Add(new Point3D(x, -1, z));
            mesh.Positions.Add(new Point3D(x, 1, z));
            mesh.Normals.Add(new Vector3D(x, 0, z));
            mesh.Normals.Add(new Vector3D(x, 0, z));
        }

        for (var i = 0; i < sides; i++)
        {
            var first = i * 2;
            mesh.TriangleIndices.Add(first);
            mesh.TriangleIndices.Add(first + 1);
            mesh.TriangleIndices.Add(first + 2);
            mesh.TriangleIndices.Add(first + 2);
            mesh.TriangleIndices.Add(first + 1);
            mesh.TriangleIndices.Add(first + 3);
        }

        var bottomCenter = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(0, -1, 0));
        mesh.Normals.Add(new Vector3D(0, -1, 0));
        var topCenter = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(0, 1, 0));
        mesh.Normals.Add(new Vector3D(0, 1, 0));

        for (var i = 0; i < sides; i++)
        {
            var first = i * 2;
            mesh.TriangleIndices.Add(bottomCenter);
            mesh.TriangleIndices.Add(first + 2);
            mesh.TriangleIndices.Add(first);
            mesh.TriangleIndices.Add(topCenter);
            mesh.TriangleIndices.Add(first + 1);
            mesh.TriangleIndices.Add(first + 3);
        }

        return mesh;
    }

    private static MeshGeometry3D CreateConeMesh(int sides)
    {
        var mesh = new MeshGeometry3D();
        var apex = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(0, 1, 0));
        mesh.Normals.Add(new Vector3D(0, 1, 0));

        for (var i = 0; i <= sides; i++)
        {
            var theta = 2.0 * Math.PI * i / sides;
            var x = Math.Cos(theta);
            var z = Math.Sin(theta);
            mesh.Positions.Add(new Point3D(x, -1, z));
            mesh.Normals.Add(new Vector3D(x, 0.5, z));
        }

        for (var i = 1; i <= sides; i++)
        {
            mesh.TriangleIndices.Add(apex);
            mesh.TriangleIndices.Add(i);
            mesh.TriangleIndices.Add(i + 1);
        }

        var baseCenter = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(0, -1, 0));
        mesh.Normals.Add(new Vector3D(0, -1, 0));
        for (var i = 1; i <= sides; i++)
        {
            mesh.TriangleIndices.Add(baseCenter);
            mesh.TriangleIndices.Add(i + 1);
            mesh.TriangleIndices.Add(i);
        }

        return mesh;
    }

    private static MeshGeometry3D CreateBoxMesh()
    {
        var mesh = new MeshGeometry3D();
        var points = new[]
        {
            new Point3D(-1, -1, 1), new Point3D(1, -1, 1), new Point3D(1, 1, 1), new Point3D(-1, 1, 1),
            new Point3D(1, -1, -1), new Point3D(-1, -1, -1), new Point3D(-1, 1, -1), new Point3D(1, 1, -1),
            new Point3D(-1, -1, -1), new Point3D(-1, -1, 1), new Point3D(-1, 1, 1), new Point3D(-1, 1, -1),
            new Point3D(1, -1, 1), new Point3D(1, -1, -1), new Point3D(1, 1, -1), new Point3D(1, 1, 1),
            new Point3D(-1, 1, 1), new Point3D(1, 1, 1), new Point3D(1, 1, -1), new Point3D(-1, 1, -1),
            new Point3D(-1, -1, -1), new Point3D(1, -1, -1), new Point3D(1, -1, 1), new Point3D(-1, -1, 1)
        };

        var normals = new[]
        {
            new Vector3D(0, 0, 1), new Vector3D(0, 0, -1), new Vector3D(-1, 0, 0),
            new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), new Vector3D(0, -1, 0)
        };

        foreach (var point in points)
        {
            mesh.Positions.Add(point);
        }

        foreach (var normal in normals)
        {
            for (var i = 0; i < 4; i++)
            {
                mesh.Normals.Add(normal);
            }
        }

        for (var face = 0; face < 6; face++)
        {
            var offset = face * 4;
            mesh.TriangleIndices.Add(offset);
            mesh.TriangleIndices.Add(offset + 1);
            mesh.TriangleIndices.Add(offset + 2);
            mesh.TriangleIndices.Add(offset);
            mesh.TriangleIndices.Add(offset + 2);
            mesh.TriangleIndices.Add(offset + 3);
        }

        return mesh;
    }

    private bool IsMouseNearPet(double mouseX, double mouseY)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var left = Left * dpi.DpiScaleX;
        var top = Top * dpi.DpiScaleY;
        var width = Width * dpi.DpiScaleX;
        var height = Height * dpi.DpiScaleY;
        const double padding = 24;
        var petLeft = left + _viewModel.PetOffsetX * dpi.DpiScaleX;
        var petTop = top + _viewModel.PetOffsetY * dpi.DpiScaleY;
        return mouseX >= petLeft - padding
            && mouseX <= petLeft + PetSize * dpi.DpiScaleX + padding
            && mouseY >= petTop - padding
            && mouseY <= petTop + PetSize * dpi.DpiScaleY + padding
            && width > 0
            && height > 0;
    }

    private void ScheduleNextBlink()
    {
        _nextBlinkAt = DateTime.UtcNow.AddMilliseconds(_random.Next(1800, 5600));
    }

    private void ApplySizeForCurrentDpi()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        Width = Math.Round(Math.Max(MinHabitatWidth, _settings.Width / dpi.DpiScaleX));
        Height = Math.Round(Math.Max(MinHabitatHeight, _settings.Height / dpi.DpiScaleY));
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_settings.Locked)
        {
            return;
        }

        var edge = (sender as Thumb)?.Tag?.ToString() ?? "SE";
        var oldRight = Left + Width;
        var oldBottom = Top + Height;
        var newLeft = Left;
        var newTop = Top;
        var newWidth = Width;
        var newHeight = Height;

        if (edge.Contains('E'))
        {
            newWidth = Width + e.HorizontalChange;
        }

        if (edge.Contains('S'))
        {
            newHeight = Height + e.VerticalChange;
        }

        if (edge.Contains('W'))
        {
            newWidth = Width - e.HorizontalChange;
            newLeft = Left + e.HorizontalChange;
        }

        if (edge.Contains('N'))
        {
            newHeight = Height - e.VerticalChange;
            newTop = Top + e.VerticalChange;
        }

        if (newWidth < MinHabitatWidth)
        {
            newWidth = MinHabitatWidth;
            if (edge.Contains('W'))
            {
                newLeft = oldRight - MinHabitatWidth;
            }
        }

        if (newHeight < MinHabitatHeight)
        {
            newHeight = MinHabitatHeight;
            if (edge.Contains('N'))
            {
                newTop = oldBottom - MinHabitatHeight;
            }
        }

        Left = Math.Round(newLeft);
        Top = Math.Round(newTop);
        Width = Math.Round(newWidth);
        Height = Math.Round(newHeight);
        ClampPetToHabitat();
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        SaveCurrentPosition();
    }

    private void UpdatePetMotion(DateTime now)
    {
        _motionPhase += 0.18;

        if (!_settings.MoveAroundHabitat || _viewModel.IsSleeping)
        {
            _viewModel.IsWalking = false;
            _viewModel.BodyBobY = _viewModel.IsSleeping ? Math.Sin(_motionPhase * 0.35) * 0.8 : 0;
            _viewModel.BodyTilt = Math.Sin(_motionPhase * 0.35) * 0.8;
            _viewModel.LeftFootOffsetX *= 0.72;
            _viewModel.LeftFootOffsetY *= 0.72;
            _viewModel.RightFootOffsetX *= 0.72;
            _viewModel.RightFootOffsetY *= 0.72;
            _viewModel.TailWag = Math.Sin(_motionPhase * 0.45) * 5;
            ClampPetToHabitat();
            return;
        }

        if (now < _hopUntil)
        {
            _viewModel.IsWalking = false;
            _viewModel.BodyBobY = -10 * Math.Sin((1 - (_hopUntil - now).TotalMilliseconds / 420.0) * Math.PI);
            _viewModel.BodyTilt = Math.Sin(_motionPhase * 1.7) * 3;
            _viewModel.LeftFootOffsetX = -3;
            _viewModel.RightFootOffsetX = 3;
            _viewModel.LeftFootOffsetY = 2;
            _viewModel.RightFootOffsetY = 2;
            _viewModel.TailWag = Math.Sin(_motionPhase * 4.5) * 12;
            return;
        }

        if (now < _pauseUntil)
        {
            _viewModel.IsWalking = false;
            _viewModel.BodyBobY = 0;
            _viewModel.BodyTilt = Math.Sin(_motionPhase * 0.9) * 1.2;
            _viewModel.LeftFootOffsetX *= 0.68;
            _viewModel.LeftFootOffsetY *= 0.68;
            _viewModel.RightFootOffsetX *= 0.68;
            _viewModel.RightFootOffsetY *= 0.68;
            _viewModel.TailWag = Math.Sin(_motionPhase * 2.0) * 8;
            if (_random.NextDouble() < 0.006)
            {
                _hopUntil = now.AddMilliseconds(420);
            }
            return;
        }

        if (now >= _nextMoveTargetAt)
        {
            PickMoveTarget();
            ScheduleNextMoveTarget();
        }

        var dx = _targetPetX - _viewModel.PetOffsetX;
        var dy = _targetPetY - _viewModel.PetOffsetY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > 0.5)
        {
            var step = Math.Min(distance, 1.45);
            _viewModel.PetOffsetX += dx / distance * step;
            _viewModel.PetOffsetY += dy / distance * step;
            var gait = Math.Sin(_motionPhase * 3.2);
            var oppositeGait = Math.Sin(_motionPhase * 3.2 + Math.PI);
            _viewModel.IsWalking = true;
            _viewModel.FaceScaleX = dx < 0 ? -1 : 1;
            _viewModel.BodyBobY = -Math.Abs(gait) * 1.5;
            _viewModel.BodyTilt = Math.Clamp(dx * 0.018, -2.2, 2.2);
            _viewModel.LeftFootOffsetX = gait * 5.2;
            _viewModel.LeftFootOffsetY = Math.Max(0, gait) * -5.0;
            _viewModel.RightFootOffsetX = oppositeGait * 5.2;
            _viewModel.RightFootOffsetY = Math.Max(0, oppositeGait) * -5.0;
            _viewModel.TailWag = Math.Sin(_motionPhase * 3.3) * 11;
        }
        else
        {
            _pauseUntil = now.AddMilliseconds(_random.Next(700, 1800));
            _viewModel.IsWalking = false;
            _viewModel.BodyBobY = 0;
            _viewModel.BodyTilt = Math.Sin(_motionPhase * 0.35) * 0.8;
            _viewModel.LeftFootOffsetX *= 0.72;
            _viewModel.LeftFootOffsetY *= 0.72;
            _viewModel.RightFootOffsetX *= 0.72;
            _viewModel.RightFootOffsetY *= 0.72;
            _viewModel.TailWag = Math.Sin(_motionPhase * 0.45) * 5;
        }

        ClampPetToHabitat();
    }

    private void PlacePetInsideHabitat()
    {
        _viewModel.PetOffsetX = Math.Max(0, (ActualWidth - PetSize) / 2.0);
        _viewModel.PetOffsetY = Math.Max(0, ActualHeight - PetSize - 18);
        PickMoveTarget();
    }

    private void PickMoveTarget()
    {
        var maxX = Math.Max(0, ActualWidth - PetSize);
        var maxY = Math.Max(0, ActualHeight - PetSize);
        if (IsMouseNearPet(_mouseTrackingService.GetCursorPosition().X, _mouseTrackingService.GetCursorPosition().Y) && _random.NextDouble() < 0.55)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var cursor = _mouseTrackingService.GetCursorPosition();
            _targetPetX = Math.Clamp(cursor.X / dpi.DpiScaleX - Left - PetSize / 2.0, 0, maxX);
            _targetPetY = Math.Clamp(cursor.Y / dpi.DpiScaleY - Top - PetSize / 2.0, 0, maxY);
        }
        else
        {
            _targetPetX = _random.NextDouble() * maxX;
            _targetPetY = _random.NextDouble() * maxY;
        }
    }

    private void ScheduleNextMoveTarget()
    {
        _nextMoveTargetAt = DateTime.UtcNow.AddMilliseconds(_random.Next(1300, 3400));
    }

    private void ClampPetToHabitat()
    {
        var maxX = Math.Max(0, ActualWidth - PetSize);
        var maxY = Math.Max(0, ActualHeight - PetSize);
        _viewModel.PetOffsetX = Math.Clamp(_viewModel.PetOffsetX, 0, maxX);
        _viewModel.PetOffsetY = Math.Clamp(_viewModel.PetOffsetY, 0, maxY);
    }

    private sealed class PixelPetState
    {
        public PixelPetSprite Sprite { get; init; } = null!;
        public ScaleTransform Facing { get; init; } = null!;
        public TranslateTransform Bob { get; init; } = null!;
        public FrameworkElement Emote { get; init; } = null!;
        public TextBlock EmoteText { get; init; } = null!;
        public string[] Emotes { get; init; } = [];
        public double X { get; set; }
        public double Y { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double HeadingAngle { get; set; }
        public double WanderPhase { get; set; }
        public double Speed { get; init; }
        public double Phase { get; set; }
        public bool IsWalking { get; set; }
        public string Pose { get; set; } = "Idle";
        public string Direction { get; set; } = "Down";
        public string DirectionCandidate { get; set; } = "Down";
        public DateTime NextDirectionAt { get; set; }
        public DateTime NextTargetAt { get; set; }
        public DateTime PauseUntil { get; set; }
        public DateTime NextEmoteAt { get; set; }
        public DateTime EmoteUntil { get; set; }
        public DateTime HopUntil { get; set; }
        public DateTime NextPoseAt { get; set; }
    }

    private sealed record PetOption(string Name, string Variant, string[] Emotes);
}

