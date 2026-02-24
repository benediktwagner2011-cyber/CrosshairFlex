using CrosshairFlex.Desktop.Services;
using System.Windows;

namespace CrosshairFlex.Desktop;

public partial class OnboardingWindow : Window
{
    private readonly LocalizationService _localizationService = new();

    public OnboardingWindow(string language = "en")
    {
        InitializeComponent();
        _localizationService.Load(language);
        ApplyLocalizedTexts();
    }

    private void ApplyLocalizedTexts()
    {
        Title = _localizationService.T("onboarding.window_title", "Welcome to CrosshairFlex");
        HeadingText.Text = _localizationService.T("onboarding.heading", "CrosshairFlex");
        IntroText.Text = _localizationService.T("onboarding.intro", "CrosshairFlex adds a pure, transparent desktop overlay crosshair without injecting into games.");
        QuickSetupTitleText.Text = _localizationService.T("onboarding.quick_setup", "Quick setup:");
        Step1Text.Text = _localizationService.T("onboarding.step1", "1) Build a profile in Add/Edit and save it only when ready.");
        Step2Text.Text = _localizationService.T("onboarding.step2", "2) Switch Home view between vertical and horizontal profile cards.");
        Step3Text.Text = _localizationService.T("onboarding.step3", "3) Assign keybinds and use Share to export/import profile links.");
        Step4Text.Text = _localizationService.T("onboarding.step4", "4) Configure overlay behavior, theme, language, and startup options.");
        StartButton.Content = _localizationService.T("onboarding.start", "Start Using CrosshairFlex");
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
