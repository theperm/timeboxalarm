using Microsoft.Win32;
using System.Diagnostics;
using System.Media;
using System.Text;

namespace TimeboxAlarm;

public partial class MainForm : Form
{
    private const int AlarmCount = 5;
    private const int TriggerWindowSeconds = 2;
    private const string DefaultSoundName = "Exclamation";
    private static readonly int[] DefaultIntervals = [1, 5, 15, 30, 60];
    private static readonly SoundOption[] AvailableSounds =
    [
        new("Asterisk", SystemSounds.Asterisk),
        new("Beep", SystemSounds.Beep),
        new("Exclamation", SystemSounds.Exclamation),
        new("Hand", SystemSounds.Hand),
        new("Question", SystemSounds.Question)
    ];
    private static readonly int DefaultSoundIndex = Array.FindIndex(
        AvailableSounds,
        sound => string.Equals(sound.Name, DefaultSoundName, StringComparison.OrdinalIgnoreCase));

    private readonly List<AlarmRow> _alarms = [];
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _trayIcon;
    private readonly Button _quitButton;
    private readonly ComboBox _soundSelector;
    private readonly string _settingsPath;
    private bool _isExiting;
    private bool _isLoadingSettings;

    public MainForm()
    {
        InitializeComponent();
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "timeboxalarm.ini");

        Text = "Timebox Alarm";
        Width = 640;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;

        BuildAlarmUi();
        _trayIcon = CreateTrayIcon();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => CheckAlarms(DateTime.Now);
        _timer.Start();

        _quitButton = new Button
        {
            Text = "Quit",
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Margin = new Padding(8)
        };
        _quitButton.Click += (_, _) => QuitApplication();

        _soundSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160
        };
        _soundSelector.Items.AddRange(AvailableSounds.Select(sound => sound.Name).ToArray());
        _soundSelector.SelectedIndex = GetSoundIndexByName(DefaultSoundName);
        _soundSelector.SelectedIndexChanged += (_, _) => SaveSettings();

        var soundLabel = new Label
        {
            Text = "Alarm sound:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 11, 4, 8)
        };

        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48
        };
        bottomPanel.Controls.Add(_quitButton);
        bottomPanel.Controls.Add(_soundSelector);
        bottomPanel.Controls.Add(soundLabel);
        Controls.Add(bottomPanel);

        ApplyTheme(GetPreferredTheme());
        LoadSettings();
        UpdateAllStatuses(DateTime.Now);

        FormClosing += OnFormClosing;
        Resize += OnFormResize;
    }

    private enum AppTheme
    {
        Light,
        Dark
    }

    private sealed class ThemePalette
    {
        public required Color Surface { get; init; }
        public required Color ControlSurface { get; init; }
        public required Color Foreground { get; init; }
        public required Color ButtonSurface { get; init; }
        public required Color ButtonForeground { get; init; }
    }

    private sealed class SoundOption(string name, SystemSound sound)
    {
        public string Name { get; } = name;
        public SystemSound Sound { get; } = sound;
    }

    private sealed class AlarmRow
    {
        public required CheckBox EnabledCheckBox { get; init; }
        public required NumericUpDown IntervalInput { get; init; }
        public required Label StatusLabel { get; init; }
        public DateTime? LastTriggeredBoundary { get; set; }
    }

    private void BuildAlarmUi()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = AlarmCount + 1,
            Padding = new Padding(12),
            AutoSize = false
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        table.Controls.Add(new Label { Text = "Alarm", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        table.Controls.Add(new Label { Text = "Interval (min)", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 1, 0);
        table.Controls.Add(new Label { Text = "Enabled", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 2, 0);
        table.Controls.Add(new Label { Text = "Status", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 3, 0);

        for (var i = 0; i < AlarmCount; i++)
        {
            var name = new Label
            {
                Text = $"Alarm {i + 1}",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            var interval = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 60,
                Value = DefaultIntervals[i],
                Width = 80
            };

            var enabled = new CheckBox { Checked = false, AutoSize = true };
            var status = new Label { Text = "Disabled", AutoSize = true, Anchor = AnchorStyles.Left };

            var row = new AlarmRow
            {
                EnabledCheckBox = enabled,
                IntervalInput = interval,
                StatusLabel = status
            };

            enabled.CheckedChanged += (_, _) =>
            {
                row.LastTriggeredBoundary = null;
                UpdateAlarmStatus(row, DateTime.Now);
                SaveSettings();
            };

            interval.ValueChanged += (_, _) =>
            {
                row.LastTriggeredBoundary = null;
                UpdateAlarmStatus(row, DateTime.Now);
                SaveSettings();
            };

            _alarms.Add(row);

            table.Controls.Add(name, 0, i + 1);
            table.Controls.Add(interval, 1, i + 1);
            table.Controls.Add(enabled, 2, i + 1);
            table.Controls.Add(status, 3, i + 1);
        }

        Controls.Add(table);
    }

    private static AppTheme GetPreferredTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key is null)
            {
                return AppTheme.Light;
            }

            if (key.GetValue("AppsUseLightTheme") is int appsUseLightThemeValue)
            {
                return appsUseLightThemeValue == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch (Exception)
        {
            // Fall back to light theme if the Windows theme setting is unavailable.
        }

        return AppTheme.Light;
    }

    private void ApplyTheme(AppTheme theme)
    {
        var palette = theme switch
        {
            AppTheme.Dark => new ThemePalette
            {
                Surface = Color.FromArgb(32, 32, 32),
                ControlSurface = Color.FromArgb(45, 45, 48),
                Foreground = Color.FromArgb(241, 241, 241),
                ButtonSurface = Color.FromArgb(63, 63, 70),
                ButtonForeground = Color.FromArgb(241, 241, 241)
            },
            _ => new ThemePalette
            {
                Surface = SystemColors.Control,
                ControlSurface = Color.White,
                Foreground = SystemColors.ControlText,
                ButtonSurface = SystemColors.Control,
                ButtonForeground = SystemColors.ControlText
            }
        };

        BackColor = palette.Surface;
        ForeColor = palette.Foreground;
        ApplyThemeToControlTree(this, palette);
    }

    private static void ApplyThemeToControlTree(Control parent, ThemePalette palette)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case Label label:
                    label.ForeColor = palette.Foreground;
                    label.BackColor = palette.Surface;
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.ForeColor = palette.Foreground;
                    numericUpDown.BackColor = palette.ControlSurface;
                    break;
                case CheckBox checkBox:
                    checkBox.ForeColor = palette.Foreground;
                    checkBox.BackColor = palette.Surface;
                    break;
                case Button button:
                    button.ForeColor = palette.ButtonForeground;
                    button.BackColor = palette.ButtonSurface;
                    break;
                case ComboBox comboBox:
                    comboBox.ForeColor = palette.Foreground;
                    comboBox.BackColor = palette.ControlSurface;
                    break;
                default:
                    control.ForeColor = palette.Foreground;
                    control.BackColor = palette.Surface;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyThemeToControlTree(control, palette);
            }
        }
    }

    private NotifyIcon CreateTrayIcon()
    {
        var contextMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open");
        var quitItem = new ToolStripMenuItem("Quit");

        openItem.Click += (_, _) => RestoreFromTray();
        quitItem.Click += (_, _) => QuitApplication();

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(quitItem);

        var trayIcon = new NotifyIcon(components)
        {
            Icon = SystemIcons.Information,
            Text = "Timebox Alarm",
            ContextMenuStrip = contextMenu,
            Visible = false
        };
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        return trayIcon;
    }

    private void CheckAlarms(DateTime now)
    {
        var playedSoundThisTick = false;
        foreach (var alarm in _alarms)
        {
            if (alarm.EnabledCheckBox.Checked)
            {
                var interval = (int)alarm.IntervalInput.Value;

                if (now.Minute % interval == 0 && now.Second < TriggerWindowSeconds)
                {
                    var boundary = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                    if (alarm.LastTriggeredBoundary != boundary)
                    {
                        alarm.LastTriggeredBoundary = boundary;
                        if (!playedSoundThisTick)
                        {
                            GetSelectedSound().Play();
                            playedSoundThisTick = true;
                        }
                        alarm.StatusLabel.Text = $"Triggered at {boundary:HH:mm}";
                    }
                }
                else
                {
                    alarm.StatusLabel.Text = $"Next: {GetNextBoundary(now, interval):HH:mm}";
                }
            }
            else
            {
                alarm.StatusLabel.Text = "Disabled";
            }
        }
    }

    private void UpdateAlarmStatus(AlarmRow alarm, DateTime now)
    {
        alarm.StatusLabel.Text = alarm.EnabledCheckBox.Checked
            ? $"Next: {GetNextBoundary(now, (int)alarm.IntervalInput.Value):HH:mm}"
            : "Disabled";
    }

    private void UpdateAllStatuses(DateTime now)
    {
        foreach (var alarm in _alarms)
        {
            UpdateAlarmStatus(alarm, now);
        }
    }

    private static DateTime GetNextBoundary(DateTime now, int intervalMinutes)
    {
        if (now.Minute % intervalMinutes == 0 && now.Second < TriggerWindowSeconds)
        {
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        }

        var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
        var nextMinute = ((now.Minute / intervalMinutes) + 1) * intervalMinutes;

        return nextMinute < 60
            ? hourStart.AddMinutes(nextMinute)
            : hourStart.AddHours(1);
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        SaveSettings();

        if (_isExiting)
        {
            _trayIcon.Visible = false;
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
        _trayIcon.Visible = true;
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        _trayIcon.Visible = false;
    }

    private void QuitApplication()
    {
        SaveSettings();
        _isExiting = true;
        _trayIcon.Visible = false;
        Close();
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var line in File.ReadLines(_settingsPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var firstCharacter = trimmed[0];
                if (firstCharacter == ';' || firstCharacter == '#' || firstCharacter == '[')
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim();
                settings[key] = value;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to read settings from '{_settingsPath}': {exception}");
            return;
        }

        _isLoadingSettings = true;
        try
        {
            for (var i = 0; i < _alarms.Count; i++)
            {
                var row = _alarms[i];
                var prefix = $"alarm{i + 1}.";

                if (settings.TryGetValue($"{prefix}interval", out var intervalText)
                    && int.TryParse(intervalText, out var intervalValue))
                {
                    var clamped = Math.Clamp(intervalValue, (int)row.IntervalInput.Minimum, (int)row.IntervalInput.Maximum);
                    row.IntervalInput.Value = clamped;
                }

                if (settings.TryGetValue($"{prefix}enabled", out var enabledText)
                    && bool.TryParse(enabledText, out var enabledValue))
                {
                    row.EnabledCheckBox.Checked = enabledValue;
                }

                row.LastTriggeredBoundary = null;
            }

            if (settings.TryGetValue("sound.selected", out var selectedSoundName))
            {
                _soundSelector.SelectedIndex = GetSoundIndexByName(selectedSoundName);
            }
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void SaveSettings()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("[alarms]");
        for (var i = 0; i < _alarms.Count; i++)
        {
            var row = _alarms[i];
            var index = i + 1;
            builder.AppendLine($"alarm{index}.interval={(int)row.IntervalInput.Value}");
            builder.AppendLine($"alarm{index}.enabled={row.EnabledCheckBox.Checked}");
        }
        builder.AppendLine($"sound.selected={GetSelectedSoundName()}");

        try
        {
            File.WriteAllText(_settingsPath, builder.ToString());
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to save settings to '{_settingsPath}': {exception}");
        }
    }

    private int GetSoundIndexByName(string soundName)
    {
        for (var i = 0; i < AvailableSounds.Length; i++)
        {
            if (string.Equals(AvailableSounds[i].Name, soundName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return DefaultSoundIndex >= 0 ? DefaultSoundIndex : 0;
    }

    private string GetSelectedSoundName()
    {
        var index = _soundSelector.SelectedIndex;
        if (index < 0 || index >= AvailableSounds.Length)
        {
            return DefaultSoundName;
        }

        return AvailableSounds[index].Name;
    }

    private SystemSound GetSelectedSound()
    {
        var index = _soundSelector.SelectedIndex;
        if (index < 0 || index >= AvailableSounds.Length)
        {
            var safeDefaultIndex = DefaultSoundIndex >= 0 ? DefaultSoundIndex : 0;
            return AvailableSounds[safeDefaultIndex].Sound;
        }

        return AvailableSounds[index].Sound;
    }
}
