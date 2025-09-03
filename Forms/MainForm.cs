using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AeroHear.Audio;
using AeroHear.Utils;
using NAudio.CoreAudioApi;

namespace AeroHear.Forms
{
    public partial class MainForm : Form
    {
        private readonly MultiAudioPlayer _player = new();
        private readonly SystemAudioManager _systemAudioManager = new();
        private readonly List<MMDevice> _devices = AudioDeviceManager.GetOutputDevices();
        private readonly List<CheckBox> _deviceCheckboxes = new();
        private DelayCalibrationControl _delayCalibration;

        private string _audioFilePath = "";
        private CheckBox _systemModeCheckbox;
        private Label _statusLabel;

        public MainForm()
        {
            InitializeComponent();
            InitializeUI();
            
            // Subscribe to system audio manager events
            _systemAudioManager.StatusChanged += OnSystemAudioStatusChanged;
        }

        private void InitializeUI()
        {
            Text = "AeroHear - Diffusion Audio Multi-Périphériques";
            Width = 750; // Increased for new button
            Height = 650; // Increased to accommodate new controls

            // System mode toggle
            _systemModeCheckbox = new CheckBox 
            { 
                Text = "Mode système (capture audio PC)", 
                Top = 10, 
                Left = 20, 
                Width = 250,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
            };
            _systemModeCheckbox.CheckedChanged += SystemModeCheckbox_CheckedChanged;
            Controls.Add(_systemModeCheckbox);

            // Status label
            _statusLabel = new Label
            {
                Text = "Mode fichier actif",
                Top = 35,
                Left = 20,
                Width = 650,
                Height = 20,
                ForeColor = System.Drawing.Color.DarkGreen
            };
            Controls.Add(_statusLabel);

            var btnLoad = new Button { Text = "Charger audio", Top = 65, Left = 20, Width = 120 };
            btnLoad.Click += BtnLoad_Click;
            Controls.Add(btnLoad);

            var btnPlay = new Button { Text = "Lire", Top = 65, Left = 150, Width = 100 };
            btnPlay.Click += BtnPlay_Click;
            Controls.Add(btnPlay);

            var btnStop = new Button { Text = "Stop", Top = 65, Left = 260, Width = 100 };
            btnStop.Click += (s, e) => 
            {
                _player.Stop();
                _systemAudioManager.StopAll();
            };
            Controls.Add(btnStop);

            var btnTest = new Button { Text = "Tester latence", Top = 65, Left = 370, Width = 120 };
            btnTest.Click += async (s, e) =>
            {
                int ms = await LatencyTester.EstimateLatencyAsync(() => System.Media.SystemSounds.Beep.Play());
                MessageBox.Show($"Latence estimée : {ms} ms");
            };
            Controls.Add(btnTest);

            // Device info button
            var btnDeviceInfo = new Button { Text = "Info Périph.", Top = 65, Left = 500, Width = 100 };
            btnDeviceInfo.Click += (s, e) =>
            {
                var info = VirtualAudioDevice.GetDefaultAudioDeviceInfo();
                MessageBox.Show(info, "Information Périphérique Audio");
            };
            Controls.Add(btnDeviceInfo);

            // Help button for virtual device
            var btnHelp = new Button { Text = "Aide Mode Système", Top = 65, Left = 610, Width = 120 };
            btnHelp.Click += (s, e) =>
            {
                using var helpForm = new VirtualDeviceHelpForm();
                helpForm.ShowDialog(this);
            };
            Controls.Add(btnHelp);

            var grp = new GroupBox { Text = "Périphériques Bluetooth", Top = 105, Left = 20, Width = 650, Height = 200 };
            Controls.Add(grp);

            int y = 20;
            foreach (var device in _devices)
            {
                var check = new CheckBox
                {
                    Text = device.FriendlyName,
                    Left = 10,
                    Top = y,
                    Width = 600
                };
                check.CheckedChanged += DeviceCheckbox_CheckedChanged;
                grp.Controls.Add(check);
                _deviceCheckboxes.Add(check);
                y += 25;
            }

            _delayCalibration = new DelayCalibrationControl(_devices)
            {
                Left = 20,
                Top = 325,
                Width = 650
                // Height is now set dynamically by the control itself
            };
            Controls.Add(_delayCalibration);
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Fichiers audio (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac|Tous les fichiers (*.*)|*.*"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _audioFilePath = dlg.FileName;
                MessageBox.Show("Fichier chargé : " + Path.GetFileName(_audioFilePath));
            }
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (_systemModeCheckbox.Checked)
            {
                MessageBox.Show("Mode système actif. Utilisez la case à cocher pour démarrer/arrêter la capture audio.");
                return;
            }

            if (string.IsNullOrEmpty(_audioFilePath))
            {
                MessageBox.Show("Veuillez charger un fichier audio.");
                return;
            }

            var selected = _deviceCheckboxes
                .Where(c => c.Checked)
                .Select(c => c.Text)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show("Veuillez sélectionner au moins un périphérique Bluetooth.");
                return;
            }

            var delays = _delayCalibration.GetDelays();
            var volumes = _delayCalibration.GetVolumes();
            
            try
            {
                _systemAudioManager.PlayFile(_audioFilePath, selected, delays, volumes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la lecture: {ex.Message}");
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void SystemModeCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (_systemModeCheckbox.Checked)
                {
                    var selected = _deviceCheckboxes
                        .Where(c => c.Checked)
                        .Select(c => c.Text)
                        .ToList();

                    if (selected.Count == 0)
                    {
                        MessageBox.Show("Veuillez sélectionner au moins un périphérique avant d'activer le mode système.");
                        _systemModeCheckbox.Checked = false;
                        return;
                    }

                    var volumes = _delayCalibration.GetVolumes();
                    _systemAudioManager.EnableSystemMode(selected, volumes);
                }
                else
                {
                    _systemAudioManager.DisableSystemMode();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur changement de mode: {ex.Message}");
                _systemModeCheckbox.Checked = false;
            }
        }

        private void DeviceCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            // Update system mode devices if currently in system mode
            if (_systemModeCheckbox.Checked && _systemAudioManager.IsSystemMode)
            {
                var selected = _deviceCheckboxes
                    .Where(c => c.Checked)
                    .Select(c => c.Text)
                    .ToList();

                if (selected.Count > 0)
                {
                    var volumes = _delayCalibration.GetVolumes();
                    _systemAudioManager.UpdateSystemModeDevices(selected, volumes);
                }
            }
        }

        private void OnSystemAudioStatusChanged(object sender, string status)
        {
            // Update status label on UI thread
            if (InvokeRequired)
            {
                Invoke(new Action(() => _statusLabel.Text = status));
            }
            else
            {
                _statusLabel.Text = status;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _systemAudioManager?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
