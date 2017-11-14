using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Steamless.API.Model;
using Steamless.API.Services;
using Steamless.Unpacker.Variant31.x64;

namespace NiohResolution
{
    public class Program
    {
        private const string EXE_FILE = "nioh.exe";
        private const string EXE_FILE_BACKUP = "nioh.exe.bak";
        private const string EXE_FILE_UNPACKED = "nioh.exe.unpacked.exe";

        private static readonly byte[] _exePattern = { 0x80, 0x07, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00 };

        public static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Nioh Resolution patcher!");

            Console.WriteLine("\nPlease specify your resolution.");

            int width = ReadInt("Width");
            int height = ReadInt("Height");

            if (!File.Exists(EXE_FILE))
            {
                Console.WriteLine($"\nCould not find {EXE_FILE}!");

                Exit();

                return;
            }

            Console.WriteLine($"\nBacking up {EXE_FILE}...");

            File.Copy(EXE_FILE, EXE_FILE_BACKUP, true);

            Console.WriteLine($"Unpacking {EXE_FILE}...");

            var result = UnpackExe();
            if (!result)
            {
                return;
            }
            
            Console.WriteLine($"Patching resolution of {width}x{height}...");

            var buffer = File.ReadAllBytes(EXE_FILE_UNPACKED);
            var positions = FindSequence(buffer, _exePattern, 0);
            var resolution = ConvertToBytes(width, height);

            if (!positions.Any())
            {
                Console.WriteLine($"Could not find any offsets in {EXE_FILE_UNPACKED}!");

                Exit();

                return;
            }

            foreach (int position in positions)
            {
                Console.WriteLine($"Patching offset {position}...");

                for (int i = 0; i < resolution.Length; i++)
                {
                    buffer[position + i] = resolution[i];
                }
            }

            Console.WriteLine("Cleaning up...");

            File.WriteAllBytes(EXE_FILE, buffer);
            File.Delete(EXE_FILE_UNPACKED);

            Console.WriteLine("\nDone! Don't forget to set the resolution of the game to 1920x1080!");

            Exit();
        }

        private static bool UnpackExe()
        {
            LoggingService loggingService = new LoggingService();
            loggingService.AddLogMessage += (sender, eventArgs) =>
            {
                Console.WriteLine(eventArgs.Message);
            };

            SteamlessPlugin plugin = new Main();
            plugin.Initialize(loggingService);

            var result = plugin.CanProcessFile(EXE_FILE);

            if (!result)
            {
                Console.WriteLine($"Cannot process {EXE_FILE}!");

                Exit();

                return false;
            }

            result = plugin.ProcessFile(EXE_FILE, new SteamlessOptions
            {
                VerboseOutput = false,
                KeepBindSection = true
            });

            if (!result)
            {
                Console.WriteLine($"Could not process {EXE_FILE}!");

                Exit();

                return false;
            }

            return true;
        }

        private static int ReadInt(string name)
        {
            int input;

            do
            {
                Console.Write($"{name}: ");

                int.TryParse(Console.ReadLine(), out input);

                if (input <= 0)
                {
                    Console.WriteLine($"That's not a valid {name.ToLower()}, try again!");
                }
            } while (input <= 0);

            return input;
        }

        private static byte[] ConvertToBytes(int width, int height)
        {
            byte[] widthBytes = BitConverter.GetBytes(width);
            byte[] heightBytes = BitConverter.GetBytes(height);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(widthBytes);
                Array.Reverse(heightBytes);
            }

            return widthBytes.Concat(heightBytes).ToArray();
        }

        //Source: https://stackoverflow.com/questions/283456/byte-array-pattern-search
        private static List<int> FindSequence(byte[] buffer, byte[] pattern, int startIndex)
        {
            List<int> positions = new List<int>();

            int i = Array.IndexOf(buffer, pattern[0], startIndex);

            while (i >= 0 && i <= buffer.Length - pattern.Length)
            {
                byte[] segment = new byte[pattern.Length];

                Buffer.BlockCopy(buffer, i, segment, 0, pattern.Length);

                if (segment.SequenceEqual(pattern))
                {
                    positions.Add(i);
                }

                i = Array.IndexOf(buffer, pattern[0], i + pattern.Length);
            }

            return positions;
        }

        private static void Exit()
        {
            Console.ReadKey();
        }
    }
}
