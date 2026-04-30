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
    private static readonly string[] ModeOptions = { "flat", "Normal", "Test", "Disabled", "Debug" };
    private readonly JsonTcpClient _tcpClient = new JsonTcpClient();
    private readonly FateApiClient _api;

    private readonly BindingList<FateLayerRow> _layers = new BindingList<FateLayerRow>();
    private readonly BindingList<FateLayerTraceRow> _trace = new BindingList<FateLayerTraceRow>();
    private readonly BindingList<FateEffectRow> _effects = new BindingList<FateEffectRow>();

    private readonly Label _connectionStatus = new Label();
    private readonly Label _loginStatus = new Label();
    private readonly Label _stateStatus = new Label();
    private readonly CheckBox _engineEnabled = new CheckBox();
    private readonly DataGridView _layersGrid = new DataGridView();
    private readonly DataGridView _traceGrid = new DataGridView();
    private readonly DataGridView _effectsGrid = new DataGridView();
    private readonly ComboBox _selectedLayerEffectCombo = new ComboBox();
    private readonly TextBox _selectedLayerEffectDescription = new TextBox();
    private readonly NumericUpDown _dieSides = new NumericUpDown();
    private readonly NumericUpDown _baseRoll = new NumericUpDown();
    private readonly Label _fateResultLabel = new Label();
    private bool _updatingLayerEffectUi;

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

        var loadEffectsButton = new Button { Text = "Загрузить эффекты", Width = 140 };
        loadEffectsButton.Click += (_, __) => LoadEffects();

        var resetButton = new Button { Text = "Сбросить", Width = 100 };
        resetButton.Click += (_, __) => ResetAndSaveDefaults();

        var closeButton = new Button { Text = "Закрыть", Width = 100 };
        closeButton.Click += (_, __) => Close();

        _stateStatus.AutoSize = true;
        _stateStatus.Padding = new Padding(10, 10, 0, 0);

        bottomPanel.Controls.Add(loadButton);
        bottomPanel.Controls.Add(saveButton);
        bottomPanel.Controls.Add(loadEffectsButton);
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
        _layersGrid.Height = 190;
        _layersGrid.AutoGenerateColumns = false;
        _layersGrid.AllowUserToAddRows = false;
        _layersGrid.AllowUserToDeleteRows = false;
        _layersGrid.RowHeadersVisible = false;
        _layersGrid.DataSource = _layers;
        _layersGrid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            UpdateStatus($"Grid value error: row={e.RowIndex} column={e.ColumnIndex}");
        };

        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LayerNumber", HeaderText = "LayerNumber", DataPropertyName = "LayerNumber", ReadOnly = true, Width = 100 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "DisplayName", DataPropertyName = "DisplayName", Width = 180 });
        _layersGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Enabled", DataPropertyName = "Enabled", Width = 90 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FlatModifier", HeaderText = "FlatModifier", DataPropertyName = "FlatModifier", Width = 110 });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Intensity", HeaderText = "Intensity", DataPropertyName = "Intensity", Width = 100 });
        _layersGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Mode",
            HeaderText = "Mode",
            DataPropertyName = "Mode",
            Width = 140,
            DataSource = ModeOptions
        });
        _layersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "EffectCode",
            HeaderText = "EffectCode",
            DataPropertyName = "EffectCode",
            ReadOnly = true,
            Width = 160
        });
        _layersGrid.SelectionChanged += (_, __) => UpdateSelectedLayerEffectEditor();
        _layersGrid.CurrentCellDirtyStateChanged += (_, __) =>
        {
            if (_layersGrid.IsCurrentCellDirty)
            {
                _layersGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        var selectedLayerLabel = new Label
        {
            Left = 8,
            Top = 332,
            Width = 220,
            Text = "Эффект выбранного слоя"
        };

        _selectedLayerEffectCombo.Left = 230;
        _selectedLayerEffectCombo.Top = 328;
        _selectedLayerEffectCombo.Width = 260;
        _selectedLayerEffectCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _selectedLayerEffectCombo.DisplayMember = "Display";
        _selectedLayerEffectCombo.ValueMember = "Code";
        _selectedLayerEffectCombo.SelectedValueChanged += SelectedLayerEffectComboOnSelectedValueChanged;

        _selectedLayerEffectDescription.Left = 500;
        _selectedLayerEffectDescription.Top = 328;
        _selectedLayerEffectDescription.Width = 556;
        _selectedLayerEffectDescription.ReadOnly = true;

        _effectsGrid.Top = 234;
        _effectsGrid.Left = 8;
        _effectsGrid.Width = 1048;
        _effectsGrid.Height = 95;
        _effectsGrid.AutoGenerateColumns = false;
        _effectsGrid.AllowUserToAddRows = false;
        _effectsGrid.AllowUserToDeleteRows = false;
        _effectsGrid.ReadOnly = true;
        _effectsGrid.RowHeadersVisible = false;
        _effectsGrid.DataSource = _effects;
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Layer", DataPropertyName = "LayerNumber", Width = 50 });
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "LayerName", DataPropertyName = "LayerName", Width = 130 });
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "EffectCode", DataPropertyName = "EffectCode", Width = 130 });
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DisplayName", DataPropertyName = "DisplayName", Width = 150 });
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "InfluenceType", DataPropertyName = "InfluenceType", Width = 120 });
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Strength", DataPropertyName = "Strength", Width = 80 });
        _effectsGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Chaos", DataPropertyName = "CanUseChaos", Width = 55 });
        _effectsGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Anomaly", DataPropertyName = "CanUseAnomaly", Width = 65 });
        _effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description", Width = 260 });

        panel.Controls.Add(_engineEnabled);
        panel.Controls.Add(_layersGrid);
        panel.Controls.Add(_effectsGrid);
        panel.Controls.Add(selectedLayerLabel);
        panel.Controls.Add(_selectedLayerEffectCombo);
        panel.Controls.Add(_selectedLayerEffectDescription);
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

        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Layer", DataPropertyName = "LayerNumber", Width = 45 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "LayerName", Width = 100 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "EffectCode", DataPropertyName = "EffectCode", Width = 85 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Effect", DataPropertyName = "EffectDisplayName", Width = 110 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "InfluenceType", DataPropertyName = "InfluenceType", Width = 85 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Strength", DataPropertyName = "Strength", Width = 60 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Candidates", DataPropertyName = "CandidateRolls", Width = 120 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Selected", DataPropertyName = "SelectedValue", Width = 60 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DistShift", DataPropertyName = "DistributionShift", Width = 60 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Anomaly", DataPropertyName = "AnomalyShift", Width = 60 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chaos", DataPropertyName = "ChaosShift", Width = 60 });
        _traceGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Applied", DataPropertyName = "Applied", Width = 50 });
        _traceGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Allowed", DataPropertyName = "AllowedForDie", Width = 50 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Input", DataPropertyName = "InputValue", Width = 55 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modifier", DataPropertyName = "Modifier", Width = 55 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Output", DataPropertyName = "OutputValue", Width = 55 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", DataPropertyName = "CalculationDetails", Width = 220 });
        _traceGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Reason", Width = 180 });

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
            LoadEffects();
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
        UpdateStatus($"Settings loaded: enabled={enabled} layers={parsedLayers.Count} mods={mods}. Loaded effects: {BuildEffectSummary(parsedLayers)}");
    }

    private void SaveSettings()
    {
        if (!EnsureAuthorized()) return;

        var rowsToSave = ReadLayerRowsFromGrid();
        var modifiers = string.Join("/", rowsToSave.OrderBy(x => x.LayerNumber).Select(x => x.FlatModifier));
        var effectSummary = BuildEffectSummary(rowsToSave);
        UpdateStatus($"Saving: enabled={_engineEnabled.Checked} layers={rowsToSave.Count} mods={modifiers}. Saving effects: {effectSummary}");

        var response = _api.UpdateFateSettings(_engineEnabled.Checked, rowsToSave);
        if (response.Status != ResponseStatus.Ok)
        {
            ShowError($"fate.settings.update failed: status={response.Status} message={response.Message}");
            return;
        }

        ApplyLayers(rowsToSave);
        UpdateStatus($"Settings saved: status={response.Status} message={response.Message}. Sent effects: {effectSummary}");
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
            Mode = "flat",
            EffectCode = i == 1 ? "CalmArea" : i == 5 ? "Empty" : "None"
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
                Mode = "flat",
                EffectCode = i == 1 ? "CalmArea" : i == 5 ? "Empty" : "None"
            })
            .ToList();

        foreach (var row in normalized)
        {
            NormalizeLayerRow(row);
        }

        _layers.RaiseListChangedEvents = false;
        _layers.Clear();
        foreach (var row in normalized)
        {
            _layers.Add(row);
        }

        _layers.RaiseListChangedEvents = true;
        _layers.ResetBindings();
        UpdateSelectedLayerEffectEditor();
    }


    private void LoadEffects()
    {
        if (!EnsureAuthorized()) return;

        var response = _api.GetFateEffects();
        if (response.Status != ResponseStatus.Ok)
        {
            ShowError($"fate.effects.list failed: {response.Message}");
            return;
        }

        var effects = _api.ParseEffects(response);
        ApplyEffects(effects);
        UpdateSelectedLayerEffectEditor();
        UpdateStatus($"Effects loaded: total={effects.Count}");
    }

    private sealed class EffectOption
    {
        public string Code { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private BindingList<EffectOption> BuildEffectOptionsForLayer(int layerNumber)
    {
        var options = _effects
            .Where(x => x.LayerNumber == layerNumber)
            .Select(x => new EffectOption
            {
                Code = x.EffectCode,
                Display = $"{x.EffectCode} — {x.DisplayName}",
                Description = x.Description
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var fallback in GetFallbackEffectCodesForLayer(layerNumber))
        {
            if (options.All(x => !string.Equals(x.Code, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                options.Insert(0, new EffectOption
                {
                    Code = fallback,
                    Display = fallback,
                    Description = "Fallback value."
                });
            }
        }

        if (options.All(x => !string.Equals(x.Code, "None", StringComparison.OrdinalIgnoreCase)))
        {
            options.Insert(0, new EffectOption
            {
                Code = "None",
                Display = "None",
                Description = "Fallback value."
            });
        }

        if (layerNumber == 5 && options.All(x => !string.Equals(x.Code, "Empty", StringComparison.OrdinalIgnoreCase)))
        {
            options.Insert(1, new EffectOption
            {
                Code = "Empty",
                Display = "Empty",
                Description = "Fallback value."
            });
        }

        return new BindingList<EffectOption>(options);
    }

    private BindingList<string> BuildEffectCodesForLayer(int layerNumber)
    {
        var codes = BuildEffectOptionsForLayer(layerNumber)
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new BindingList<string>(codes);
    }

    private void UpdateSelectedLayerEffectEditor()
    {
        if (_layersGrid.CurrentRow?.DataBoundItem is not FateLayerRow layer)
        {
            _updatingLayerEffectUi = true;
            _selectedLayerEffectCombo.DataSource = new BindingList<EffectOption>();
            _selectedLayerEffectDescription.Text = string.Empty;
            _updatingLayerEffectUi = false;
            return;
        }

        var options = BuildEffectOptionsForLayer(layer.LayerNumber);
        if (!string.IsNullOrWhiteSpace(layer.EffectCode) &&
            options.All(x => !string.Equals(x.Code, layer.EffectCode, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new EffectOption
            {
                Code = layer.EffectCode,
                Display = layer.EffectCode,
                Description = "Value from current settings."
            });
        }

        _updatingLayerEffectUi = true;
        _selectedLayerEffectCombo.DataSource = options;
        var current = options.FirstOrDefault(x => string.Equals(x.Code, layer.EffectCode, StringComparison.OrdinalIgnoreCase));
        _selectedLayerEffectCombo.SelectedItem = current ?? options.FirstOrDefault();
        _selectedLayerEffectDescription.Text = (current ?? options.FirstOrDefault())?.Description ?? string.Empty;
        _updatingLayerEffectUi = false;
    }

    private void SelectedLayerEffectComboOnSelectedValueChanged(object? sender, EventArgs e)
    {
        if (_updatingLayerEffectUi)
        {
            return;
        }

        if (_layersGrid.CurrentRow?.DataBoundItem is not FateLayerRow layer)
        {
            return;
        }

        var selectedCode = (_selectedLayerEffectCombo.SelectedItem as EffectOption)?.Code
            ?? Convert.ToString(_selectedLayerEffectCombo.SelectedValue)
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(selectedCode))
        {
            return;
        }

        layer.EffectCode = selectedCode;
        NormalizeLayerRow(layer);
        _layers.ResetBindings();
        _layersGrid.Refresh();
        _selectedLayerEffectDescription.Text = (_selectedLayerEffectCombo.SelectedItem as EffectOption)?.Description ?? string.Empty;
        UpdateStatus($"Effect selected: layer={layer.LayerNumber} effect={layer.EffectCode}");
    }

    private static string GetDefaultEffectCodeForLayer(int layerNumber)
    {
        return layerNumber switch
        {
            1 => "CalmArea",
            5 => "Empty",
            _ => "None"
        };
    }

    private static string[] GetFallbackEffectCodesForLayer(int layerNumber)
    {
        var defaultCode = GetDefaultEffectCodeForLayer(layerNumber);
        return defaultCode == "None"
            ? new[] { "None" }
            : new[] { defaultCode, "None" };
    }

    private static string NormalizeMode(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "Normal";
        }

        return ModeOptions.Contains(mode, StringComparer.OrdinalIgnoreCase) ? mode : "Normal";
    }

    private void NormalizeLayerRow(FateLayerRow row)
    {
        row.Mode = NormalizeMode(row.Mode);

        if (string.IsNullOrWhiteSpace(row.EffectCode))
        {
            row.EffectCode = GetDefaultEffectCodeForLayer(row.LayerNumber);
            return;
        }

        if (_effects.Count == 0)
        {
            return;
        }

        var allowed = BuildEffectCodesForLayer(row.LayerNumber);
        if (!allowed.Contains(row.EffectCode, StringComparer.OrdinalIgnoreCase))
        {
            row.EffectCode = GetDefaultEffectCodeForLayer(row.LayerNumber);
        }
    }

    private void CommitGridEdits()
    {
        if (_layersGrid.IsCurrentCellDirty)
        {
            _layersGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        if (_layersGrid.EditingControl is ComboBox combo && _layersGrid.CurrentCell != null)
        {
            _layersGrid.CurrentCell.Value = combo.SelectedValue ?? combo.SelectedItem ?? combo.Text;
        }

        _layersGrid.EndEdit();
        Validate();
    }

    private void SyncLayerGridValuesToModel()
    {
        CommitGridEdits();
        foreach (DataGridViewRow gridRow in _layersGrid.Rows)
        {
            if (gridRow.DataBoundItem is not FateLayerRow layer)
            {
                continue;
            }

            layer.DisplayName = Convert.ToString(gridRow.Cells["DisplayName"].Value) ?? layer.DisplayName;
            layer.Enabled = ConvertToBool(gridRow.Cells["Enabled"].Value, layer.Enabled);
            layer.FlatModifier = ConvertToInt(gridRow.Cells["FlatModifier"].Value, layer.FlatModifier);
            layer.Intensity = ConvertToDouble(gridRow.Cells["Intensity"].Value, layer.Intensity);
            layer.Mode = Convert.ToString(gridRow.Cells["Mode"].Value) ?? layer.Mode;
            layer.EffectCode = Convert.ToString(gridRow.Cells["EffectCode"].Value) ?? layer.EffectCode;
            NormalizeLayerRow(layer);
        }
    }

    private System.Collections.Generic.List<FateLayerRow> ReadLayerRowsFromGrid()
    {
        CommitGridEdits();
        var result = new System.Collections.Generic.List<FateLayerRow>();

        foreach (DataGridViewRow row in _layersGrid.Rows)
        {
            if (row.IsNewRow) continue;

            var layerNumber = ConvertToInt(row.Cells["LayerNumber"].Value, 0);
            if (layerNumber < 1 || layerNumber > 5) continue;

            var effectCode = Convert.ToString(row.Cells["EffectCode"].Value);
            if (string.IsNullOrWhiteSpace(effectCode))
            {
                effectCode = row.DataBoundItem is FateLayerRow existing
                    ? existing.EffectCode
                    : GetDefaultEffectCodeForLayer(layerNumber);
            }

            var item = new FateLayerRow
            {
                LayerNumber = layerNumber,
                DisplayName = Convert.ToString(row.Cells["DisplayName"].Value) ?? GetDefaultLayerName(layerNumber),
                Enabled = ConvertToBool(row.Cells["Enabled"].Value, true),
                FlatModifier = ConvertToInt(row.Cells["FlatModifier"].Value, 0),
                Intensity = ConvertToDouble(row.Cells["Intensity"].Value, 1.0),
                Mode = Convert.ToString(row.Cells["Mode"].Value) ?? "Normal",
                EffectCode = effectCode
            };

            NormalizeLayerRow(item);
            result.Add(item);
        }

        return result.OrderBy(x => x.LayerNumber).ToList();
    }

    private static string GetDefaultLayerName(int layerNumber)
    {
        return $"Layer {layerNumber}";
    }

    private static int ConvertToInt(object? value, int fallback)
    {
        return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static double ConvertToDouble(object? value, double fallback)
    {
        return double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static bool ConvertToBool(object? value, bool fallback)
    {
        return bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static string BuildEffectSummary(System.Collections.Generic.IEnumerable<FateLayerRow> rows)
    {
        return string.Join(" ", rows.OrderBy(x => x.LayerNumber).Select(x => $"layer{x.LayerNumber}={x.EffectCode}"));
    }

    private void ApplyEffects(System.Collections.Generic.List<FateEffectRow> source)
    {
        _effects.RaiseListChangedEvents = false;
        _effects.Clear();
        foreach (var effect in source)
        {
            _effects.Add(effect);
        }

        _effects.RaiseListChangedEvents = true;
        _effects.ResetBindings();
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
