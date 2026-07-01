using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Extensions
{
    public static class StringExtensions
    {
        public static string FormatBytes(this long bytes)
        {
            string[] Suffix = ["B", "KB", "MB", "GB", "TB"];

            double dblSByte = bytes;
            int i;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return $"{dblSByte:0.00} {Suffix[i]}";
        }

        public static string Md5Hash(this string input)
        {
            try
            {
                StringBuilder stringBuilder = new(32);
                using (MD5 md5 = MD5.Create())
                {
                    byte[] array = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                    for (int i = 0; i < 16; i++)
                    {
                        stringBuilder.Append(array[i].ToString("x2"));
                    }
                }

                return stringBuilder.ToString();
            }
            catch (Exception)
            {
                return input;
            }
        }

        public static string CompressString(this string s)
        {
            try
            {
                byte[] compressedBytes;

                using (MemoryStream uncompressedStream = new MemoryStream(Encoding.UTF8.GetBytes(s)))
                {
                    using MemoryStream compressedStream = new MemoryStream();
                    using (DeflateStream compressorStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, true))
                    {
                        uncompressedStream.CopyTo(compressorStream);
                    }

                    compressedBytes = compressedStream.ToArray();
                }

                return "<X>" + Convert.ToBase64String(compressedBytes);
            }
            catch (Exception)
            {
                return s;
            }
        }

        public static string DecompressString(this string s)
        {
            try
            {
                if (!IsCompressed(s))
                {
                    return s;
                }
                s = s[3..];
                byte[] decompressedBytes;

                MemoryStream compressedStream = new MemoryStream(Convert.FromBase64String(s));

                using (DeflateStream decompressorStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    using MemoryStream decompressedStream = new MemoryStream();
                    decompressorStream.CopyTo(decompressedStream);

                    decompressedBytes = decompressedStream.ToArray();
                }

                return Encoding.UTF8.GetString(decompressedBytes);
            }
            catch (Exception)
            {
                return s;
            }
        }

        public static bool IsCompressed(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }
            else
            {
                return s.StartsWith("<X>");
            }
        }

        public static string? TrimWhiteSpace(this string symbol)
        {
            if (!string.IsNullOrWhiteSpace(symbol) && symbol.Contains(','))
            {
                symbol = string.Join(",", symbol.Split(',').Select(x => x.Trim()));
            }

            return symbol;
        }

        public static string FromCamelCase(this string camelCase)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(camelCase))
                {
                    return string.Empty;
                }

                return Regex.Replace(camelCase, "(\\B[A-Z])", " $1");
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string ToSpacedString<T>(this T e) where T : Enum
        {
            try
            {
                return FromCamelCase(e.ToString());
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static OrderSubType? TryGetSubType(this string subType)
        {
            if (string.IsNullOrWhiteSpace(subType))
            {
                return null;
            }
            if (Enum.TryParse<OrderSubType>(subType.Replace(" ", ""), true, out var evalType))
            {
                return evalType;
            }
            if (subType is "C.O.T" or "API")
            {
                return OrderSubType.Ticket;
            }
            if (subType.StartsWith("Looper"))
            {
                return OrderSubType.Looper;
            }
            if (subType.StartsWith("Free Look All"))
            {
                return OrderSubType.FreeLookAll;
            }
            if (subType.StartsWith("Free Look"))
            {
                return OrderSubType.FreeLook;
            }
            if (subType == "Loop Resubmit")
            {
                return OrderSubType.LooperResubmit;
            }
            if (subType.Contains("DOMINATOR"))
            {
                return OrderSubType.Dominator;
            }
            if (subType == "Synt")
            {
                return OrderSubType.Synthetic;
            }
            if (subType == "Scr")
            {
                return OrderSubType.Scrape;
            }
            if (subType == "Seek")
            {
                return OrderSubType.Seeker;
            }
            return null;
        }

    }
}
