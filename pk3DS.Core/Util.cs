﻿using System;
using System.IO;
using System.Linq;

namespace pk3DS.Core
{
    public static class Util
    {
        // Strings and Paths
        public static FileInfo GetNewestFile(DirectoryInfo directory)
        {
            return directory.GetFiles()
                .Union(directory.GetDirectories().Select(GetNewestFile))
                .OrderByDescending(f => f?.LastWriteTime ?? DateTime.MinValue)
                .FirstOrDefault();
        }

        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string CleanFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
        }

        public static string TrimFromZero(string input)
        {
            int index = input.IndexOf('\0');
            if (index < 0)
                return input;

            return input.Substring(0, index);
        }

        // Randomization
        public static Random Rand { get; private set; } = new();
        public static void ReseedRand(int seed) => Rand = new Random(seed);
        public static uint Random32() => (uint)Rand.Next(1 << 30) << 2 | (uint)Rand.Next(1 << 2);

        // Data Retrieval
        public static int ToInt32(string value)
        {
            string val = value?.Replace(" ", "").Replace("_", "").Trim();
            return string.IsNullOrWhiteSpace(val) ? 0 : int.Parse(val);
        }

        public static uint ToUInt32(string value)
        {
            string val = value?.Replace(" ", "").Replace("_", "").Trim();
            return string.IsNullOrWhiteSpace(val) ? 0 : uint.Parse(val);
        }

        // Data Manipulation
        public static void Shuffle<T>(T[] array)
        {
            int n = array.Length;
            for (int i = 0; i < n; i++)
            {
                int r = i + (int)(Rand.NextDouble() * (n - i));
                T t = array[r];
                array[r] = array[i];
                array[i] = t;
            }
        }

        // GARCTool Utility
        public static string GuessExtension(BinaryReader br, string defaultExt, bool bypass)
        {
            try
            {
                string ext = "";
                long position = br.BaseStream.Position;
                byte[] magic = System.Text.Encoding.ASCII.GetBytes(br.ReadChars(4));

                // check for extensions
                {
                    ushort count = BitConverter.ToUInt16(magic, 2);

                    // check for 2char container extensions
                    try
                    {
                        br.BaseStream.Position = position + 4 + (4 * count);
                        if (br.ReadUInt32() == br.BaseStream.Length)
                        {
                            ext += (char)magic[0] + (char)magic[1];
                            goto end;
                        }
                    }
                    catch { }
                    if (bypass) return defaultExt;

                    // check for darc
                    try
                    {
                        count = BitConverter.ToUInt16(magic, 0);
                        br.BaseStream.Position = position + 4 + (0x40 * count);
                        uint tableval = br.ReadUInt32();
                        br.BaseStream.Position += 0x20 * tableval;
                        while (br.PeekChar() == 0) // seek forward
                            br.ReadByte();
                        if (br.ReadUInt32() == 0x63726164)
                            return "darc";
                    }
                    catch { }

                    // check for bclim
                    try
                    {
                        br.BaseStream.Position = br.BaseStream.Length - 0x28;
                        if (br.ReadUInt32() == 0x4D494C43)
                        {
                            br.BaseStream.Position = br.BaseStream.Length - 0x4;
                            if (br.ReadUInt32() == br.BaseStream.Length - 0x28)
                                return "bclim";
                        }
                    }
                    catch { }

                    // generic check
                    {
                        if (magic[0] < 0x41)
                            return defaultExt;
                        for (int i = 0; i < magic.Length && i < 4; i++)
                        {
                            if ((magic[i] >= 'a' && magic[i] <= 'z') || (magic[i] >= 'A' && magic[i] <= 'Z')
                                || char.IsDigit((char)magic[i]))
                            {
                                ext += (char)magic[i];
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                end:
                {
                    // Return BaseStream position to the start.
                    br.BaseStream.Position = position;
                    return ext.Length <= 1 ? defaultExt : ext;
                }
            }
            catch { return defaultExt; }
        }

        public static string GuessExtension(string path, bool bypass)
        {
            using BinaryReader br = new BinaryReader(File.OpenRead(path));
            return GuessExtension(br, "bin", bypass);
        }

        // Find Code off of Reference
        public static int IndexOfBytes(byte[] array, byte[] pattern, int startIndex, int count)
        {
            int len = pattern.Length;
            int endIndex = count > 0 ? startIndex + count : array.Length - pattern.Length;
            int i = startIndex;
            int j = 0;
            while (true)
            {
                if (pattern[j] != array[i + j])
                {
                    if (++i == endIndex)
                        return -1;
                    j = 0;
                }
                else if (++j == len)
                {
                    return i;
                }
            }
        }

        // Misc
        public static string GetHexString(byte[] data)
        {
            return BitConverter.ToString(data).Replace('-', ' ');
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        
        // General Utility
        public static string[] GetHexLines(byte[] data, int count = 4)
        {
            data ??= Array.Empty<byte>();
            // Generates an x-byte wide space separated string array; leftovers included at the end.
            string[] s = new string[(data.Length/count) + (data.Length % count > 0 ? 1 : 0)];
            for (int i = 0; i < s.Length;i++)
                s[i] = BitConverter.ToString(data.Skip(i*count).Take(count).ToArray()).Replace('-', ' ');
            return s;
        }

        public static string[] GetHexLines(uint[] data)
        {
            data ??= Array.Empty<uint>();
            // Generates an 4-byte wide space separated string array.
            string[] s = new string[data.Length];
            for (int i = 0; i < s.Length; i++)
                s[i] = BitConverter.ToString(BitConverter.GetBytes(data[i])).Replace('-', ' ');
            return s;
        }
    }
}
