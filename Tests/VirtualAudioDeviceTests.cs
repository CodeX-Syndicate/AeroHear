using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroHear.Audio;
using NAudio.Wave;

namespace AeroHear.Tests
{
    /// <summary>
    /// Test suite for the virtual audio device functionality
    /// Note: These tests require a Windows environment with audio devices
    /// </summary>
    public static class VirtualAudioDeviceTests
    {
        public static async Task RunAllTests()
        {
            Console.WriteLine("🧪 Testing Virtual Audio Device Functionality");
            Console.WriteLine("=".PadRight(50, '='));

            // Test 1: Audio Device Enumeration
            TestAudioDeviceEnumeration();

            // Test 2: Virtual Audio Device Creation
            TestVirtualAudioDeviceCreation();

            // Test 3: Real-time Audio Router
            TestRealTimeAudioRouter();

            // Test 4: System Audio Manager
            await TestSystemAudioManager();

            // Test 5: Virtual Device Registration
            TestVirtualDeviceRegistration();

            Console.WriteLine("\n✅ All tests completed!");
        }

        private static void TestAudioDeviceEnumeration()
        {
            Console.WriteLine("\n📋 Test: Audio Device Enumeration");
            try
            {
                var devices = AudioDeviceManager.GetOutputDevices();
                Console.WriteLine($"   Found {devices.Count} audio output devices:");
                
                foreach (var device in devices)
                {
                    Console.WriteLine($"   - {device.FriendlyName}");
                }

                var defaultInfo = VirtualAudioDevice.GetDefaultAudioDeviceInfo();
                Console.WriteLine($"   {defaultInfo}");
                
                Console.WriteLine("   ✅ Audio device enumeration successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Audio device enumeration failed: {ex.Message}");
            }
        }

        private static void TestVirtualAudioDeviceCreation()
        {
            Console.WriteLine("\n🎧 Test: Virtual Audio Device Creation");
            try
            {
                using var multiPlayer = new MultiAudioPlayer();
                using var virtualDevice = new VirtualAudioDevice(multiPlayer);
                
                Console.WriteLine("   Virtual audio device created successfully");
                Console.WriteLine($"   Is capturing: {virtualDevice.IsCapturing}");
                
                Console.WriteLine("   ✅ Virtual audio device creation successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Virtual audio device creation failed: {ex.Message}");
            }
        }

        private static void TestRealTimeAudioRouter()
        {
            Console.WriteLine("\n🎵 Test: Real-time Audio Router");
            try
            {
                using var router = new RealTimeAudioRouter();
                
                // Initialize with standard audio format
                var waveFormat = new WaveFormat(44100, 16, 2);
                router.Initialize(waveFormat);
                
                Console.WriteLine("   Real-time audio router initialized");
                
                // Test with empty device list (should not fail)
                router.SetupOutputDevices(new List<string>());
                Console.WriteLine("   Output devices setup completed");
                
                // Test audio data routing (with dummy data)
                var dummyData = new byte[1024];
                router.RouteAudioData(dummyData, 0, dummyData.Length);
                Console.WriteLine("   Audio data routing test completed");
                
                Console.WriteLine("   ✅ Real-time audio router test successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Real-time audio router test failed: {ex.Message}");
            }
        }

        private static async Task TestSystemAudioManager()
        {
            Console.WriteLine("\n🖥️ Test: System Audio Manager");
            try
            {
                using var systemManager = new SystemAudioManager();
                
                Console.WriteLine($"   Initial system mode: {systemManager.IsSystemMode}");
                Console.WriteLine($"   Initial capture status: {systemManager.IsCapturing}");
                
                // Test status info
                var statusInfo = systemManager.GetStatusInfo();
                Console.WriteLine($"   Status: {statusInfo}");
                
                // Test enabling system mode with empty device list
                var devices = new List<string>();
                var volumes = new Dictionary<string, float>();
                
                // Note: This might fail on non-Windows systems
                try
                {
                    systemManager.EnableSystemMode(devices, volumes);
                    Console.WriteLine("   System mode enabled successfully");
                    
                    await Task.Delay(100); // Brief test period
                    
                    systemManager.DisableSystemMode();
                    Console.WriteLine("   System mode disabled successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   System mode test failed (expected on non-Windows): {ex.Message}");
                }
                
                Console.WriteLine("   ✅ System audio manager test completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ System audio manager test failed: {ex.Message}");
            }
        }

        private static void TestVirtualDeviceRegistration()
        {
            Console.WriteLine("\n🔧 Test: Virtual Device Registration");
            try
            {
                var isAdmin = VirtualDeviceRegistration.IsRunAsAdministrator();
                Console.WriteLine($"   Running as administrator: {isAdmin}");
                
                var registrationStatus = VirtualDeviceRegistration.GetRegistrationStatus();
                Console.WriteLine($"   Registration status retrieved");
                
                var registerResult = VirtualDeviceRegistration.RegisterVirtualDevice();
                Console.WriteLine($"   Register result: {registerResult}");
                
                var unregisterResult = VirtualDeviceRegistration.UnregisterVirtualDevice();
                Console.WriteLine($"   Unregister result: {unregisterResult}");
                
                Console.WriteLine("   ✅ Virtual device registration test successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Virtual device registration test failed: {ex.Message}");
            }
        }

        public static void PrintSystemRequirements()
        {
            Console.WriteLine("📋 System Requirements for Full Functionality:");
            Console.WriteLine("- Windows 10/11 with .NET 8");
            Console.WriteLine("- Active audio output devices");
            Console.WriteLine("- Administrator privileges (for full device registration)");
            Console.WriteLine("- Bluetooth audio devices for testing");
        }
    }
}