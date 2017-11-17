using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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

        private const string PATTERN_ASPECTRATIO1 = "C7 43 50 39 8E E3 3F";
        private const int PATTERN_ASPECTRATIO1_OFFSET = 3;

        private const string PATTERN_ASPECTRATIO2 = "00 00 87 44 00 00 F0 44";

        private const string PATTERN_MAGIC1 = "0F 95 C0 88 46 34";
        private const string PATTERN_MAGIC1_PATCH = "32 C0 90 88 46 34";

        private const string PATTERN_MAGIC2_A = "45 85 D2 7E 1A";
        private const string PATTERN_MAGIC2_A_PATCH = "45 85 D2 EB 1A";

        private const string PATTERN_MAGIC2_B = "C3 79 14";
        private const string PATTERN_MAGIC2_B_PATCH = "C3 EB 14";

        private const string PATTERN_RESOLUTION = "80 07 00 00 38 04 00 00";

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Console.WriteLine("Welcome to the Nioh Resolution patcher!");

            Console.WriteLine("\nPlease enter your desired resolution.\n");

            int width = ReadInt("Width", 1920);
            int height = ReadInt("Height", 1080);

            Console.WriteLine("\nPlease enter a scale for your desired render resolution.\n");
            Console.WriteLine("Keep in mind that this scale:");
            Console.WriteLine("- Has no effect on the entered resolution.");
            Console.WriteLine("- Has no effect on the in-game UI.");
            Console.WriteLine("- Can less than 1.0 to increase performance.");
            Console.WriteLine();

            float scale = ReadFloat("Scale", 1.0f);

            if (!File.Exists(EXE_FILE))
            {
                Console.WriteLine($"\nCould not find {EXE_FILE}!");

                Exit();

                return;
            }

            Console.WriteLine($"\nBacking up {EXE_FILE}...");

            File.Copy(EXE_FILE, EXE_FILE_BACKUP, true);

            Console.WriteLine($"\nUnpacking {EXE_FILE}...");

            var result = UnpackExe();
            if (!result)
            {
                Exit();

                return;
            }

            Console.WriteLine($"\nApplying resolution of {width}x{height}...");

            var buffer = File.ReadAllBytes(EXE_FILE_UNPACKED);

            result = PatchExe(ref buffer, width, height, scale);
            if (!result)
            {
                Exit();

                return;
            }

            Console.WriteLine("\nCleaning up...");

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
                Console.WriteLine($"-> Cannot process {EXE_FILE}!");

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
                Console.WriteLine($"-> Could not process {EXE_FILE}!");

                Exit();

                return false;
            }

            return true;
        }

        private static bool PatchExe(ref byte[] buffer, int width, int height, float scale)
        {
            return 
                PatchAspectRatio(ref buffer, width, height) && 
                PatchResolution(ref buffer, width, height, scale);
        }

        //Source: http://www.wsgf.org/forums/viewtopic.php?f=64&t=32376&sid=4b092cca9c19f600524045733f6db9ab&start=110
        private static bool PatchAspectRatio(ref byte[] buffer, int width, int height)
        {
            float ratio = width / (float)height;
            float ratioWidth = 1920;
            float ratioHeight = 1080;

            if (ratio < 1.77777)
            {
                ratioHeight = ratioWidth / ratio;
            }
            else
            {
                ratioWidth = ratioHeight * ratio;
            }

            //Aspect Ratio Fix #1
            var positions = FindSequence(ref buffer, StringToPattern(PATTERN_ASPECTRATIO1), 0);

            if (!AssertPatch(nameof(PATTERN_ASPECTRATIO1), 1, positions.Count))
            {
                return false;
            }

            var ratio1Patch = ConvertToBytes(ratio);
            Patch(ref buffer, positions.First() + PATTERN_ASPECTRATIO1_OFFSET, ratio1Patch);

            //Aspect Ratio Fix #2
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_ASPECTRATIO2), 0);

            if (!AssertPatch(nameof(PATTERN_ASPECTRATIO2), 1, positions.Count))
            {
                return false;
            }

            var ratio2Patch = ConvertToBytes(ratioHeight).Concat(ConvertToBytes(ratioWidth)).ToArray();
            Patch(ref buffer, positions, ratio2Patch);

            //Magic Fix #1
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC1), 0);

            if (!AssertPatch(nameof(PATTERN_MAGIC1), 1, positions.Count))
            {
                return false;
            }

            Patch(ref buffer, positions, StringToPattern(PATTERN_MAGIC1_PATCH));

            //Magic Fix #2 - A
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC2_A), 0);

            if (!AssertPatch(nameof(PATTERN_MAGIC2_A), 1, positions.Count))
            {
                return false;
            }

            Patch(ref buffer, positions, StringToPattern(PATTERN_MAGIC2_A_PATCH));

            //Magic Fix #2 - B
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC2_B), positions.First());

            if (!AssertPatch(nameof(PATTERN_MAGIC2_B), 1, positions.Count))
            {
                return false;
            }

            Patch(ref buffer, positions.First(), StringToPattern(PATTERN_MAGIC2_B_PATCH));

            return true;
        }

        private static bool PatchResolution(ref byte[] buffer, int width, int height, float scale)
        {
            var positions = FindSequence(ref buffer, StringToPattern(PATTERN_RESOLUTION), 0);

            if (!AssertPatch(nameof(PATTERN_RESOLUTION), 2, positions.Count))
            {
                return false;
            }

            //Window resolution
            var resolution = ConvertToBytes(width).Concat(ConvertToBytes(height)).ToArray();
            Patch(ref buffer, positions[0], resolution);

            //Internal resolution
            int internalWidth = (int) Math.Round(width * scale);
            int internalHeight = (int) Math.Round(height * scale);

            resolution = ConvertToBytes(internalWidth).Concat(ConvertToBytes(internalHeight)).ToArray();
            Patch(ref buffer, positions[1], resolution);

            return true;
        }

        private static void Exit()
        {
            Console.ReadKey();
        }

        private static int ReadInt(string name, int defaultValue)
        {
            int input;

            do
            {
                Console.Write($"-> {name} [default = {defaultValue}]: ");
                
                string inputString = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(inputString))
                {
                    return defaultValue;
                }


                int.TryParse(inputString, out input);

                if (input <= 0)
                {
                    Console.WriteLine("--> Invalid value, try again!");
                }
            } while (input <= 0);

            return input;
        }

        private static float ReadFloat(string name, float defaultValue)
        {
            float input;

            do
            {
                Console.Write($"-> {name} [default = {defaultValue:F1}]: ");

                string inputString = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(inputString))
                {
                    return defaultValue;
                }

                float.TryParse(inputString, out input);

                if (input <= 0)
                {
                    Console.WriteLine("--> Invalid value, try again!");
                }
            } while (input <= 0);

            return input;
        }

        private static byte[] ConvertToBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ConvertToBytes(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] StringToPattern(string pattern)
        {
            return pattern
                .Split(' ')
                .Select(x => Convert.ToByte(x, 16))
                .ToArray();
        }

        //Source: https://stackoverflow.com/questions/283456/byte-array-pattern-search
        private static List<int> FindSequence(ref byte[] buffer, byte[] pattern, int startIndex)
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

                    i = Array.IndexOf(buffer, pattern[0], i + pattern.Length);
                }
                else
                {
                    i = Array.IndexOf(buffer, pattern[0], i + 1);
                }
            }

            return positions;
        }

        private static bool AssertPatch(string name, int expected, int value)
        {
            if (value != expected)
            {
                Console.WriteLine($"-> Expected to find {expected} {name} offset(s) in {EXE_FILE_UNPACKED}, but found {value}!");

                return false;
            }

            return true;
        }

        private static void Patch(ref byte[] buffer, List<int> positions, byte[] patchBytes)
        {
            foreach (int position in positions)
            {
                Patch(ref buffer, position, patchBytes);
            }
        }
        
        private static void Patch(ref byte[] buffer, int position, byte[] patchBytes)
        {
            Console.WriteLine($"-> Patching offset {position}");

            for (int i = 0; i < patchBytes.Length; i++)
            {
                buffer[position + i] = patchBytes[i];
            }
        }
    }
}
