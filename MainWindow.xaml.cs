using System.Text;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using NAudio.Wave.SampleProviders;

namespace AeroHear
{
    public class AudioDeviceModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; } = string.Empty;
        public MMDevice Device { get; set; } = null!;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public double DeviceVolume
        {
            get
            {
                try { return Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100; }
                catch { return 50; }
            }
            set
            {
                try
                {
                    Device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(value / 100.0);
                    OnPropertyChanged();
                }
                catch { }
            }
        }

        public bool IsMuted
        {
            get
            {
                try { return Device.AudioEndpointVolume.Mute; }
                catch { return false; }
            }
            set
            {
                try
                {
                    Device.AudioEndpointVolume.Mute = value;
                    OnPropertyChanged();
                }
                catch { }
            }
        }

        private int _delayMs;
        public int DelayMs
        {
            get => _delayMs;
            set
            {
                if (_delayMs != value)
                {
                    _delayMs = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<AudioDeviceModel> AudioDevices { get; set; } = new();

        private class ActiveOutput
        {
            public WasapiOut WasapiOut { get; set; } = null!;
            public BufferedWaveProvider Buffer { get; set; } = null!;
            public AudioDeviceModel DeviceModel { get; set; } = null!;
        }

        private WaveStream? _audioStream;
        private IWaveProvider? _finalProvider;
        private VolumeSampleProvider? _volumeProvider;
        private WasapiLoopbackCapture? _loopbackCapture;

        private readonly List<ActiveOutput> _activeOutputs = new();
        private CancellationTokenSource? _playbackCts;
        private string _selectedFilePath = string.Empty;

        private DispatcherTimer _timer = null!;
        private bool _isDraggingSlider = false;

        private Dictionary<string, int> _savedDelays = new();
        private readonly string _settingsFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "device_delays.json");

        private void LoadDelaysSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (dict != null) _savedDelays = dict;
                }
            }
            catch { }
        }

        private void SaveDelaysSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_savedDelays);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadDelaysSettings();
            LoadAudioDevices();
            this.Closed += (s, e) => StopPlayback();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isDraggingSlider && _audioStream != null)
            {
                ProgressSlider.Value = _audioStream.CurrentTime.TotalSeconds;
                CurrentTimeText.Text = _audioStream.CurrentTime.ToString(@"mm\:ss");
            }
            else if (_loopbackCapture != null)
            {
                // UI disabled for loopback
                CurrentTimeText.Text = "Live";
                TotalTimeText.Text = "Live";
            }
        }

        private void AudioDeviceModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AudioDeviceModel.DelayMs) && sender is AudioDeviceModel model)
            {
                // Sauvegarder automatiquement ce réglage pour ce périphérique
                _savedDelays[model.Name] = model.DelayMs;
                SaveDelaysSettings();

                // Resynchroniser à la volée quand le slider modifie le délai.
                if (_audioStream != null && _finalProvider != null)
                {
                    if (_audioStream.CanSeek)
                    {
                        try { _audioStream.CurrentTime = _audioStream.CurrentTime; } catch { }
                    }
                    foreach (var output in _activeOutputs)
                    {
                        output.Buffer.ClearBuffer(); 
                        AddSilence(output.Buffer, output.DeviceModel.DelayMs, _finalProvider.WaveFormat);
                    }
                }
                else if (_loopbackCapture != null)
                {
                    foreach (var output in _activeOutputs)
                    {
                        output.Buffer.ClearBuffer(); 
                        AddSilence(output.Buffer, output.DeviceModel.DelayMs, _loopbackCapture.WaveFormat);
                    }
                }
            }
        }

        private void AddSilence(BufferedWaveProvider buffer, int delayMs, WaveFormat format)
        {
            if (delayMs <= 0) return;
            int bytes = (int)((delayMs / 1000.0) * format.AverageBytesPerSecond);
            bytes -= bytes % format.BlockAlign; 
            if (bytes > 0)
            {
                byte[] silence = new byte[bytes];
                buffer.AddSamples(silence, 0, bytes);
            }
        }

        private void LoadAudioDevices()
        {
            AudioDevices.Clear();
            var enumerator = new MMDeviceEnumerator();
            // Récupère uniquement les périphériques de lecture actifs
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var addedNames = new HashSet<string>();
            foreach (var endpoint in endpoints)
            {
                if (!addedNames.Add(endpoint.FriendlyName)) continue;

                int delay = _savedDelays.TryGetValue(endpoint.FriendlyName, out int d) ? d : 0;
                var model = new AudioDeviceModel
                {
                    Name = endpoint.FriendlyName,
                    Device = endpoint,
                    IsSelected = false,
                    DelayMs = delay
                };
                model.PropertyChanged += AudioDeviceModel_PropertyChanged;
                AudioDevices.Add(model);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAudioDevices();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Fichiers Audio (*.mp3;*.wav)|*.mp3;*.wav|Tous les fichiers (*.*)|*.*",
                Title = "Sélectionner une musique"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                SourceTextBox.Text = _selectedFilePath;
            }
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            string sourceUrl = SourceTextBox.Text;
            if (string.IsNullOrEmpty(sourceUrl))
            {
                MessageBox.Show("Veuillez entrer une URL YouTube ou sélectionner un fichier audio.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedDevices = AudioDevices.Where(d => d.IsSelected).ToList();
            if (!selectedDevices.Any())
            {
                MessageBox.Show("Veuillez sélectionner au moins un périphérique audio.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StopPlayback();
            SourceTextBox.IsReadOnly = true;

            try
            {
                if (sourceUrl.Contains("youtube.com") || sourceUrl.Contains("youtu.be"))
                {
                    SourceTextBox.Text = "Chargement de la vidéo YouTube...";
                    var youtube = new YoutubeClient();
                    var manifest = await youtube.Videos.Streams.GetManifestAsync(sourceUrl);
                    var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    _audioStream = new MediaFoundationReader(streamInfo.Url);
                    _volumeProvider = new VolumeSampleProvider(_audioStream.ToSampleProvider()) { Volume = (float)VolumeSlider.Value };
                    _finalProvider = _volumeProvider.ToWaveProvider(); // Gives 32 bit float provider which is fine for WasapiOut
                    SourceTextBox.Text = sourceUrl;
                }
                else
                {
                    var afr = new AudioFileReader(sourceUrl);
                    afr.Volume = (float)VolumeSlider.Value;
                    _audioStream = afr;
                    _volumeProvider = null;
                    _finalProvider = afr;
                }

                ProgressSlider.Maximum = _audioStream.TotalTime.TotalSeconds;
                TotalTimeText.Text = _audioStream.TotalTime.ToString(@"mm\:ss");

                _activeOutputs.Clear();

                foreach (var deviceModel in selectedDevices)
                {
                    if (deviceModel.IsMuted) deviceModel.IsMuted = false;
                    if (deviceModel.DeviceVolume < 10) deviceModel.DeviceVolume = 50;

                    var wasapiOut = new WasapiOut(deviceModel.Device, AudioClientShareMode.Shared, true, 50);
                    var buffer = new BufferedWaveProvider(_finalProvider.WaveFormat)
                    {
                        BufferDuration = TimeSpan.FromSeconds(5),
                        DiscardOnBufferOverflow = true
                    };

                    wasapiOut.Init(buffer);

                    _activeOutputs.Add(new ActiveOutput { WasapiOut = wasapiOut, Buffer = buffer, DeviceModel = deviceModel });
                    AddSilence(buffer, deviceModel.DelayMs, _finalProvider.WaveFormat);
                }

                _playbackCts = new CancellationTokenSource();

                // On précharge 500ms d'audio pour éviter que WasapiOut ne démarre avec un buffer vide (qui rajouterait du silence hasardeux)
                byte[] preloadBuffer = new byte[_finalProvider.WaveFormat.AverageBytesPerSecond / 2];
                int preloadRead = _finalProvider.Read(preloadBuffer, 0, preloadBuffer.Length);
                if (preloadRead > 0)
                {
                    foreach (var output in _activeOutputs)
                    {
                        output.Buffer.AddSamples(preloadBuffer, 0, preloadRead);
                    }
                }

                // On lance la lecture au même instant pour tous les périphériques !
                foreach (var output in _activeOutputs)
                {
                    output.WasapiOut.Play();
                }

                _timer.Start();
                _ = PumpAudioAsync(_playbackCts.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la lecture : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                StopPlayback();
            }
        }

        private void CapturePcButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevices = AudioDevices.Where(d => d.IsSelected).ToList();
            if (!selectedDevices.Any())
            {
                MessageBox.Show("Veuillez sélectionner au moins un périphérique audio.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StopPlayback();
            SourceTextBox.IsReadOnly = true;
            SourceTextBox.Text = "Mode Écoute PC actif (Loopback)...";

            try
            {
                _loopbackCapture = new WasapiLoopbackCapture();
                var format = _loopbackCapture.WaveFormat;
                _activeOutputs.Clear();

                foreach (var deviceModel in selectedDevices)
                {
                    if (deviceModel.IsMuted) deviceModel.IsMuted = false;
                    if (deviceModel.DeviceVolume < 10) deviceModel.DeviceVolume = 50;

                    var wasapiOut = new WasapiOut(deviceModel.Device, AudioClientShareMode.Shared, true, 50);
                    var buffer = new BufferedWaveProvider(format)
                    {
                        BufferDuration = TimeSpan.FromSeconds(5),
                        DiscardOnBufferOverflow = true
                    };

                    wasapiOut.Init(buffer);

                    _activeOutputs.Add(new ActiveOutput { WasapiOut = wasapiOut, Buffer = buffer, DeviceModel = deviceModel });
                    AddSilence(buffer, deviceModel.DelayMs, format);
                }

                _loopbackCapture.DataAvailable += (s, args) =>
                {
                    foreach (var output in _activeOutputs)
                    {
                        output.Buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
                    }
                };

                _loopbackCapture.RecordingStopped += (s, args) => { StopPlayback(); };

                // On lance la lecture juste avant l'enregistrement pour ne pas avoir de consommation de silence non-contrôlée
                foreach (var output in _activeOutputs)
                {
                    output.WasapiOut.Play();
                }

                _loopbackCapture.StartRecording();
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la capture : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                StopPlayback();
            }
        }

        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDraggingSlider = false;
            if (_audioStream != null && _finalProvider != null && _audioStream.CanSeek)
            {
                try { _audioStream.CurrentTime = TimeSpan.FromSeconds(ProgressSlider.Value); } catch { }
                foreach (var output in _activeOutputs)
                {
                    output.Buffer.ClearBuffer(); 
                    AddSilence(output.Buffer, output.DeviceModel.DelayMs, _finalProvider.WaveFormat);
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
        }

        private void DecreaseDelay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AudioDeviceModel model)
            {
                if (model.DelayMs >= 10) model.DelayMs -= 10;
                else model.DelayMs = 0;
            }
        }

        private void IncreaseDelay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AudioDeviceModel model)
            {
                if (model.DelayMs <= 1490) model.DelayMs += 10;
                else model.DelayMs = 1500;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioStream is AudioFileReader afr)
            {
                afr.Volume = (float)VolumeSlider.Value;
            }
            else if (_volumeProvider != null)
            {
                _volumeProvider.Volume = (float)VolumeSlider.Value;
            }
        }

        private void StopPlayback()
        {
            _timer?.Stop();
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = null;

            if (_loopbackCapture != null)
            {
                _loopbackCapture.StopRecording();
                _loopbackCapture.Dispose();
                _loopbackCapture = null;
            }

            foreach (var output in _activeOutputs)
            {
                output.WasapiOut.Stop();
                output.WasapiOut.Dispose();
            }
            _activeOutputs.Clear();

            _audioStream?.Dispose();
            _audioStream = null;
            _finalProvider = null;
            _volumeProvider = null;

            if (SourceTextBox != null)
                SourceTextBox.IsReadOnly = false;
        }

        private async Task PumpAudioAsync(CancellationToken token)
        {
            if (_finalProvider == null) return;

            byte[] readBuffer = new byte[_finalProvider.WaveFormat.AverageBytesPerSecond / 10]; // 100ms buffer

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // On empêche la mémoire de déborder en vérifiant l'état des buffers pour chaque sortie (en prenant en compte son délai)
                    if (_activeOutputs.Any(o => o.Buffer.BufferedDuration.TotalSeconds > 1.5 + (o.DeviceModel.DelayMs / 1000.0)))
                    {
                        await Task.Delay(50, token);
                        continue;
                    }

                    int bytesRead = _finalProvider.Read(readBuffer, 0, readBuffer.Length);
                    if (bytesRead == 0)
                    {
                        break; // Fin du fichier
                    }

                    foreach (var output in _activeOutputs)
                    {
                        output.Buffer.AddSamples(readBuffer, 0, bytesRead);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal de la lecture
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur pendant le relais audio : {ex.Message}");
            }
        }
    }
}