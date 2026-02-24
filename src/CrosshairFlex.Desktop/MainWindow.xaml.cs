using CrosshairFlex.Desktop.Models;
using CrosshairFlex.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;

namespace CrosshairFlex.Desktop;

public partial class MainWindow : Window
{
    private const string SharePrefix = "crosshairflex://profile/";

    private sealed class SharedProfilePayload
    {
        public string Version { get; set; } = "1.0.0";
        public CrosshairProfile Profile { get; set; } = CrosshairProfile.CreateDefault();
    }

    private enum Section
    {
        Home,
        Add,
        Keybinds,
        Share,
        Profile
    }

    private enum HomeLayoutMode
    {
        Vertical,
        Horizontal
    }

    private readonly ConfigService _configService = new();
    private readonly StartupService _startupService = new();
    private readonly HotkeyService _hotkeyService = new();
    private readonly OverlayService _overlayService = new();
    private readonly LocalizationService _localizationService = new();
    private readonly TrayService _trayService;

    private AppConfig _config = new();
    private ObservableCollection<CrosshairProfile> _profiles = [];
    private bool _updatingUi;
    private string? _editingProfileId;
    private CrosshairProfile _draftProfile = new();
    private HomeLayoutMode _homeLayoutMode = HomeLayoutMode.Vertical;

    public MainWindow()
    {
        InitializeComponent();
        _trayService = new TrayService(ShowFromTray, ToggleOverlayFromTray, ExitFromTray);

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hotkeyService.Initialize(this, ActivateProfileById, () => _overlayService.IsVisibleForInput);
        if (_profiles.Count > 0)
        {
            SaveAndReloadHotkeys();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _config = _configService.Load();
        _localizationService.Load(_config.Language);
        Title = _localizationService.T("app.title", "CrosshairFlex");

        if (_config.Profiles.Count == 0)
        {
            _config.Profiles.Add(CrosshairProfile.CreateDefault());
        }

        _profiles = new ObservableCollection<CrosshairProfile>(_config.Profiles);
        for (var i = 0; i < _profiles.Count; i++)
        {
            NormalizeProfileHotkeys(_profiles[i]);
        }
        HomeProfilesListBox.ItemsSource = _profiles;
        HomeProfilesHorizontalListBox.ItemsSource = _profiles;
        KeybindProfilesListBox.ItemsSource = _profiles;
        ShareProfileComboBox.ItemsSource = _profiles;
        ShareProfileComboBox.DisplayMemberPath = nameof(CrosshairProfile.Name);

        var activeProfile = GetSelectedOrFallbackProfile();
        _config.LastProfileId = activeProfile.Id;

        if (!_config.FirstLaunchCompleted)
        {
            var onboarding = new OnboardingWindow(_config.Language) { Owner = this };
            onboarding.ShowDialog();
            _config.FirstLaunchCompleted = true;
        }

        _updatingUi = true;
        SetLanguageSelection(_config.Language);
        ThemeToggleCheckBox.IsChecked = string.Equals(_config.Theme, "light", StringComparison.OrdinalIgnoreCase);
        StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;
        TempOnRightMouseCheckBox.IsChecked = _config.EnableTemporaryOnRightMouse;
        TempOnLeftMouseCheckBox.IsChecked = _config.EnableTemporaryOnLeftMouse;
        SafeModeCheckBox.IsChecked = _config.SafeMode;
        _updatingUi = false;

        SelectProfile(activeProfile);
        SetHomeLayoutMode(HomeLayoutMode.Vertical);
        ShowSection(Section.Home);
        ApplyLocalizedTexts();
        ApplyTheme();
        UpdateSafetyText();
        SaveAndReloadHotkeys();
        PushBehaviorToServices();
        VersionText.Text = $"Version {_config.Version}";
        SettingsStatusText.Text = "Settings loaded.";
    }

    private void ShowFromTray()
    {
        Dispatcher.Invoke(RestoreAndActivate);
    }

    private void ToggleOverlayFromTray()
    {
        Dispatcher.Invoke(() => _overlayService.ToggleOverlayVisibility());
    }

    private void ExitFromTray()
    {
        Dispatcher.Invoke(Close);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
    }

    public void RestoreAndActivate()
    {
        ShowInTaskbar = true;
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _trayService.Dispose();
        _hotkeyService.Dispose();
        _overlayService.Dispose();
    }

    private void ActivateProfileById(string profileId)
    {
        Dispatcher.Invoke(() =>
        {
            for (var i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i].Id == profileId)
                {
                    SelectProfile(_profiles[i]);
                    ApplyVisualSettingsToOverlay();
                    return;
                }
            }
        });
    }

    private CrosshairProfile GetSelectedOrFallbackProfile()
    {
        if (!string.IsNullOrWhiteSpace(_config.LastProfileId))
        {
            for (var i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i].Id == _config.LastProfileId)
                {
                    return _profiles[i];
                }
            }
        }

        return _profiles[0];
    }

    private CrosshairProfile? CurrentProfile =>
        (HomeProfilesListBox.SelectedItem as CrosshairProfile)
        ?? (HomeProfilesHorizontalListBox.SelectedItem as CrosshairProfile);

    private void SelectProfile(CrosshairProfile profile)
    {
        _updatingUi = true;

        HomeProfilesListBox.SelectedItem = profile;
        HomeProfilesHorizontalListBox.SelectedItem = profile;
        KeybindProfilesListBox.SelectedItem = profile;
        ShareProfileComboBox.SelectedItem = profile;
        _editingProfileId = profile.Id;

        AddProfileNameTextBox.Text = profile.Name;
        AddShapeComboBox.SelectedIndex = (int)profile.Shape;
        AddSizeSlider.Value = profile.Size;
        AddThicknessSlider.Value = profile.Thickness;
        AddGapSlider.Value = profile.Gap;
        AddPumpRoundSlider.Value = profile.PumpCornerRounding;
        AddOpacitySlider.Value = ToOpacitySliderValue(profile.Opacity);
        AddCustomPngTextBox.Text = profile.CustomPngPath;
        UpdateKeybindEditorUi(profile);

        UpdateColorPreview(profile.Red, profile.Green, profile.Blue);

        _updatingUi = false;
    }

    private void UpdateKeybindEditorUi(CrosshairProfile profile)
    {
        NormalizeProfileHotkeys(profile);
        SelectedProfileNameText.Text = profile.Name;
        SelectedProfileKeybindsText.Text = profile.ProfileHotkeys.Count == 0
            ? _localizationService.T("keybinds.none", "No hotkeys assigned.")
            : $"{_localizationService.T("keybinds.list_prefix", "Hotkeys")}: {string.Join(", ", profile.ProfileHotkeys)}";

        KeybindRowsPanel.Children.Clear();
        if (profile.ProfileHotkeys.Count == 0)
        {
            AddHotkeyEditorRow(string.Empty);
            return;
        }

        for (var i = 0; i < profile.ProfileHotkeys.Count; i++)
        {
            AddHotkeyEditorRow(profile.ProfileHotkeys[i]);
        }
    }

    private void AddHotkeyEditorRow(string value)
    {
        var row = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var removeButton = new System.Windows.Controls.Button
        {
            Content = "−",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = _localizationService.T("keybinds.remove_hotkey", "Remove hotkey field"),
            Tag = row
        };
        removeButton.Click += RemoveKeybindRow_OnClick;
        DockPanel.SetDock(removeButton, Dock.Right);

        var textBox = new System.Windows.Controls.TextBox
        {
            Width = 220,
            Height = 34,
            Text = value ?? string.Empty,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var light = string.Equals(_config.Theme, "light", StringComparison.OrdinalIgnoreCase);
        textBox.Background = new SolidColorBrush(light
            ? MediaColor.FromRgb(255, 255, 255)
            : MediaColor.FromRgb(15, 23, 37));
        textBox.Foreground = new SolidColorBrush(light
            ? MediaColor.FromRgb(0, 0, 0)
            : MediaColor.FromRgb(255, 255, 255));
        textBox.BorderBrush = new SolidColorBrush(light
            ? MediaColor.FromRgb(198, 212, 235)
            : MediaColor.FromRgb(39, 54, 78));

        row.Children.Add(removeButton);
        row.Children.Add(textBox);
        KeybindRowsPanel.Children.Add(row);
    }

    private void RemoveKeybindRow_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not DockPanel row)
        {
            return;
        }

        if (KeybindRowsPanel.Children.Count <= 1)
        {
            if (row.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault() is System.Windows.Controls.TextBox textBox)
            {
                textBox.Text = string.Empty;
            }
            return;
        }

        KeybindRowsPanel.Children.Remove(row);
    }

    private void EnterNewProfileMode()
    {
        _editingProfileId = null;
        _draftProfile = new CrosshairProfile
        {
            Name = string.Empty,
            Shape = CrosshairShape.Plus,
            Size = 0,
            Thickness = 0,
            Gap = 0,
            PumpCornerRounding = 0,
            Red = 0,
            Green = 255,
            Blue = 0,
            Opacity = 1.0,
            CustomPngPath = string.Empty,
            ProfileHotkey = string.Empty
        };

        _updatingUi = true;
        AddProfileNameTextBox.Text = _draftProfile.Name;
        AddShapeComboBox.SelectedIndex = (int)_draftProfile.Shape;
        AddSizeSlider.Value = _draftProfile.Size;
        AddThicknessSlider.Value = _draftProfile.Thickness;
        AddGapSlider.Value = _draftProfile.Gap;
        AddPumpRoundSlider.Value = _draftProfile.PumpCornerRounding;
        AddOpacitySlider.Value = ToOpacitySliderValue(_draftProfile.Opacity);
        AddCustomPngTextBox.Text = _draftProfile.CustomPngPath;
        UpdateColorPreview(_draftProfile.Red, _draftProfile.Green, _draftProfile.Blue);
        _updatingUi = false;
    }

    private void UpdateColorPreview(byte red, byte green, byte blue)
    {
        AddColorPreviewBorder.Background = new SolidColorBrush(MediaColor.FromRgb(red, green, blue));
        AddColorText.Text = $"#{red:X2}{green:X2}{blue:X2}";
    }

    private void PullAddEditorToProfile(CrosshairProfile profile)
    {
        profile.Name = string.IsNullOrWhiteSpace(AddProfileNameTextBox.Text) ? profile.Name : AddProfileNameTextBox.Text.Trim();
        profile.Shape = (CrosshairShape)Math.Clamp(AddShapeComboBox.SelectedIndex, 0, 4);
        profile.Size = (int)AddSizeSlider.Value;
        profile.Thickness = (int)AddThicknessSlider.Value;
        profile.Gap = (int)AddGapSlider.Value;
        profile.PumpCornerRounding = (int)AddPumpRoundSlider.Value;
        profile.Opacity = ToProfileOpacityValue(AddOpacitySlider.Value);
        profile.CustomPngPath = AddCustomPngTextBox.Text.Trim();
    }

    private static double ToOpacitySliderValue(double profileOpacity)
    {
        return Math.Clamp(1.0 - profileOpacity, 0.0, 1.0);
    }

    private static double ToProfileOpacityValue(double sliderValue)
    {
        return Math.Clamp(1.0 - sliderValue, 0.0, 1.0);
    }

    private void ShowSection(Section section)
    {
        HomePanel.Visibility = section == Section.Home ? Visibility.Visible : Visibility.Collapsed;
        AddPanel.Visibility = section == Section.Add ? Visibility.Visible : Visibility.Collapsed;
        KeybindsPanel.Visibility = section == Section.Keybinds ? Visibility.Visible : Visibility.Collapsed;
        SharePanel.Visibility = section == Section.Share ? Visibility.Visible : Visibility.Collapsed;
        ProfilePanel.Visibility = section == Section.Profile ? Visibility.Visible : Visibility.Collapsed;

        ApplyNavHighlight(HomeNavButton, section == Section.Home);
        ApplyNavHighlight(AddNavButton, section == Section.Add);
        ApplyNavHighlight(KeybindNavButton, section == Section.Keybinds);
        ApplyNavHighlight(ShareNavButton, section == Section.Share);
        ApplyNavHighlight(ProfileNavButton, section == Section.Profile);
    }

    private void SetHomeLayoutMode(HomeLayoutMode mode)
    {
        _homeLayoutMode = mode;
        var horizontal = mode == HomeLayoutMode.Horizontal;
        HomeProfilesListBox.Visibility = horizontal ? Visibility.Collapsed : Visibility.Visible;
        HomeProfilesHorizontalListBox.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        HomeViewModeButton.Content = horizontal
            ? _localizationService.T("home.view.horizontal", "View: Horizontal")
            : _localizationService.T("home.view.vertical", "View: Vertical");
    }

    private void ApplyNavHighlight(System.Windows.Controls.Button button, bool active)
    {
        var light = string.Equals(_config.Theme, "light", StringComparison.OrdinalIgnoreCase);
        button.Background = active
            ? new SolidColorBrush(MediaColor.FromRgb(37, 201, 176))
            : new SolidColorBrush(MediaColor.FromRgb(19, 30, 49));
        button.Foreground = active
            ? new SolidColorBrush(light ? MediaColor.FromRgb(0, 0, 0) : MediaColor.FromRgb(255, 255, 255))
            : new SolidColorBrush(light ? MediaColor.FromRgb(37, 53, 76) : MediaColor.FromRgb(159, 178, 204));
    }

    private void ShowHome_Click(object sender, RoutedEventArgs e) => ShowSection(Section.Home);
    private void ShowAdd_Click(object sender, RoutedEventArgs e)
    {
        EnterNewProfileMode();
        ShowSection(Section.Add);
    }
    private void ShowKeybinds_Click(object sender, RoutedEventArgs e) => ShowSection(Section.Keybinds);
    private void ShowShare_Click(object sender, RoutedEventArgs e) => ShowSection(Section.Share);
    private void ShowProfile_Click(object sender, RoutedEventArgs e) => ShowSection(Section.Profile);

    private void HomeViewMode_OnClick(object sender, RoutedEventArgs e)
    {
        SetHomeLayoutMode(_homeLayoutMode == HomeLayoutMode.Horizontal
            ? HomeLayoutMode.Vertical
            : HomeLayoutMode.Horizontal);
    }

    private void CopyShareCode_OnClick(object sender, RoutedEventArgs e)
    {
        var shareProfile = ShareProfileComboBox.SelectedItem as CrosshairProfile ?? CurrentProfile;
        if (shareProfile is null)
        {
            SetShareStatus("No profile selected.", false);
            return;
        }

        var payload = new SharedProfilePayload
        {
            Version = _config.Version,
            Profile = CloneProfile(shareProfile)
        };

        payload.Profile.Id = string.Empty;
        payload.Profile.ProfileHotkey = string.Empty;
        payload.Profile.ProfileHotkeys = [];

        var json = JsonSerializer.Serialize(payload);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var shareCode = $"{SharePrefix}{encoded}";

        System.Windows.Clipboard.SetText(shareCode);
        SetShareStatus("Share code copied to clipboard.", true);
        CustomDialogWindow.ShowInfo(this, "Share", "Link copied.");
    }

    private void ImportShareCode_OnClick(object sender, RoutedEventArgs e)
    {
        var raw = ShareCodeTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw) && System.Windows.Clipboard.ContainsText())
        {
            raw = System.Windows.Clipboard.GetText().Trim();
            ShareCodeTextBox.Text = raw;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            SetShareStatus("Paste a share code first.", false);
            return;
        }

        if (raw.StartsWith(SharePrefix, StringComparison.OrdinalIgnoreCase))
        {
            raw = raw.Substring(SharePrefix.Length);
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw));

            CrosshairProfile profileToImport;
            try
            {
                var payload = JsonSerializer.Deserialize<SharedProfilePayload>(json);
                if (payload?.Profile is null)
                {
                    SetShareStatus("Invalid share payload.", false);
                    return;
                }

                profileToImport = payload.Profile;
            }
            catch
            {
                var legacyProfile = JsonSerializer.Deserialize<CrosshairProfile>(json);
                if (legacyProfile is null)
                {
                    SetShareStatus("Invalid share payload.", false);
                    return;
                }

                profileToImport = legacyProfile;
            }

            NormalizeImportedProfile(profileToImport);
            profileToImport.Id = Guid.NewGuid().ToString("N");
            profileToImport.ProfileHotkey = string.Empty;
            profileToImport.ProfileHotkeys = [];
            profileToImport.Name = BuildUniqueImportedName(profileToImport.Name);

            _profiles.Add(profileToImport);
            SelectProfile(profileToImport);
            ShowSection(Section.Home);
            ApplyAndSave();

            SetShareStatus($"Imported profile '{profileToImport.Name}'.", true);
        }
        catch
        {
            SetShareStatus("Invalid share code format.", false);
        }
    }

    private static CrosshairProfile CloneProfile(CrosshairProfile source)
    {
        return new CrosshairProfile
        {
            Id = source.Id,
            Name = source.Name,
            Shape = source.Shape,
            Size = source.Size,
            Thickness = source.Thickness,
            Gap = source.Gap,
            PumpCornerRounding = source.PumpCornerRounding,
            Red = source.Red,
            Green = source.Green,
            Blue = source.Blue,
            Opacity = source.Opacity,
            CustomPngPath = source.CustomPngPath,
            ProfileHotkey = source.ProfileHotkey,
            ProfileHotkeys = [.. source.ProfileHotkeys]
        };
    }

    private string BuildUniqueImportedName(string baseName)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Imported Profile" : baseName.Trim();
        var candidate = normalized;
        var i = 2;

        while (_profiles.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{normalized} ({i})";
            i++;
        }

        return candidate;
    }

    private static void NormalizeImportedProfile(CrosshairProfile profile)
    {
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "Imported Profile" : profile.Name.Trim();
        profile.Size = Math.Clamp(profile.Size, 6, 230);
        profile.Thickness = Math.Clamp(profile.Thickness, 1, 40);
        profile.Gap = Math.Clamp(profile.Gap, 0, 120);
        profile.PumpCornerRounding = Math.Clamp(profile.PumpCornerRounding, 0, 60);
        profile.Opacity = Math.Clamp(profile.Opacity, 0.0, 1.0);
    }

    private void SetShareStatus(string message, bool success)
    {
        ShareStatusText.Text = message;
        ShareStatusText.Foreground = success
            ? new SolidColorBrush(MediaColor.FromRgb(120, 220, 170))
            : new SolidColorBrush(MediaColor.FromRgb(255, 130, 130));
    }

    private void EditProfileFromHome_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var profileId = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        for (var i = 0; i < _profiles.Count; i++)
        {
            if (_profiles[i].Id == profileId)
            {
                SelectProfile(_profiles[i]);
                ShowSection(Section.Add);
                return;
            }
        }
    }

    private void DeleteProfileFromHome_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var profileId = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        DeleteProfileById(profileId);
    }

    private void HomeProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        var selected = (sender as System.Windows.Controls.ListBox)?.SelectedItem as CrosshairProfile;
        if (selected is null)
        {
            return;
        }

        SelectProfile(selected);
        ApplyVisualSettingsToOverlay();
        HomeProfilesListBox.Items.Refresh();
        HomeProfilesHorizontalListBox.Items.Refresh();
        ShareProfileComboBox.Items.Refresh();
        SaveAndReloadHotkeys();
    }

    private void CreateProfile_OnClick(object sender, RoutedEventArgs e)
    {
        EnterNewProfileMode();
    }

    private void SaveProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (_editingProfileId is null)
        {
            var profile = CrosshairProfile.CreateDefault();
            PullAddEditorToProfile(profile);
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                profile.Name = $"Profile {_profiles.Count + 1}";
            }

            _profiles.Add(profile);
            SelectProfile(profile);
            HomeProfilesListBox.Items.Refresh();
            HomeProfilesHorizontalListBox.Items.Refresh();
            KeybindProfilesListBox.Items.Refresh();
            ShareProfileComboBox.Items.Refresh();
            ApplyAndSave();
            SettingsStatusText.Text = "Profile created.";
            SettingsStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(120, 220, 170));
            return;
        }

        var existingProfile = _profiles.FirstOrDefault(p => p.Id == _editingProfileId);
        if (existingProfile is null)
        {
            return;
        }

        PullAddEditorToProfile(existingProfile);
        SelectProfile(existingProfile);
        HomeProfilesListBox.Items.Refresh();
        HomeProfilesHorizontalListBox.Items.Refresh();
        KeybindProfilesListBox.Items.Refresh();
        ShareProfileComboBox.Items.Refresh();
        ApplyAndSave();
    }

    private void DeleteProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (CurrentProfile is null)
        {
            return;
        }

        DeleteProfileById(CurrentProfile.Id);
    }

    private void DeleteProfileById(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null)
        {
            return;
        }

        if (_profiles.Count <= 1)
        {
            SettingsStatusText.Text = "At least one profile is required.";
            SettingsStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 130, 130));
            return;
        }

        var deleteConfirmation = CustomDialogWindow.ShowConfirm(
            this,
            "Confirm Delete",
            $"Delete profile '{profile.Name}'?",
            "Delete",
            "Cancel");
        if (!deleteConfirmation)
        {
            return;
        }

        var index = _profiles.IndexOf(profile);
        if (index < 0)
        {
            return;
        }

        _profiles.RemoveAt(index);
        var fallbackIndex = Math.Clamp(index, 0, _profiles.Count - 1);
        var nextProfile = _profiles[fallbackIndex];

        SelectProfile(nextProfile);
        HomeProfilesListBox.Items.Refresh();
        HomeProfilesHorizontalListBox.Items.Refresh();
        KeybindProfilesListBox.Items.Refresh();
        ShareProfileComboBox.Items.Refresh();
        ApplyAndSave();

        SettingsStatusText.Text = "Profile deleted.";
        SettingsStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(120, 220, 170));
    }

    private void AddEditor_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        if (_editingProfileId is null)
        {
            PullAddEditorToProfile(_draftProfile);
            return;
        }

        var profile = _profiles.FirstOrDefault(p => p.Id == _editingProfileId);
        if (profile is null)
        {
            return;
        }

        PullAddEditorToProfile(profile);
        HomeProfilesListBox.Items.Refresh();
        HomeProfilesHorizontalListBox.Items.Refresh();
        ShareProfileComboBox.Items.Refresh();
        ApplyVisualSettingsToOverlay();
    }

    private void PickColor_OnClick(object sender, RoutedEventArgs e)
    {
        var target = _editingProfileId is null
            ? _draftProfile
            : _profiles.FirstOrDefault(p => p.Id == _editingProfileId);
        if (target is null)
        {
            return;
        }

        var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(target.Red, target.Green, target.Blue)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            target.Red = dialog.Color.R;
            target.Green = dialog.Color.G;
            target.Blue = dialog.Color.B;
            UpdateColorPreview(target.Red, target.Green, target.Blue);

            if (_editingProfileId is not null)
            {
                ApplyAndSave();
            }
        }
    }

    private void BrowsePng_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PNG image|*.png",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            AddCustomPngTextBox.Text = dialog.FileName;
            AddShapeComboBox.SelectedIndex = (int)CrosshairShape.CustomPng;
            if (_editingProfileId is null)
            {
                _draftProfile.Shape = CrosshairShape.CustomPng;
            }
            else if (CurrentProfile is not null)
            {
                CurrentProfile.Shape = CrosshairShape.CustomPng;
                ApplyAndSave();
            }
        }
    }

    private void KeybindProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        if (KeybindProfilesListBox.SelectedItem is CrosshairProfile profile)
        {
            SelectProfile(profile);
        }
    }

    private void SaveKeybind_OnClick(object sender, RoutedEventArgs e)
    {
        if (KeybindProfilesListBox.SelectedItem is not CrosshairProfile profile)
        {
            return;
        }

        var keys = GetKeybindEditorValues();
        if (keys.Count == 0)
        {
            KeybindStatusText.Text = "Enter at least one hotkey.";
            KeybindStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 130, 130));
            return;
        }

        if (keys.Count != keys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            KeybindStatusText.Text = "Duplicate hotkeys in this profile are not allowed.";
            KeybindStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 130, 130));
            return;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            if (IsHotkeyUsedByOtherProfile(keys[i], profile.Id))
            {
                KeybindStatusText.Text = "Duplicate key already used by another profile.";
                KeybindStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 130, 130));
                return;
            }
        }

        profile.ProfileHotkeys = keys;
        profile.ProfileHotkey = keys[0];
        HomeProfilesListBox.Items.Refresh();
        HomeProfilesHorizontalListBox.Items.Refresh();
        KeybindProfilesListBox.Items.Refresh();
        ShareProfileComboBox.Items.Refresh();
        ApplyAndSave();
        UpdateKeybindEditorUi(profile);
    }

    private void AddKeybind_OnClick(object sender, RoutedEventArgs e)
    {
        if (KeybindProfilesListBox.SelectedItem is not CrosshairProfile)
        {
            return;
        }

        AddHotkeyEditorRow(string.Empty);
    }

    private List<string> GetKeybindEditorValues()
    {
        var keys = new List<string>();
        foreach (var row in KeybindRowsPanel.Children.OfType<DockPanel>())
        {
            var textBox = row.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault();
            if (textBox is null)
            {
                continue;
            }

            var key = textBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private bool IsHotkeyUsedByOtherProfile(string key, string profileId)
    {
        for (var i = 0; i < _profiles.Count; i++)
        {
            if (_profiles[i].Id == profileId)
            {
                continue;
            }

            NormalizeProfileHotkeys(_profiles[i]);
            if (_profiles[i].ProfileHotkeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static void NormalizeProfileHotkeys(CrosshairProfile profile)
    {
        if (profile.ProfileHotkeys.Count == 0 && !string.IsNullOrWhiteSpace(profile.ProfileHotkey))
        {
            profile.ProfileHotkeys.Add(profile.ProfileHotkey.Trim());
        }

        var normalized = profile.ProfileHotkeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        profile.ProfileHotkeys = normalized;
        profile.ProfileHotkey = normalized.Count > 0 ? normalized[0] : string.Empty;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || LanguageComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var language = item.Tag?.ToString() ?? "en";
        _config.Language = language;
        _localizationService.Load(language);
        Title = _localizationService.T("app.title", "CrosshairFlex");
        ApplyLocalizedTexts();
        UpdateSafetyText();
        SaveConfig();
    }

    private void SetLanguageSelection(string language)
    {
        foreach (var item in LanguageComboBox.Items)
        {
            if (item is ComboBoxItem combo && string.Equals(combo.Tag?.ToString(), language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageComboBox.SelectedItem = combo;
                return;
            }
        }

        LanguageComboBox.SelectedIndex = 0;
    }

    private void ThemeToggleCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _config.Theme = ThemeToggleCheckBox.IsChecked == true ? "light" : "dark";
        ApplyTheme();
        SaveConfig();
    }

    private void ApplyTheme()
    {
        var light = string.Equals(_config.Theme, "light", StringComparison.OrdinalIgnoreCase);
        Background = light
            ? new SolidColorBrush(MediaColor.FromRgb(244, 248, 253))
            : new SolidColorBrush(MediaColor.FromRgb(10, 18, 32));

        Foreground = light
            ? new SolidColorBrush(MediaColor.FromRgb(0, 0, 0))
            : new SolidColorBrush(MediaColor.FromRgb(234, 242, 255));

        var panelBackground = new SolidColorBrush(light
            ? MediaColor.FromRgb(255, 255, 255)
            : MediaColor.FromRgb(16, 26, 43));
        var panelBorder = new SolidColorBrush(light
            ? MediaColor.FromRgb(200, 216, 236)
            : MediaColor.FromRgb(37, 56, 79));
        var inputBackground = new SolidColorBrush(light
            ? MediaColor.FromRgb(255, 255, 255)
            : MediaColor.FromRgb(14, 24, 39));
        var listBackground = new SolidColorBrush(light
            ? MediaColor.FromRgb(246, 250, 255)
            : MediaColor.FromRgb(14, 23, 38));
        var textColor = new SolidColorBrush(light
            ? MediaColor.FromRgb(0, 0, 0)
            : MediaColor.FromRgb(255, 255, 255));

        NavPanelBorder.Background = panelBackground;
        NavPanelBorder.BorderBrush = panelBorder;
        HomePanel.Background = panelBackground;
        HomePanel.BorderBrush = panelBorder;
        AddPanel.Background = panelBackground;
        AddPanel.BorderBrush = panelBorder;
        KeybindsPanel.Background = panelBackground;
        KeybindsPanel.BorderBrush = panelBorder;
        SharePanel.Background = panelBackground;
        SharePanel.BorderBrush = panelBorder;
        ProfilePanel.Background = panelBackground;
        ProfilePanel.BorderBrush = panelBorder;
        ShareImportCard.Background = panelBackground;
        ShareImportCard.BorderBrush = panelBorder;
        ShareCopyCard.Background = panelBackground;
        ShareCopyCard.BorderBrush = panelBorder;
        ProfileGeneralCard.Background = panelBackground;
        ProfileGeneralCard.BorderBrush = panelBorder;
        ProfileOverlayCard.Background = panelBackground;
        ProfileOverlayCard.BorderBrush = panelBorder;

        HomeProfilesListBox.Background = listBackground;
        HomeProfilesHorizontalListBox.Background = listBackground;
        KeybindProfilesListBox.Background = listBackground;
        HomeProfilesListBox.Foreground = textColor;
        HomeProfilesHorizontalListBox.Foreground = textColor;
        KeybindProfilesListBox.Foreground = textColor;

        AddProfileNameTextBox.Background = inputBackground;
        AddProfileNameTextBox.Foreground = textColor;
        AddCustomPngTextBox.Background = inputBackground;
        AddCustomPngTextBox.Foreground = textColor;
        foreach (var row in KeybindRowsPanel.Children.OfType<DockPanel>())
        {
            if (row.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault() is System.Windows.Controls.TextBox textBox)
            {
                textBox.Background = inputBackground;
                textBox.Foreground = textColor;
                textBox.BorderBrush = panelBorder;
            }
        }
        SelectedProfileLabelText.Foreground = textColor;
        SelectedProfileNameText.Foreground = textColor;
        SelectedProfileKeybindsText.Foreground = textColor;
        ShareCodeTextBox.Background = inputBackground;
        ShareCodeTextBox.Foreground = textColor;
        AddColorText.Foreground = textColor;
        VersionText.Foreground = textColor;
        HomeSubtitleText.Foreground = textColor;
        ShareStatusText.Foreground = textColor;
        SettingsStatusText.Foreground = textColor;
        ThemeToggleCheckBox.Foreground = textColor;
        StartWithWindowsCheckBox.Foreground = textColor;
        TempOnRightMouseCheckBox.Foreground = textColor;
        TempOnLeftMouseCheckBox.Foreground = textColor;
        SafeModeCheckBox.Foreground = textColor;
        LanguageLabelText.Foreground = textColor;
        LanguageComboBox.Foreground = new SolidColorBrush(MediaColor.FromRgb(0, 0, 0));
        LanguageComboBox.Background = inputBackground;
        LanguageComboBox.BorderBrush = panelBorder;
        ApplyThemeToAllBorders(light, panelBorder);
        ApplyNavHighlight(HomeNavButton, HomePanel.Visibility == Visibility.Visible);
        ApplyNavHighlight(AddNavButton, AddPanel.Visibility == Visibility.Visible);
        ApplyNavHighlight(KeybindNavButton, KeybindsPanel.Visibility == Visibility.Visible);
        ApplyNavHighlight(ShareNavButton, SharePanel.Visibility == Visibility.Visible);
        ApplyNavHighlight(ProfileNavButton, ProfilePanel.Visibility == Visibility.Visible);
    }

    private void ApplyThemeToAllBorders(bool light, SolidColorBrush borderBrush)
    {
        var targetBackground = new SolidColorBrush(light
            ? MediaColor.FromRgb(255, 255, 255)
            : MediaColor.FromRgb(16, 27, 44));

        ApplyThemeToBordersRecursive(this, targetBackground, borderBrush, light);
    }

    private static void ApplyThemeToBordersRecursive(DependencyObject root, System.Windows.Media.Brush background, System.Windows.Media.Brush borderBrush, bool light)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is Border border && !IsInsideButton(border))
            {
                if (light)
                {
                    border.Background = background;
                }
                else if (border.Background is SolidColorBrush existing && existing.Color.R > 240 && existing.Color.G > 240 && existing.Color.B > 240)
                {
                    border.Background = background;
                }

                border.BorderBrush = borderBrush;
            }

            ApplyThemeToBordersRecursive(child, background, borderBrush, light);
        }
    }

    private static bool IsInsideButton(DependencyObject element)
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void ApplyLocalizedTexts()
    {
        HomeTitleText.Text = _localizationService.T("home.title", "HOME");
        HomeSubtitleText.Text = _localizationService.T("home.subtitle", "Select your active crosshair profile.");
        HomeViewModeButton.Content = _homeLayoutMode == HomeLayoutMode.Horizontal
            ? _localizationService.T("home.view.horizontal", "View: Horizontal")
            : _localizationService.T("home.view.vertical", "View: Vertical");

        HomeNavButton.ToolTip = _localizationService.T("nav.home", "Home");
        AddNavButton.ToolTip = _localizationService.T("nav.add", "Add");
        KeybindNavButton.ToolTip = _localizationService.T("nav.keybinds", "Keybinds");
        ShareNavButton.ToolTip = _localizationService.T("nav.share", "Share");
        ProfileNavButton.ToolTip = _localizationService.T("nav.profile", "Profile");

        ThemeToggleCheckBox.Content = _localizationService.T("settings.light_mode", "Light Mode");
        StartWithWindowsCheckBox.Content = _localizationService.T("settings.start_with_windows", "Start with Windows");
        TempOnRightMouseCheckBox.Content = _localizationService.T("settings.temp_right", "Temporary crosshair on Right Mouse hold");
        TempOnLeftMouseCheckBox.Content = _localizationService.T("settings.temp_left", "Temporary crosshair on Left Mouse hold");
        SafeModeCheckBox.Content = _localizationService.T("settings.safe_mode", "Safe Mode (modifier-based switching only)");
        AddResetButton.Content = _localizationService.T("add.reset", "Reset Form");
        AddSaveButton.Content = _localizationService.T("add.save", "Save");
        AddDeleteButton.Content = _localizationService.T("add.delete", "Delete Profile");
        ShareCopyButton.Content = _localizationService.T("share.copy", "Copy Share Link");
        ShareImportButton.Content = _localizationService.T("share.import", "Import Package");
        SaveSettingsButton.Content = _localizationService.T("settings.save", "Save Settings");
        ToggleOverlayButton.Content = _localizationService.T("settings.toggle_overlay", "Toggle Overlay");
        ResetSettingsButton.Content = _localizationService.T("settings.reset", "Reset Settings");
        OpenOnboardingButton.Content = _localizationService.T("settings.onboarding", "Open Onboarding");
        AddEditTitleText.Text = _localizationService.T("add.title", "ADD / EDIT");
        AddProfileNameLabel.Text = _localizationService.T("add.profile_name", "Profile Name");
        AddShapeLabel.Text = _localizationService.T("add.shape", "Shape");
        AddSizeLabel.Text = _localizationService.T("add.size", "Size");
        AddThicknessLabel.Text = _localizationService.T("add.thickness", "Thickness");
        AddGapLabel.Text = _localizationService.T("add.gap", "Gap");
        AddPumpRoundLabel.Text = _localizationService.T("add.pump_rounding", "Pump Corner Rounding");
        AddOpacityLabel.Text = _localizationService.T("add.opacity", "Opacity");
        AddCustomPngLabel.Text = _localizationService.T("add.custom_png", "Custom PNG");
        PickColorButton.Content = _localizationService.T("add.pick_color", "Pick Color");
        BrowsePngButton.Content = _localizationService.T("add.browse", "Browse");
        KeybindsTitleText.Text = _localizationService.T("keybinds.title", "KEYBINDS");
        SelectedProfileLabelText.Text = _localizationService.T("keybinds.selected_profile", "Selected Profile");
        AssignedKeyLabelText.Text = _localizationService.T("keybinds.assigned_keys", "Assigned Keys");
        ApplyKeyButton.Content = _localizationService.T("keybinds.apply", "Apply Key");
        AddKeyButton.ToolTip = _localizationService.T("keybinds.add_hotkey", "Add additional hotkey field");
        ShareTitleText.Text = _localizationService.T("share.title", "SHARE");
        ShareSubtitleText.Text = _localizationService.T("share.subtitle", "Share your profiles and setup with your team.");
        ShareImportTitleText.Text = _localizationService.T("share.import_title", "Paste / Import");
        ShareImportDescText.Text = _localizationService.T("share.import_desc", "Paste a shared code to import a profile.");
        ShareCopyTitleText.Text = _localizationService.T("share.copy_title", "Copy / Export");
        ShareCopyDescText.Text = _localizationService.T("share.copy_desc", "Create a share link for the selected crosshair.");
        ShareSelectLabelText.Text = _localizationService.T("share.select", "Select Crosshair");
        ProfileTitleText.Text = _localizationService.T("profile.title", "PROFILE");
        GeneralTitleText.Text = _localizationService.T("profile.general", "General");
        LanguageLabelText.Text = _localizationService.T("profile.language", "Language");
        OverlayBehaviorTitleText.Text = _localizationService.T("profile.overlay_behavior", "Overlay Behavior");

        if (KeybindProfilesListBox.SelectedItem is CrosshairProfile profile)
        {
            UpdateKeybindEditorUi(profile);
        }
    }

    private void BehaviorChanged_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        PushBehaviorToServices();
        SaveConfig();
    }

    private void SaveSettings_OnClick(object sender, RoutedEventArgs e)
    {
        PushBehaviorToServices();
        SaveAndReloadHotkeys();
        SettingsStatusText.Text = "Settings saved.";
        SettingsStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(120, 220, 170));
    }

    private void SafeModeCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _config.SafeMode = SafeModeCheckBox.IsChecked == true;
        UpdateSafetyText();
        SaveAndReloadHotkeys();
    }

    private void UpdateSafetyText()
    {
        var warning = _localizationService.T("safe_mode.warning", "Single-key switching may not work in some anti-cheat protected games.");
        AntiCheatWarningText.Text = warning;
        SafeModeInfoText.Text = warning;
    }

    private void ToggleOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        _overlayService.ToggleOverlayVisibility();
    }

    private void OpenOnboarding_OnClick(object sender, RoutedEventArgs e)
    {
        var onboarding = new OnboardingWindow(_config.Language) { Owner = this };
        onboarding.ShowDialog();
    }

    private void ResetSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var fresh = new AppConfig
        {
            FirstLaunchCompleted = true
        };
        _profiles.Clear();
        _profiles.Add(CrosshairProfile.CreateDefault());
        fresh.Profiles = [.. _profiles];
        _config = fresh;

        _updatingUi = true;
        StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;
        TempOnRightMouseCheckBox.IsChecked = _config.EnableTemporaryOnRightMouse;
        TempOnLeftMouseCheckBox.IsChecked = _config.EnableTemporaryOnLeftMouse;
        SafeModeCheckBox.IsChecked = _config.SafeMode;
        ThemeToggleCheckBox.IsChecked = false;
        SetLanguageSelection("en");
        _updatingUi = false;

        SelectProfile(_profiles[0]);
        ApplyTheme();
        UpdateSafetyText();
        ApplyAndSave();
    }

    private void ApplyAndSave()
    {
        if (CurrentProfile is null)
        {
            return;
        }

        PullAddEditorToProfile(CurrentProfile);
        HomeProfilesListBox.Items.Refresh();
        HomeProfilesHorizontalListBox.Items.Refresh();
        KeybindProfilesListBox.Items.Refresh();
        ShareProfileComboBox.Items.Refresh();
        PushBehaviorToServices();
        SaveAndReloadHotkeys();
    }

    private void PushBehaviorToServices()
    {
        _config.Profiles = [.. _profiles];
        _config.LastProfileId = CurrentProfile?.Id ?? _config.LastProfileId;
        _config.EnableTemporaryOnRightMouse = TempOnRightMouseCheckBox.IsChecked == true;
        _config.EnableTemporaryOnLeftMouse = TempOnLeftMouseCheckBox.IsChecked == true;
        _config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _config.SafeMode = SafeModeCheckBox.IsChecked == true;

        _startupService.SetStartup(_config.StartWithWindows);
        ApplyVisualSettingsToOverlay();
    }

    private void ApplyVisualSettingsToOverlay()
    {
        if (CurrentProfile is null)
        {
            return;
        }

        _overlayService.ApplyProfile(CurrentProfile);
        _hotkeyService.SetMouseBehavior(
            _config.EnableTemporaryOnRightMouse,
            _config.EnableTemporaryOnLeftMouse,
            () => _overlayService.SetTemporaryVisible(true),
            () => _overlayService.SetTemporaryVisible(false));
    }

    private void SaveConfig()
    {
        _configService.Save(_config);
    }

    private void SaveAndReloadHotkeys()
    {
        SaveConfig();
        var bindings = new List<ProfileHotkeyBinding>(_profiles.Count * 2);
        for (var i = 0; i < _profiles.Count; i++)
        {
            NormalizeProfileHotkeys(_profiles[i]);
            for (var k = 0; k < _profiles[i].ProfileHotkeys.Count; k++)
            {
                bindings.Add(new ProfileHotkeyBinding(_profiles[i].Id, _profiles[i].ProfileHotkeys[k]));
            }
        }

        var result = _hotkeyService.RegisterAll(bindings, _config.SafeMode);
        if (result.Failures.Count == 0)
        {
            KeybindStatusText.Text = $"{_localizationService.T("status.hotkeys_ok", "Hotkeys active")}: {result.RegisteredCount}";
            KeybindStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(120, 220, 170));
            return;
        }

        var first = result.Failures[0];
        KeybindStatusText.Text = $"{_localizationService.T("status.hotkeys_issue", "Hotkey issue")}: '{first.Hotkey}' -> {first.Reason}";
        KeybindStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 130, 130));
    }
}
