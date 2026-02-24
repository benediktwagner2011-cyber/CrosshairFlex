using CrosshairFlex.Desktop.Interop;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace CrosshairFlex.Desktop.Services;

public readonly record struct ProfileHotkeyBinding(string ProfileId, string Hotkey);
public readonly record struct HotkeyRegistrationFailure(string ProfileId, string Hotkey, string Reason);
public sealed class HotkeyRegistrationResult
{
    public int RegisteredCount { get; init; }
    public List<HotkeyRegistrationFailure> Failures { get; init; } = [];
}

public sealed class HotkeyService : IDisposable
{
    private const int VirtualKeyMapSize = 256;
    private const int QueueCapacity = 128;
    private const int ModControl = 1;
    private const int ModAlt = 2;
    private const int ModShift = 4;
    private const int ModWin = 8;

    private readonly int[] _virtualKeyToProfileIndex = new int[VirtualKeyMapSize];
    private readonly int[] _requiredModifiersByVirtualKey = new int[VirtualKeyMapSize];
    private readonly bool[] _keyDownState = new bool[VirtualKeyMapSize];
    private readonly int[] _switchQueue = new int[QueueCapacity];
    private readonly AutoResetEvent _queueSignal = new(initialState: false);
    private readonly CancellationTokenSource _workerCancellation = new();
    private readonly Thread _workerThread;

    private int _queueHead;
    private int _queueTail;

    private string[] _profileIdsByIndex = [];
    private Action<string>? _profileAction;
    private Func<bool>? _isOverlayVisibleProvider;
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _mouseProc;
    private bool _tempOnRightMouse;
    private bool _tempOnLeftMouse;
    private Action? _tempOnPressed;
    private Action? _tempOnReleased;
    public bool IsInitialized => _keyboardHookHandle != IntPtr.Zero;

    public HotkeyService()
    {
        Array.Fill(_virtualKeyToProfileIndex, -1);
        Array.Fill(_requiredModifiersByVirtualKey, -1);
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "ProfileSwitchWorker"
        };
        _workerThread.Start();
    }

    public void Initialize(Window owner, Action<string> profileAction, Func<bool> isOverlayVisibleProvider)
    {
        _ = owner;
        _profileAction = profileAction;
        _isOverlayVisibleProvider = isOverlayVisibleProvider;

        if (_keyboardHookHandle == IntPtr.Zero)
        {
            _keyboardProc = KeyboardProc;
            var module = NativeMethods.GetModuleHandle(Environment.ProcessPath is null ? null : Path.GetFileName(Environment.ProcessPath));
            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardProc, module, 0);
        }
    }

    public HotkeyRegistrationResult RegisterAll(IEnumerable<ProfileHotkeyBinding> profileHotkeys, bool safeMode)
    {
        Array.Fill(_virtualKeyToProfileIndex, -1);
        Array.Fill(_requiredModifiersByVirtualKey, -1);
        Array.Fill(_keyDownState, false);
        Volatile.Write(ref _queueHead, 0);
        Volatile.Write(ref _queueTail, 0);

        var registered = 0;
        var failures = new List<HotkeyRegistrationFailure>();
        var profileIds = new List<string>();

        foreach (var binding in profileHotkeys)
        {
            if (!TryParseProfileSwitchKey(binding.Hotkey, out var vk, out var requiredModifiers, out var parseReason))
            {
                failures.Add(new HotkeyRegistrationFailure(binding.ProfileId, binding.Hotkey, parseReason));
                continue;
            }

            if (safeMode && requiredModifiers == 0)
            {
                failures.Add(new HotkeyRegistrationFailure(binding.ProfileId, binding.Hotkey, "Safe mode requires Ctrl/Alt/Shift/Win"));
                continue;
            }

            if (vk >= VirtualKeyMapSize)
            {
                failures.Add(new HotkeyRegistrationFailure(binding.ProfileId, binding.Hotkey, "Key not supported by low-level mapper"));
                continue;
            }

            if (_virtualKeyToProfileIndex[vk] >= 0)
            {
                failures.Add(new HotkeyRegistrationFailure(binding.ProfileId, binding.Hotkey, "Already assigned to another profile"));
                continue;
            }

            var profileIndex = profileIds.Count;
            profileIds.Add(binding.ProfileId);
            _virtualKeyToProfileIndex[vk] = profileIndex;
            _requiredModifiersByVirtualKey[vk] = requiredModifiers;
            registered++;
        }

        _profileIdsByIndex = profileIds.ToArray();

        return new HotkeyRegistrationResult
        {
            RegisteredCount = registered,
            Failures = failures
        };
    }

    public void SetMouseBehavior(bool tempOnRightMouse, bool tempOnLeftMouse, Action onPressed, Action onReleased)
    {
        _tempOnRightMouse = tempOnRightMouse;
        _tempOnLeftMouse = tempOnLeftMouse;
        _tempOnPressed = onPressed;
        _tempOnReleased = onReleased;

        if ((_tempOnRightMouse || _tempOnLeftMouse) && _mouseHookHandle == IntPtr.Zero)
        {
            _mouseProc = MouseProc;
            var module = NativeMethods.GetModuleHandle(Environment.ProcessPath is null ? null : Path.GetFileName(Environment.ProcessPath));
            _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseProc, module, 0);
            return;
        }

        if (!_tempOnRightMouse && !_tempOnLeftMouse && _mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        if (_isOverlayVisibleProvider is null || !_isOverlayVisibleProvider())
        {
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message == NativeMethods.WmKeyDown || message == NativeMethods.WmSysKeyDown)
        {
            var vk = Marshal.ReadInt32(lParam);
            if ((uint)vk < VirtualKeyMapSize)
            {
                if (!_keyDownState[vk])
                {
                    _keyDownState[vk] = true;
                    var profileIndex = Volatile.Read(ref _virtualKeyToProfileIndex[vk]);
                    if (profileIndex >= 0)
                    {
                        var requiredMods = Volatile.Read(ref _requiredModifiersByVirtualKey[vk]);
                        var currentMods = GetCurrentModifierMask();
                        if ((currentMods & requiredMods) == requiredMods)
                        {
                            EnqueueProfileSwitch(profileIndex);
                        }
                    }
                }
            }
        }
        else if (message == NativeMethods.WmKeyUp || message == NativeMethods.WmSysKeyUp)
        {
            var vk = Marshal.ReadInt32(lParam);
            if ((uint)vk < VirtualKeyMapSize)
            {
                _keyDownState[vk] = false;
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private int GetCurrentModifierMask()
    {
        var modifiers = 0;
        if (IsKeyDown(0x11) || IsKeyDown(0xA2) || IsKeyDown(0xA3))
        {
            modifiers |= ModControl;
        }

        if (IsKeyDown(0x12) || IsKeyDown(0xA4) || IsKeyDown(0xA5))
        {
            modifiers |= ModAlt;
        }

        if (IsKeyDown(0x10) || IsKeyDown(0xA0) || IsKeyDown(0xA1))
        {
            modifiers |= ModShift;
        }

        if (IsKeyDown(0x5B) || IsKeyDown(0x5C))
        {
            modifiers |= ModWin;
        }

        return modifiers;
    }

    private bool IsKeyDown(int vk)
    {
        return (uint)vk < VirtualKeyMapSize && _keyDownState[vk];
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (_tempOnRightMouse)
            {
                if (message == NativeMethods.WmRButtonDown)
                {
                    _tempOnPressed?.Invoke();
                }
                else if (message == NativeMethods.WmRButtonUp)
                {
                    _tempOnReleased?.Invoke();
                }
            }

            if (_tempOnLeftMouse)
            {
                if (message == NativeMethods.WmLButtonDown)
                {
                    _tempOnPressed?.Invoke();
                }
                else if (message == NativeMethods.WmLButtonUp)
                {
                    _tempOnReleased?.Invoke();
                }
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private void EnqueueProfileSwitch(int profileIndex)
    {
        var tail = Volatile.Read(ref _queueTail);
        var nextTail = tail + 1;
        if (nextTail == QueueCapacity)
        {
            nextTail = 0;
        }

        var head = Volatile.Read(ref _queueHead);
        if (nextTail == head)
        {
            var nextHead = head + 1;
            if (nextHead == QueueCapacity)
            {
                nextHead = 0;
            }

            Volatile.Write(ref _queueHead, nextHead);
        }

        _switchQueue[tail] = profileIndex;
        Volatile.Write(ref _queueTail, nextTail);
        _queueSignal.Set();
    }

    private bool TryDequeueProfileSwitch(out int profileIndex)
    {
        var head = Volatile.Read(ref _queueHead);
        var tail = Volatile.Read(ref _queueTail);
        if (head == tail)
        {
            profileIndex = -1;
            return false;
        }

        profileIndex = _switchQueue[head];
        var nextHead = head + 1;
        if (nextHead == QueueCapacity)
        {
            nextHead = 0;
        }

        Volatile.Write(ref _queueHead, nextHead);
        return true;
    }

    private void WorkerLoop()
    {
        var waitHandles = new WaitHandle[] { _queueSignal, _workerCancellation.Token.WaitHandle };
        while (true)
        {
            while (TryDequeueProfileSwitch(out var profileIndex))
            {
                if ((uint)profileIndex >= (uint)_profileIdsByIndex.Length)
                {
                    continue;
                }

                var profileId = _profileIdsByIndex[profileIndex];
                if (profileId.Length == 0)
                {
                    continue;
                }

                _profileAction?.Invoke(profileId);
            }

            var signaled = WaitHandle.WaitAny(waitHandles);
            if (signaled == 1)
            {
                return;
            }
        }
    }

    private static bool TryParseProfileSwitchKey(string raw, out int virtualKey, out int modifiers, out string reason)
    {
        virtualKey = 0;
        modifiers = 0;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            reason = "Empty";
            return false;
        }

        var parts = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            reason = "Invalid format";
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!TryParseModifier(parts[i], out var modifier))
            {
                reason = $"Unknown modifier '{parts[i]}'";
                return false;
            }

            modifiers |= modifier;
        }

        if (!TryParseKeyToken(parts[^1], out var key))
        {
            reason = $"Unknown key token '{parts[^1]}'";
            return false;
        }

        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            reason = "Unsupported key";
            return false;
        }

        reason = "OK";
        return true;
    }

    private static bool TryParseModifier(string token, out int modifier)
    {
        modifier = 0;
        if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Strg", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModControl;
            return true;
        }

        if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModAlt;
            return true;
        }

        if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Umschalt", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModShift;
            return true;
        }

        if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModWin;
            return true;
        }

        return false;
    }

    private static bool TryParseKeyToken(string token, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalized = token.Trim();

        // Accept common user input like "Ctrl+Alt+1".
        if (normalized.Length == 1 && char.IsDigit(normalized[0]))
        {
            key = normalized[0] switch
            {
                '0' => Key.D0,
                '1' => Key.D1,
                '2' => Key.D2,
                '3' => Key.D3,
                '4' => Key.D4,
                '5' => Key.D5,
                '6' => Key.D6,
                '7' => Key.D7,
                '8' => Key.D8,
                '9' => Key.D9,
                _ => Key.None
            };
            return key != Key.None;
        }

        if (normalized.Length == 1 && char.IsLetter(normalized[0]))
        {
            return Enum.TryParse(normalized.ToUpperInvariant(), out key);
        }

        if (normalized.StartsWith("Num", StringComparison.OrdinalIgnoreCase) &&
            normalized.Length == 4 &&
            char.IsDigit(normalized[3]))
        {
            key = normalized[3] switch
            {
                '0' => Key.NumPad0,
                '1' => Key.NumPad1,
                '2' => Key.NumPad2,
                '3' => Key.NumPad3,
                '4' => Key.NumPad4,
                '5' => Key.NumPad5,
                '6' => Key.NumPad6,
                '7' => Key.NumPad7,
                '8' => Key.NumPad8,
                '9' => Key.NumPad9,
                _ => Key.None
            };
            return key != Key.None;
        }

        return Enum.TryParse(normalized, ignoreCase: true, out key);
    }

    public void Dispose()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        if (_mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        _workerCancellation.Cancel();
        _queueSignal.Set();
        _workerThread.Join(TimeSpan.FromMilliseconds(500));
        _workerCancellation.Dispose();
        _queueSignal.Dispose();

        _profileAction = null;
        _isOverlayVisibleProvider = null;
        _keyboardProc = null;
        _mouseProc = null;
    }
}
