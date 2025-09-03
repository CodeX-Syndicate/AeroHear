using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AeroHear.Audio
{
    /// <summary>
    /// Handles real-time audio streaming to multiple devices
    /// </summary>
    public class RealTimeAudioRouter : IDisposable
    {
        private readonly Dictionary<string, WaveOutEvent> _outputDevices = new();
        private readonly Dictionary<string, BufferedWaveProvider> _bufferProviders = new();
        private readonly object _lock = new object();
        private WaveFormat _waveFormat;
        private bool _isInitialized;

        /// <summary>
        /// Initializes the router with the specified audio format
        /// </summary>
        public void Initialize(WaveFormat waveFormat)
        {
            _waveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
            _isInitialized = true;
        }

        /// <summary>
        /// Sets up output devices for real-time streaming
        /// </summary>
        public void SetupOutputDevices(List<string> deviceNames, Dictionary<string, float> deviceVolumes = null)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Router not initialized. Call Initialize() first.");

            lock (_lock)
            {
                // Clean up existing devices
                ClearOutputDevices();

                foreach (var deviceName in deviceNames)
                {
                    try
                    {
                        var deviceNumber = AudioDeviceManager.GetWaveOutDeviceNumber(deviceName);
                        if (deviceNumber == -1) continue;

                        // Create buffered wave provider for this device
                        var bufferProvider = new BufferedWaveProvider(_waveFormat)
                        {
                            BufferLength = _waveFormat.AverageBytesPerSecond * 2, // 2 seconds buffer
                            DiscardOnBufferOverflow = true
                        };

                        // Apply volume if specified
                        ISampleProvider sampleProvider = bufferProvider.ToSampleProvider();
                        if (deviceVolumes != null && deviceVolumes.ContainsKey(deviceName))
                        {
                            var volumeProvider = new VolumeSampleProvider(sampleProvider)
                            {
                                Volume = Math.Max(0.0f, Math.Min(1.0f, deviceVolumes[deviceName]))
                            };
                            sampleProvider = volumeProvider;
                        }

                        // Create output device
                        var outputDevice = new WaveOutEvent { DeviceNumber = deviceNumber };
                        outputDevice.Init(sampleProvider);
                        outputDevice.Play();

                        _outputDevices[deviceName] = outputDevice;
                        _bufferProviders[deviceName] = bufferProvider;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to setup device {deviceName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Routes audio data to all configured output devices
        /// </summary>
        public void RouteAudioData(byte[] audioData, int offset, int length)
        {
            if (!_isInitialized || audioData == null) return;

            lock (_lock)
            {
                foreach (var bufferProvider in _bufferProviders.Values)
                {
                    try
                    {
                        // Add audio data to buffer - this will be played automatically
                        bufferProvider.AddSamples(audioData, offset, length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error routing audio data: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Updates volume for a specific device
        /// </summary>
        public void UpdateDeviceVolume(string deviceName, float volume)
        {
            lock (_lock)
            {
                if (_outputDevices.ContainsKey(deviceName))
                {
                    // Note: Volume changes would require recreating the sample provider chain
                    // For simplicity, this could be implemented by stopping and restarting the device
                    Console.WriteLine($"Volume update for {deviceName} to {volume} (requires restart)");
                }
            }
        }

        /// <summary>
        /// Stops all audio output and clears devices
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                ClearOutputDevices();
            }
        }

        private void ClearOutputDevices()
        {
            foreach (var device in _outputDevices.Values)
            {
                try
                {
                    device.Stop();
                    device.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing output device: {ex.Message}");
                }
            }

            _outputDevices.Clear();
            _bufferProviders.Clear();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}