using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AeroHear.Audio
{
    /// <summary>
    /// Manages virtual audio device creation and system audio capture
    /// </summary>
    public class VirtualAudioDevice : IDisposable
    {
        private WasapiLoopbackCapture _loopbackCapture;
        private MultiAudioPlayer _multiPlayer;
        private bool _isCapturing;
        private readonly object _lock = new object();

        public event EventHandler<WaveInEventArgs> DataAvailable;
        public bool IsCapturing => _isCapturing;

        public VirtualAudioDevice(MultiAudioPlayer multiPlayer)
        {
            _multiPlayer = multiPlayer ?? throw new ArgumentNullException(nameof(multiPlayer));
        }

        /// <summary>
        /// Starts capturing system audio and routing to selected devices
        /// </summary>
        public void StartSystemAudioCapture()
        {
            lock (_lock)
            {
                if (_isCapturing) return;

                try
                {
                    // Use WASAPI loopback to capture system audio
                    _loopbackCapture = new WasapiLoopbackCapture();
                    _loopbackCapture.DataAvailable += OnDataAvailable;
                    _loopbackCapture.RecordingStopped += OnRecordingStopped;
                    
                    _loopbackCapture.StartRecording();
                    _isCapturing = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to start system audio capture: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Stops system audio capture
        /// </summary>
        public void StopSystemAudioCapture()
        {
            lock (_lock)
            {
                if (!_isCapturing) return;

                try
                {
                    _loopbackCapture?.StopRecording();
                    _isCapturing = false;
                }
                catch (Exception ex)
                {
                    // Log error but don't throw during stop operation
                    Console.WriteLine($"Error stopping capture: {ex.Message}");
                }
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Forward the captured audio data to external handlers
            DataAvailable?.Invoke(sender, e);
            
            // TODO: Route audio data to selected Bluetooth devices in real-time
            // This would require extending MultiAudioPlayer to handle streaming data
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            _isCapturing = false;
            if (e.Exception != null)
            {
                Console.WriteLine($"Recording stopped with error: {e.Exception.Message}");
            }
        }

        /// <summary>
        /// Gets information about the default audio device
        /// </summary>
        public static string GetDefaultAudioDeviceInfo()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return $"Default Device: {defaultDevice.FriendlyName}";
            }
            catch (Exception ex)
            {
                return $"Unable to get default device: {ex.Message}";
            }
        }

        public void Dispose()
        {
            StopSystemAudioCapture();
            _loopbackCapture?.Dispose();
        }
    }
}