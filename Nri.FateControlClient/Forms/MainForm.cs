using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Nri.FateControlClient.Models;
using Nri.FateControlClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.FateControlClient.Forms;

public sealed class MainForm : Form
{
    private readonly JsonTcpClient _tcpClient = new JsonTcpClient();
    private readonly FateApiClient _api;

    private readonly BindingList<FateLayerRow> _layers = new BindingList<FateLayerRow>();
    private readonly BindingList<FateLayerTraceRow> _trace = new BindingList<FateLayerTraceRow>();

    private readonly Label _connectionStatus = new Label();
    private readonly Label _loginStatus = new Label();
    private readonly Label _stateStatus = new Label();
    private readonly CheckBox _engineEnabled = new CheckBox();
    private readonly DataGridView _layersGrid = new DataGridView();
    private readonly DataGridView _traceGrid = new DataGridView();
    private readonly NumericUpDown _dieSides = new NumericUpDown();
    private readonly NumericUpDown _baseRoll = new NumericUpDown();
    private readonly Label _fateResultLabel = new Label();

    public MainForm()
    {
        _api = new FateApiClient(_tcpClient);

        Text = "NRI Fate Control Client";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        InitializeUi();
        ResetLayersToDefault();
        UpdateStatus("Ready.");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _tcpClient.Dispose();
        base.OnFormClosed(e);
    }

    private void InitializeUi()
    {
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(8),
            AutoSize = false
        };

        var serverButton = new Button { Text = "Сервер", Width = 90 };
        serverButton.Click += (_, __) => ConfigureServer();
        var loginButton = new Button { Text = "Вход", Width = 90 };
        loginButton.Click += (_, __) => PerformLogin();

        _connectionStatus.AutoSize = true;
        _connectionStatus.Text = "Connection: disconnected";
        _connectionStatus.Padding = new Padding(12, 8, 0, 0);

        _loginStatus.AutoSize = true;
        _loginStatus.Text = "Login: not authorized";
        _loginStatus.Padding = new Padding(12, 8, 0, 0);

        topPanel.Controls.Add(serverButton);
        topPanel.Controls.Add(loginButton);
        topPanel.Controls.Add(_connectionStatus);
        topPanel.Controls.Add(_loginStatus);
        Controls.Add(topPanel);

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 360
        };

        mainSplit.Panel1.Controls.Add(BuildSettingsPanel());
        mainSplit.Panel2.Controls.Add(BuildTestPanel());

        Controls.Add(mainSplit);

        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(8),
            AutoSize = false
        };

        var loadButton = new Button { Text = "Загрузить", Width = 100 };
        loadButton.Click += (_, __) => LoadSettings();

        var saveButton = new Button { Text = "Сохранить", Width = 100 };
        saveButton.Click += (_, __) => SaveSettings();

        var resetButton = new Button { Text = "Сбросить", Width = 100 };
        resetButton.Click += (_, __) => ResetAndSaveDefaults();

        var closeButton = new Button { Text = "Закрыть", Width = 100 };
        closeButton.Click += (_, __) => Close();

        _stateStatus.AutoSize = true;
        _stateStatus.Padding = new Padding(10, 10, 0, 0);

        bottomPanel.Controls.Add(loadButton);
        bottomPanel.Controls.Add(saveButton);
        bottomPanel.Controls.Add(resetButton);
        bottomPanel.Controls.Add(closeButton);
        bottomPanel.Controls.Add(_stateStatus);
        Controls.Add(bottomPanel);
    }

    private Control BuildSettingsPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        _engineEnabled.Text = "Fate Engine Enabled";
        _engineEnabled.Top = 8;
        _engineEnabled.Left = 8;
        _engineEnabled.Width = 180;

        _layersGrid.Top = 36;
        _layersGrid.Left = 8;
        _layersGrid.Width = 1048;
        _layersGrid.Height = 290;
        _layersGrid.AutoGenerateColumns = false;
        _layersGrid.AllowUserToAddRows = false;
        _layersGrid.AllowUserToDeleteRows = false;
        _layersGrid.RowHeadersVisible = false;
        _layersGrid.DataSource = _layers;

        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "LayerNumber", DataPropertyName = "LayerNumber", ReadOnly = true, Width = 100 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DisplayName", DataPropertyName = "DisplayName", Width = 180 });
        _layersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Enabled", DataPropertyName = "Enabled", Width = 90 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "FlatModifier", DataPropertyName = "FlatModifier", Width = 110 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Intensity", DataPropertyName = "Intensity", Width = 100 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mode", DataPropertyName = "Mode", Width = 140 });

        panel.Controls.Add(_engineEnabled);
        panel.Controls.Add(_layersGrid);
        return panel;
    }

    private Control BuildTestPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var dieLabel = new Label { Text = "DieSides", Left = 8, Top = 10, Width = 70 };
        _dieSides.Left = 82;
        _dieSides.Top = 6;
        _dieSides.Width = 90;
        _dieSides.Minimum = 2;
        _dieSides.Maximum = 1000;
        _dieSides.Value = 100;

        var baseRollLabel = new Label { Text = "BaseRoll", Left = 190, Top = 10, Width = 70 };
        _baseRoll.Left = 266;
        _baseRoll.Top = 6;
        _baseRoll.Width = 90;
        _baseRoll.Minimum = -100000;
        _baseRoll.Maximum = 100000;
        _baseRoll.Value = 10;

        var testButton = new Button { Text = "Тестовый бросок", Left = 380, Top = 4, Width = 140 };
        testButton.Click += (_, __) => RunTestRoll();

        _fateResultLabel.Left = 540;
        _fateResultLabel.Top = 10;
        _fateResultLabel.Width = 500;
        _fateResultLabel.Text = "Result: -";

        _traceGrid.Left = 8;
        _traceGrid.Top = 38;
        _traceGrid.Width = 1048;
        _traceGrid.Height = 280;
        _traceGrid.AutoGenerateColumns = false;
        _traceGrid.AllowUserToAddRows = false;
        _traceGrid.AllowUserToDeleteRows = false;
        _traceGrid.RowHeadersVisible = false;
        _traceGrid.DataSource = _trace;

        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Layer", DataPropertyName = "LayerNumber", Width = 60 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "LayerName", Width = 140 });
        _traceGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Applied", DataPropertyName = "Applied", Width = 70 });
        _traceGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Allowed", DataPropertyName = "AllowedForDie", Width = 70 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Input", DataPropertyName = "InputValue", Width = 80 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modifier", DataPropertyName = "Modifier", Width = 90 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Output", DataPropertyName = "OutputValue", Width = 80 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Reason", Width = 430 });

        panel.Controls.Add(dieLabel);
        panel.Controls.Add(_dieSides);
        panel.Controls.Add(baseRollLabel);
        panel.Controls.Add(_baseRoll);
        panel.Controls.Add(testButton);
        panel.Controls.Add(_fateResultLabel);
        panel.Controls.Add(_traceGrid);
        return panel;
    }

    private void ConfigureServer()
    {
        using var dialog = new ServerDialog(_tcpClient.Host, _tcpClient.Port);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _api.SetEndpoint(dialog.Host, dialog.Port);
        TryConnect();
    }

    private void PerformLogin()
    {
        EnsureConnected();

        using var dialog = new LoginDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var response = _api.Login(dialog.Login, dialog.Password);
        if (response.Status == ResponseStatus.Ok)
        {
            _loginStatus.Text = "Login: authorized";
            UpdateStatus("Login successful.");
            return;
        }

        ShowError($"Login failed: {response.Message}");
    }

    private void LoadSettings()
    {
        if (!EnsureAuthorized()) return;

        var response = _api.GetFateSettings();
        if (response.Status != ResponseStatus.Ok)
        {
            ShowError($"fate.settings.get failed: {response.Message}");
            return;
        }

        var parsedLayers = _api.ParseSettings(response, out var enabled);
        ApplyLayers(parsedLayers);
        _engineEnabled.Checked = enabled;
        var mods = string.Join("/", parsedLayers.OrderBy(x => x.LayerNumber).Select(x => x.FlatModifier));
        UpdateStatus($"Settings loaded: enabled={enabled} layers={parsedLayers.Count} mods={mods}");
    }

    private void SaveSettings()
    {
        if (!EnsureAuthorized()) return;

        _layersGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        _layersGrid.EndEdit();

        var response = _api.UpdateFateSettings(_engineEnabled.Checked, _layers.ToList());
        if (response.Status != ResponseStatus.Ok)
        {
            ShowError($"fate.settings.update failed: {response.Message}");
            return;
        }

        var loadResponse = _api.GetFateSettings();
        if (loadResponse.Status != ResponseStatus.Ok)
        {
            ShowError($"fate.settings.get after save failed: {loadResponse.Message}");
            return;
        }

        var parsedLayers = _api.ParseSettings(loadResponse, out var enabled);
        ApplyLayers(parsedLayers);
        _engineEnabled.Checked = enabled;
        UpdateStatus($"Settings saved, reloaded: enabled={enabled} layers={parsedLayers.Count}");
    }

    private void ResetAndSaveDefaults()
    {
        ResetLayersToDefault();
        _engineEnabled.Checked = true;
        SaveSettings();
    }

    private void RunTestRoll()
    {
        if (!EnsureAuthorized()) return;

        var response = _api.TestRoll((int)_dieSides.Value, (int)_baseRoll.Value);
        if (response.Status != ResponseStatus.Ok)
        {
            ShowError($"fate.test.roll failed: {response.Message}");
            return;
        }

        var trace = _api.ParseTrace(response, out var fateValue, out var applied, out var skippedReason);
        ApplyTrace(trace);

        _fateResultLabel.Text = $"Result: FateValue={fateValue}, Applied={applied}, SkippedReason={skippedReason}";
        UpdateStatus("Test roll executed.");
    }

    private void TryConnect()
    {
        try
        {
            _api.Connect();
            _connectionStatus.Text = $"Connection: connected ({_tcpClient.Host}:{_tcpClient.Port})";
            UpdateStatus("Connected.");
        }
        catch (Exception ex)
        {
            _connectionStatus.Text = "Connection: disconnected";
            ShowError($"Connection failed: {ex.Message}");
        }
    }

    private void EnsureConnected()
    {
        if (_api.IsConnected)
        {
            return;
        }

        TryConnect();
    }

    private bool EnsureAuthorized()
    {
        EnsureConnected();
        if (string.IsNullOrWhiteSpace(_api.AuthToken))
        {
            ShowError("Not authorized. Use 'Вход'.");
            return false;
        }

        return true;
    }

    private void ResetLayersToDefault()
    {
        ApplyLayers(Enumerable.Range(1, 5).Select(i => new FateLayerRow
        {
            LayerNumber = i,
            DisplayName = $"Layer {i}",
            Enabled = true,
            FlatModifier = 0,
            Intensity = 1.0,
            Mode = "flat"
        }).ToList());
    }

    private void ApplyLayers(System.Collections.Generic.List<FateLayerRow> source)
    {
        var normalized = Enumerable.Range(1, 5)
            .Select(i => source.FirstOrDefault(x => x.LayerNumber == i) ?? new FateLayerRow
            {
                LayerNumber = i,
                DisplayName = $"Layer {i}",
                Enabled = true,
                FlatModifier = 0,
                Intensity = 1.0,
                Mode = "flat"
            })
            .ToList();

        _layers.RaiseListChangedEvents = false;
        _layers.Clear();
        foreach (var row in normalized)
        {
            _layers.Add(row);
        }

        _layers.RaiseListChangedEvents = true;
        _layers.ResetBindings();
    }

    private void ApplyTrace(System.Collections.Generic.List<FateLayerTraceRow> source)
    {
        _trace.RaiseListChangedEvents = false;
        _trace.Clear();

        foreach (var row in source)
        {
            _trace.Add(row);
        }

        _trace.RaiseListChangedEvents = true;
        _trace.ResetBindings();
    }

    private void UpdateStatus(string message)
    {
        _stateStatus.Text = message;
    }

    private void ShowError(string message)
    {
        UpdateStatus(message);
        MessageBox.Show(this, message, "Fate Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
