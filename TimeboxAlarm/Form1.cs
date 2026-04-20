using System.Media;

namespace TimeboxAlarm;

public partial class Form1 : Form
{
    private const int AlarmCount = 5;

    private readonly List<AlarmRow> _alarms = [];
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _trayIcon;
    private readonly Button _quitButton;
    private bool _isExiting;

    public Form1()
    {
        InitializeComponent();

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

        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48
        };
        bottomPanel.Controls.Add(_quitButton);
        Controls.Add(bottomPanel);

        FormClosing += OnFormClosing;
        Resize += OnFormResize;
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
                Value = (i + 1) * 5,
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
                row.StatusLabel.Text = enabled.Checked
                    ? $"Next: {GetNextBoundary(DateTime.Now, (int)interval.Value):HH:mm}"
                    : "Disabled";
            };

            interval.ValueChanged += (_, _) =>
            {
                row.LastTriggeredBoundary = null;
                row.StatusLabel.Text = enabled.Checked
                    ? $"Next: {GetNextBoundary(DateTime.Now, (int)interval.Value):HH:mm}"
                    : "Disabled";
            };

            _alarms.Add(row);

            table.Controls.Add(name, 0, i + 1);
            table.Controls.Add(interval, 1, i + 1);
            table.Controls.Add(enabled, 2, i + 1);
            table.Controls.Add(status, 3, i + 1);
        }

        Controls.Add(table);
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
        foreach (var alarm in _alarms)
        {
            if (!alarm.EnabledCheckBox.Checked)
            {
                alarm.StatusLabel.Text = "Disabled";
                continue;
            }

            var interval = (int)alarm.IntervalInput.Value;
            if (interval <= 0)
            {
                alarm.StatusLabel.Text = "Disabled";
                continue;
            }

            if (now.Minute % interval == 0 && now.Second < 2)
            {
                var boundary = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                if (alarm.LastTriggeredBoundary != boundary)
                {
                    alarm.LastTriggeredBoundary = boundary;
                    SystemSounds.Exclamation.Play();
                    alarm.StatusLabel.Text = $"Triggered at {boundary:HH:mm}";
                }
            }
            else
            {
                alarm.StatusLabel.Text = $"Next: {GetNextBoundary(now, interval):HH:mm}";
            }
        }
    }

    private static DateTime GetNextBoundary(DateTime now, int intervalMinutes)
    {
        if (now.Minute % intervalMinutes == 0 && now.Second < 2)
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
        _isExiting = true;
        _trayIcon.Visible = false;
        Close();
    }
}
