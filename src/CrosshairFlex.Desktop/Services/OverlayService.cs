using CrosshairFlex.Desktop.Models;
using Microsoft.Win32;

namespace CrosshairFlex.Desktop.Services;

public sealed class OverlayService : IDisposable
{
    private readonly OverlayWindow _overlayWindow;
    private bool _overlayEnabled = true;
    private bool _temporaryVisible;
    private CrosshairProfile _profile = CrosshairProfile.CreateDefault();
    private bool _renderDirty = true;

    public OverlayService()
    {
        _overlayWindow = new OverlayWindow();
        _overlayWindow.ApplyBoundsToVirtualScreen();
        _overlayWindow.Show();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        RedrawIfNeeded();
    }

    public bool IsVisibleForInput => _overlayEnabled || _temporaryVisible;

    public void ApplyProfile(CrosshairProfile profile)
    {
        _profile = profile;
        _renderDirty = true;
        RedrawIfNeeded();
        RefreshVisibility();
    }

    public void ToggleOverlayVisibility()
    {
        _overlayEnabled = !_overlayEnabled;
        RefreshVisibility();
    }

    public void SetTemporaryVisible(bool visible)
    {
        _temporaryVisible = visible;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        var shouldShow = _overlayEnabled || _temporaryVisible;
        if (shouldShow)
        {
            _overlayWindow.Show();
            RedrawIfNeeded();
        }
        else
        {
            _overlayWindow.Hide();
        }
    }

    private void RedrawIfNeeded()
    {
        if (!_renderDirty)
        {
            return;
        }

        _overlayWindow.ApplyBoundsToVirtualScreen();
        _overlayWindow.RenderProfile(_profile);
        _renderDirty = false;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _renderDirty = true;
        _overlayWindow.InvalidateRenderState();
        RedrawIfNeeded();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _overlayWindow.Close();
    }
}
