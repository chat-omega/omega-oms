using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZeroPlus.Models.Data.Enums;

public static class StringExtensions
{
    public static string ToSpaced(this string str)
    {
        // Check if the string is null or empty
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        // Convert the string to a char array
        char[] chars = str.ToCharArray();

        // Create a StringBuilder to store the converted string
        StringBuilder sb = new();

        // Loop through the char array
        for (int i = 0; i < chars.Length; i++)
        {
            // If the current character is a capital letter,
            // then add a space to the StringBuilder
            if (char.IsUpper(chars[i]))
            {
                sb.Append(' ');
            }

            // Append the current character to the StringBuilder
            sb.Append(chars[i]);
        }

        // Return the converted string
        return sb.ToString();
    }


    public static string FormatTable(this List<List<string>> list)
    {
        if (list == null || list.Count == 0 || list[0].Count == 0)
        {
            return string.Empty;
        }

        int[] columnWidths = new int[list[0].Count];

        for (int col = 0; col < list[0].Count; col++)
        {
            columnWidths[col] = list.Max(row => row[col]?.Length ?? 0);
        }

        StringBuilder builder = new StringBuilder();

        for (var index = 0; index < list.Count; index++)
        {
            var row = list[index];
            for (int col = 0; col < row.Count; col++)
            {
                string cellValue = row[col] ?? "";
                builder.Append(cellValue.PadRight(columnWidths[col] + 2, ' '));
            }

            builder.AppendLine();

            if (index == 0)
            {
                builder.Append(new string('-', columnWidths.Sum() + columnWidths.Length * 2));
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    public static Side? ToSide(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return input.Contains(Side.Sell.ToString(), StringComparison.OrdinalIgnoreCase) ? Side.Sell : Side.Buy;
    }

}