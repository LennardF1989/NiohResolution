using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using Steamless.API.Model;
using Steamless.API.Services;
using Steamless.Unpacker.Variant31.x64;

namespace NiohResolution
{
    public class Program
    {
        private const string EXE_FILE = "nioh.exe";
        private const string EXE_FILE_BACKUP = "nioh.exe.backup.exe";
        private const string EXE_FILE_UNPACKED = "nioh.exe.unpacked.exe";

        private const string PATTERN_ASPECTRATIO1 = "C7 43 50 39 8E E3 3F";
        private const int PATTERN_ASPECTRATIO1_OFFSET = 3;

        private const string PATTERN_ASPECTRATIO2 = "00 00 87 44 00 00 F0 44";

        private const string PATTERN_MAGIC1 = "00 00 00 41 88 46 34";
        private const string PATTERN_MAGIC1_PATCH = "32 C0 90 88 46 34";

        private const string PATTERN_MAGIC2_A = "45 85 D2 7E 1A 48";
        private const string PATTERN_MAGIC2_A_PATCH = "50 45 00 00 64";

        private const string PATTERN_MAGIC2_B = "C3 79 14";
        private const string PATTERN_MAGIC2_B_PATCH = "C3 EB 14";

        private const string PATTERN_RESOLUTION = "80 07 00 00 38 04 00 00 00";

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
            Console.WriteLine("- Can be less than 1.0 to increase performance.");
            Console.WriteLine();

            float scale = ReadFloat("Scale", 1.0f);

            if (File.Exists(EXE_FILE_BACKUP))
            {
                Console.WriteLine($"\nA backup of {EXE_FILE} has been found, it was created the last time this patcher ran succesfully.\n");

                if (ReadBool("Do you want to restore this backup before patching?", false))
                {
                    File.Copy(EXE_FILE_BACKUP, EXE_FILE, true);
                }
            }

            if (!File.Exists(EXE_FILE))
            {
                Console.WriteLine($"\nCould not find {EXE_FILE}!");

                Exit();

                return;
            }

            Console.WriteLine($"\nUnpacking {EXE_FILE}...");

            var result = UnpackExe();
            if (!result)
            {
                Console.WriteLine($"\nUnpacking of {EXE_FILE} failed, this could mean the file is already unpacked and ready for patching.");

                File.Copy(EXE_FILE, EXE_FILE_UNPACKED, true);
            }

            Console.WriteLine($"\nPatching resolution to {width}x{height} with scale set to {scale:F1}...");

            var buffer = File.ReadAllBytes(EXE_FILE_UNPACKED);

            result = PatchExe(ref buffer, width, height, scale);
            if (!result)
            {
                Console.WriteLine("\nPatching failed, consider restoring a backup and try again.");

                File.Delete(EXE_FILE_UNPACKED);

                Exit();

                return;
            }

            Console.WriteLine("\nAn experimental patch for the aspect ratio of FMV's is available.\n");

            if (ReadBool("Do you want to apply this patch?", false))
            {
                Console.WriteLine("\nPatching FMV's...");

                byte[] bufferCopy = buffer.ToArray();
                result = PatchFMV(ref bufferCopy);
                if (result)
                {
                    buffer = bufferCopy;
                }
                else
                {
                    Console.WriteLine("-> Patching failed, rolling back changes...");
                }
            }
            
            Console.WriteLine($"\nBacking up {EXE_FILE}...");

            File.Copy(EXE_FILE, EXE_FILE_BACKUP, true);

            Console.WriteLine($"\nReplacing {EXE_FILE}...");

            File.WriteAllBytes(EXE_FILE, buffer);
            File.Delete(EXE_FILE_UNPACKED);

            Console.WriteLine("\nDone! Don't forget to apply the following changes in the launcher:");
            Console.WriteLine("- Set the Render Resolution to High");
            Console.WriteLine("- Set the Resolution to 1920x1080");

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
                return false;
            }

            result = plugin.ProcessFile(EXE_FILE, new SteamlessOptions
            {
                VerboseOutput = false,
                KeepBindSection = true
            });

            if (!result)
            {
                Console.WriteLine($"-> Processing {EXE_FILE} failed!");

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

        //Source: http://www.wsgf.org/forums/viewtopic.php?f=64&t=32376&start=110
        private static bool PatchAspectRatio(ref byte[] buffer, int width, int height)
        {
            float ratio = width / (float)height;
            float ratioWidth = 2560;
            float ratioHeight = 1440;

            if (ratio < 1.77777)
            {
                ratioHeight = ratioWidth / ratio;
            }
            else
            {
                ratioWidth = ratioHeight * ratio;
            }

            Console.WriteLine($"Width: {ratioWidth}, Height: {ratioHeight}");

            //Aspect Ratio Fix #1
            var positions = FindSequence(ref buffer, StringToPattern(PATTERN_ASPECTRATIO1), 0);

            if (!AssertEquals(nameof(PATTERN_ASPECTRATIO1), 1, positions.Count))
            {
                positions.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                return false;
            }

            var ratio1Patch = ConvertToBytes(ratio);
            Patch(ref buffer, positions.First() + PATTERN_ASPECTRATIO1_OFFSET, ratio1Patch);

            //Aspect Ratio Fix #2
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_ASPECTRATIO2), 0);

            if (!AssertEquals(nameof(PATTERN_ASPECTRATIO2), 1, positions.Count))
            {
                positions.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                return false;
            }

            var ratio2Patch = ConvertToBytes(ratioHeight).Concat(ConvertToBytes(ratioWidth)).ToArray();
            Patch(ref buffer, positions, ratio2Patch);

            //Magic Fix #1
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC1), 0);

            if (!AssertEquals(nameof(PATTERN_MAGIC1), 1, positions.Count))
            {
                positions.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                return false;
            }

            Patch(ref buffer, positions, StringToPattern(PATTERN_MAGIC1_PATCH));

            //Magic Fix #2 - A
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC2_A), 0);

            if (!AssertEquals(nameof(PATTERN_MAGIC2_A), 1, positions.Count))
            {
                positions.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                return false;
            }

            Patch(ref buffer, positions, StringToPattern(PATTERN_MAGIC2_A_PATCH));

            //Magic Fix #2 - B
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC2_B), positions.First());

            if (!AssertEquals(nameof(PATTERN_MAGIC2_B), 1, positions.Count))
            {
                positions.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                return false;
            }

            Patch(ref buffer, positions.First(), StringToPattern(PATTERN_MAGIC2_B_PATCH));

            return true;
        }

        private static bool PatchResolution(ref byte[] buffer, int width, int height, float scale)
        {
            var positions = FindSequence(ref buffer, StringToPattern(PATTERN_RESOLUTION), 0);

            if (!AssertEquals(nameof(PATTERN_RESOLUTION), 2, positions.Count))
            {
                positions.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                return false;
            }

            //Window resolution
            var resolution = ConvertToBytes(width).Concat(ConvertToBytes(height)).ToArray();
            Patch(ref buffer, positions[0], resolution);

            //Internal resolution
            int internalWidth = (int)Math.Round(width * scale);
            int internalHeight = (int)Math.Round(height * scale);

            resolution = ConvertToBytes(internalWidth).Concat(ConvertToBytes(internalHeight)).ToArray();
            Patch(ref buffer, positions[1], resolution);

            return true;
        }

        //Source: http://www.wsgf.org/forums/viewtopic.php?f=64&t=32376&start=150
        private static bool PatchFMV(ref byte[] buffer)
        {
            //Magic Fix #1
            var offset = 0x3c5b78;
            var find = "96 40 01 66 0F 7F 41";
            var patch = "E9 62 31 40 01 90 90";
            var result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC1", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            //Magic Fix #2
            offset = 0xb5965f;
            find = "61 00 48 8B 74 24 38 48";
            patch = "E9 45 F7 C6 00 90 90 90";
            result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC2", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            //Magic Fix #3
            offset = 0x1159f5b;
            find = "25 48 8B 47 78 48 03";
            patch = "E9 4F ED 66 78 90 90";
            result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC3", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            //Magic Fix #4
            offset = 0x1159f7c;
            find = "8B 01 FF 50 78 48 8B";
            patch = "E9 41 ED 66 00 90 90";
            result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC4", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            //Magic Fix #5
            offset = 0x11698e9;
            find = "00 89 41 24 8B 87";
            patch = "E9 16 F4 65 8B 90";
            result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC5", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            //Magic Fix #6
            offset = 0x1196c12;
            find = "20 E8 C8 CB FC FF 83 BF";
            patch = "E9 5F 21 63 00 90 90 90";
            result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC6", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            //Magic Fix #7
            offset = 0x17c8caf;
            find = "CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC";
            patch = "4C 03 87 88 00 00 00 C6 87 F4 23 08 F5 00 E9 A0 12 99 FF 48 8B 47 78 48 03 C0 80 7B 44 00 0F 84 B0 12 99 FF C6 87 F4 23 08 F5 01 E9 A4 12 99 FF C7 41 50 39 8E E3 3F 49 8B 81 B0 0D 00 00 F3 0F 10 80 78 13 00 00 F3 0F 59 41 50 F3 0F 11 41 50 E9 7B CE BF FE 49 8B 45 40 80 B8 74 2A 36 FF 01 74 0B 8B 87 78 01 00 00 E9 D3 0B 9A FF F3 44 0F 10 BF 78 01 00 00 F3 44 0F 59 B8 78 2A 36 FF F3 44 0F 5E B8 7C 2A 36 FF F3 44 0F 11 79 18 45 0F 57 FF DB 87 38 03 00 00 D8 88 78 2A 36 FF D8 B0 7C 2A 36 FF DB 9F 38 03 00 00 D9 87 80 03 00 00 D8 B0 78 2A 36 FF D8 88 7C 2A 36 FF D9 9F 80 03 00 00 E9 7C 0B 9A FF 4C 8B 44 24 58 4D 8B 80 6B 50 27 01 41 80 78 D4 01 74 0D F3 0F 10 9F B0 08 00 00 E9 84 DE 9C FF F3 0F 10 9F B0 08 00 00 F3 41 0F 5E 58 D8 E9 71 DE 9C FF 4C 8B 5C 24 28 4D 8B 9B 8F 7B E9 01 41 80 7B D4 01 74 0D F3 0F 11 89 CC 01 00 00 E9 9E 08 39 FF F3 41 0F 5E 4B D8 F3 41 0F 59 4B DC F3 0F 11 89 CC 01 00 00 E9 85 08 39 FF";
            result = CompareSequence(ref buffer, StringToPattern(find), offset);

            if (!AssertEquals("FMV_MAGIC7", true, result))
            {
                return false;
            }

            Patch(ref buffer, offset, StringToPattern(patch));

            return true;
        }

        private static void Exit()
        {
            Console.WriteLine("\nPress any key to exit...");

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

        private static bool ReadBool(string name, bool defaultValue)
        {
            while (true)
            {
                Console.Write($"-> {name} [default = {(defaultValue ? "Yes" : "No")}]: ");

                string inputString = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(inputString))
                {
                    return defaultValue;
                }

                if (inputString.StartsWith("Y", true, CultureInfo.CurrentCulture))
                {
                    return true;
                }

                if (inputString.StartsWith("N", true, CultureInfo.CurrentCulture))
                {
                    return false;
                }

                Console.WriteLine("--> Invalid value, try again!");
            }
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
            //Console.WriteLine($"BUFF: {buffer.Length}, PATT: {Encoding.UTF8.GetString(pattern)}, SINDEX: {startIndex}");
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

        private static bool CompareSequence(ref byte[] buffer, byte[] pattern, int startIndex)
        {
            if (startIndex > buffer.Length - pattern.Length)
            {
                return false;
            }

            byte[] segment = new byte[pattern.Length];
            Buffer.BlockCopy(buffer, startIndex, segment, 0, pattern.Length);

            return segment.SequenceEqual(pattern);
        }

        private static bool AssertEquals<T>(string name, T expected, T value)
        {
            if (!value.Equals(expected))
            {
                Console.WriteLine($"-> {name} expected {expected}, but got {value}!");

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
