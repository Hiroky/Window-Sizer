using System.ComponentModel;

namespace WindowSizer;

/// <summary>
/// ウインドウ一覧から対象を選び、クライアント領域の幅と高さを指定して適用するメイン画面。
/// </summary>
internal sealed class MainForm : Form
{
    private const int MinSize = 1;
    private const int MaxSize = 16384;
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 720;

    private static readonly Color WarnColor = Color.DarkOrange;
    private static readonly Color ErrorColor = Color.Firebrick;

    private readonly ListView _listView = new();
    private readonly ComboBox _presetBox = new();
    private readonly NumericUpDown _widthBox = new();
    private readonly NumericUpDown _heightBox = new();
    private readonly Button _refreshButton = new();
    private readonly Button _applyButton = new();
    private readonly Label _statusLabel = new();

    // プリセット欄と幅・高さ欄を同期している間は、互いの変更イベントに反応しない
    private bool _syncingInputs;

    public MainForm()
    {
        Text = "Window Sizer - クライアント領域リサイズ";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 520);
        MinimumSize = new Size(640, 400);

        BuildLayout();

        Shown += (_, _) => RefreshWindowList();
        _refreshButton.Click += (_, _) => RefreshWindowList();
        _listView.SelectedIndexChanged += (_, _) => OnSelectionChanged();
        _presetBox.SelectedIndexChanged += (_, _) => OnPresetChanged();
        _widthBox.ValueChanged += (_, _) => OnSizeInputChanged();
        _heightBox.ValueChanged += (_, _) => OnSizeInputChanged();
        _applyButton.Click += (_, _) => ApplyResize();
        AcceptButton = _applyButton;

        SetSizeInputs(DefaultWidth, DefaultHeight);
    }

    private void BuildLayout()
    {
        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.GridLines = true;
        _listView.HideSelection = false;
        _listView.MultiSelect = false;
        _listView.Columns.Add("タイトル", 360);
        _listView.Columns.Add("プロセス", 140);
        _listView.Columns.Add("クライアント幅", 110);
        _listView.Columns.Add("クライアント高さ", 120);

        _refreshButton.Text = "一覧を更新";
        _refreshButton.AutoSize = true;
        _refreshButton.Margin = new Padding(0, 0, 6, 0);

        _presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _presetBox.Width = 340;
        _presetBox.Items.Add(DisplayPreset.Custom);
        foreach (var preset in DisplayPresets.All)
        {
            _presetBox.Items.Add(preset);
        }

        _widthBox.Minimum = MinSize;
        _widthBox.Maximum = MaxSize;
        _widthBox.Value = DefaultWidth;
        _widthBox.Width = 90;

        _heightBox.Minimum = MinSize;
        _heightBox.Maximum = MaxSize;
        _heightBox.Value = DefaultHeight;
        _heightBox.Width = 90;

        _applyButton.Text = "適用";
        _applyButton.AutoSize = true;

        _statusLabel.AutoSize = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(
            CreateRow(_refreshButton, CreateLabel("対象のウインドウを選び、クライアント領域のサイズを指定して「適用」を押してください。")),
            0,
            0);
        layout.Controls.Add(_listView, 0, 1);
        layout.Controls.Add(CreateRow(CreateLabel("解像度プリセット:"), _presetBox), 0, 2);
        layout.Controls.Add(
            CreateRow(CreateLabel("幅 (px):"), _widthBox, CreateLabel("高さ (px):"), _heightBox, _applyButton),
            0,
            3);
        layout.Controls.Add(_statusLabel, 0, 4);

        Controls.Add(layout);
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 6, 6, 0),
    };

    private static FlowLayoutPanel CreateRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
        };
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>一覧を再取得する。選択中のウインドウがあれば選択を維持する。</summary>
    private void RefreshWindowList()
    {
        var selectedHandle = GetSelectedWindow()?.Handle;

        try
        {
            _listView.BeginUpdate();
            _listView.Items.Clear();

            foreach (var window in WindowResizer.GetWindows())
            {
                var item = CreateItem(window);
                _listView.Items.Add(item);

                if (window.Handle == selectedHandle)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                }
            }
        }
        finally
        {
            _listView.EndUpdate();
        }

        ShowStatus($"{_listView.Items.Count} 件のウインドウを表示しています。", SystemColors.ControlText);
    }

    /// <summary>ウインドウを選んだら現在のクライアント領域を状態欄に表示する。幅・高さの入力欄は変更しない。</summary>
    private void OnSelectionChanged()
    {
        if (GetSelectedWindow() is not { } window)
        {
            return;
        }

        if (window.ClientSize is { } size)
        {
            ShowStatus($"現在のクライアント領域: {size.Width} x {size.Height}", SystemColors.ControlText);
        }
        else
        {
            ShowStatus("最小化されています。適用時に元のサイズへ戻してからリサイズします。", WarnColor);
        }
    }

    /// <summary>プリセット欄で項目が選ばれたら、その解像度を幅・高さに反映する。</summary>
    private void OnPresetChanged()
    {
        if (_syncingInputs || _presetBox.SelectedItem is not DisplayPreset { IsCustom: false } preset)
        {
            return;
        }

        SetSizeInputs(preset.Width, preset.Height);
    }

    /// <summary>幅・高さが変わったら、一致するプリセットを選ぶ（一致しなければカスタム）。</summary>
    private void OnSizeInputChanged()
    {
        if (!_syncingInputs)
        {
            WithSyncing(SyncPresetBox);
        }
    }

    /// <summary>幅と高さを設定し、プリセット欄も合わせる。</summary>
    private void SetSizeInputs(int width, int height)
    {
        WithSyncing(() =>
        {
            _widthBox.Value = Math.Clamp(width, MinSize, MaxSize);
            _heightBox.Value = Math.Clamp(height, MinSize, MaxSize);
            SyncPresetBox();
        });
    }

    private void SyncPresetBox()
    {
        _presetBox.SelectedItem = DisplayPresets.Find((int)_widthBox.Value, (int)_heightBox.Value) ?? DisplayPreset.Custom;
    }

    private void WithSyncing(Action action)
    {
        _syncingInputs = true;
        try
        {
            action();
        }
        finally
        {
            _syncingInputs = false;
        }
    }

    private void ApplyResize()
    {
        // Enter キーで適用された場合も入力中の値を確定させるため、先にフォーカスを移す
        _applyButton.Focus();

        if (_listView.SelectedItems.Count == 0)
        {
            ShowStatus("一覧からウインドウを選択してください。", WarnColor);
            return;
        }

        var item = _listView.SelectedItems[0];
        var window = (WindowInfo)item.Tag!;
        var requested = new Size((int)_widthBox.Value, (int)_heightBox.Value);

        try
        {
            var actual = WindowResizer.ResizeClientArea(window.Handle, requested);
            UpdateItemSize(item, window with { ClientSize = actual });

            if (actual == requested)
            {
                ShowStatus($"適用しました: {actual.Width} x {actual.Height}", SystemColors.ControlText);
            }
            else
            {
                ShowStatus(
                    $"指定は {requested.Width} x {requested.Height} ですが、実際は {actual.Width} x {actual.Height} になりました。対象アプリが最小・最大サイズを制限している可能性があります。",
                    WarnColor);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            ShowStatus("適用に失敗しました。", ErrorColor);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private WindowInfo? GetSelectedWindow() =>
        _listView.SelectedItems.Count > 0 ? _listView.SelectedItems[0].Tag as WindowInfo : null;

    private static ListViewItem CreateItem(WindowInfo window)
    {
        var item = new ListViewItem(new[] { window.Title, window.ProcessName, string.Empty, string.Empty });
        UpdateItemSize(item, window);
        return item;
    }

    private static void UpdateItemSize(ListViewItem item, WindowInfo window)
    {
        item.Tag = window;
        var size = window.ClientSize;
        item.SubItems[2].Text = size?.Width.ToString() ?? "最小化";
        item.SubItems[3].Text = size?.Height.ToString() ?? "-";
    }

    private void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
    }
}
