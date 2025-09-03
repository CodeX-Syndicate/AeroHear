using System;
using System.Threading.Tasks;
using AeroHear.Tests;

namespace AeroHear.TestRunner
{
    /// <summary>
    /// Console application to test virtual audio device functionality
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🎵 AeroHear Virtual Audio Device Test Runner");
            Console.WriteLine("=".PadRight(50, '='));
            
            VirtualAudioDeviceTests.PrintSystemRequirements();
            
            Console.WriteLine("\nPress Enter to run tests or 'q' to quit...");
            var input = Console.ReadLine();
            
            if (input?.ToLower() == "q")
            {
                return;
            }
            
            try
            {
                await VirtualAudioDeviceTests.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test execution failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }
}