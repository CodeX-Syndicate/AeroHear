using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace AeroHear.Audio
{
    /// <summary>
    /// Manages system-wide audio capture and routing to multiple devices
    /// </summary>
    public class SystemAudioManager : IDisposable
    {
        private VirtualAudioDevice _virtualDevice;
        private RealTimeAudioRouter _audioRouter;
        private MultiAudioPlayer _filePlayer;
        private bool _isSystemMode;
        private List<string> _selectedDevices = new();
        private Dictionary<string, float> _deviceVolumes = new();

        public bool IsSystemMode => _isSystemMode;
        public bool IsCapturing => _virtualDevice?.IsCapturing ?? false;

        public event EventHandler<string> StatusChanged;

        public SystemAudioManager()
        {
            _filePlayer = new MultiAudioPlayer();
            _audioRouter = new RealTimeAudioRouter();
            _virtualDevice = new VirtualAudioDevice(_filePlayer);
            
            // Subscribe to audio data events
            _virtualDevice.DataAvailable += OnSystemAudioDataAvailable;
        }

        /// <summary>
        /// Switches to system-wide audio mode
        /// </summary>
        public void EnableSystemMode(List<string> deviceNames, Dictionary<string, float> deviceVolumes = null)
        {
            if (_isSystemMode) return;

            try
            {
                _selectedDevices = deviceNames?.ToList() ?? new List<string>();
                _deviceVolumes = deviceVolumes ?? new Dictionary<string, float>();

                // Stop any file playback
                _filePlayer.Stop();

                // Start system audio capture
                _virtualDevice.StartSystemAudioCapture();
                
                // Setup real-time audio routing
                var captureFormat = new WaveFormat(44100, 16, 2); // Standard format
                _audioRouter.Initialize(captureFormat);
                _audioRouter.SetupOutputDevices(_selectedDevices, _deviceVolumes);

                _isSystemMode = true;
                StatusChanged?.Invoke(this, "Mode système activé - AeroHear capture l'audio système");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Erreur activation mode système: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Switches back to file playback mode
        /// </summary>
        public void DisableSystemMode()
        {
            if (!_isSystemMode) return;

            try
            {
                _virtualDevice.StopSystemAudioCapture();
                _audioRouter.Stop();
                _isSystemMode = false;
                StatusChanged?.Invoke(this, "Mode fichier activé");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Erreur désactivation mode système: {ex.Message}");
            }
        }

        /// <summary>
        /// Plays a file to selected devices (file mode)
        /// </summary>
        public void PlayFile(string filePath, List<string> deviceNames, Dictionary<string, int> deviceDelays = null, Dictionary<string, float> deviceVolumes = null)
        {
            if (_isSystemMode)
            {
                throw new InvalidOperationException("Cannot play files while in system mode. Disable system mode first.");
            }

            _filePlayer.PlayToDevices(deviceNames, filePath, deviceDelays, deviceVolumes);
        }

        /// <summary>
        /// Stops all audio playback
        /// </summary>
        public void StopAll()
        {
            if (_isSystemMode)
            {
                DisableSystemMode();
            }
            else
            {
                _filePlayer.Stop();
            }
        }

        /// <summary>
        /// Updates device selection for system mode
        /// </summary>
        public void UpdateSystemModeDevices(List<string> deviceNames, Dictionary<string, float> deviceVolumes = null)
        {
            if (!_isSystemMode) return;

            _selectedDevices = deviceNames?.ToList() ?? new List<string>();
            _deviceVolumes = deviceVolumes ?? new Dictionary<string, float>();
            
            // Restart audio routing with new devices
            _audioRouter.SetupOutputDevices(_selectedDevices, _deviceVolumes);
            StatusChanged?.Invoke(this, $"Périphériques mis à jour: {string.Join(", ", _selectedDevices)}");
        }

        /// <summary>
        /// Gets information about the current audio setup
        /// </summary>
        public string GetStatusInfo()
        {
            if (_isSystemMode)
            {
                var deviceList = string.Join(", ", _selectedDevices);
                return $"Mode système actif - Périphériques: {deviceList}";
            }
            else
            {
                return "Mode fichier actif";
            }
        }

        private void OnSystemAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_isSystemMode && e.BytesRecorded > 0)
            {
                // Route captured system audio to all selected devices
                _audioRouter.RouteAudioData(e.Buffer, 0, e.BytesRecorded);
            }
        }

        public void Dispose()
        {
            DisableSystemMode();
            _virtualDevice?.Dispose();
            _audioRouter?.Dispose();
            _filePlayer?.Stop();
        }
    }
}