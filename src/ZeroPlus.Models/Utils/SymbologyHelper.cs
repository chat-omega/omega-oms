using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Utils
{
    public class SymbologyHelper
    {
        private static ILogger<SymbologyHelper>? _logger;

        private static readonly HashSet<string> _amExpirationOptions = new HashSet<string>()
        {
            "SPX", "NDX", "RUT"
        };
        private static readonly HashSet<string> _extra15MinExpirationOptions = new HashSet<string>()
        {
            "SPXW", "NDXP", "RUTW"
        };
        private static readonly CultureInfo _enUSCultureInfo = new CultureInfo("en-US");
        private static readonly Regex _optionSymbolRegex = new Regex(@"([.]{1})([\w\d]*)([\d]{6})([PC]{1})([\d]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _optionSymbolEmsRegex = new Regex(@"([+-]*\d*)(\w{1,6})(\d*\\+)(\d+)([A-Z])(\d{1})(\\+)(\d+.*\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        public SymbologyHelper() : this(null)
        {
        }

        public SymbologyHelper(ILogger<SymbologyHelper>? logger)
        {
            _logger = logger;
        }

        public static bool TryConvertTosSymbolToEmsSymbol(string? inputSymbol, out string? outSymbol)
        {
            if (string.IsNullOrWhiteSpace(inputSymbol))
            {
                outSymbol = inputSymbol;
                return false;
            }
            else if (inputSymbol[0] == '.')
            {
                outSymbol = "";
                if (TryParseTosSymbol(inputSymbol, out string root, out DateTime expiration, out double strike, out PutCall optionType))
                {
                    if (TryEncodeExpiration(expiration, optionType, out char code))
                    {
                        outSymbol += "+";
                        outSymbol += root;
                        outSymbol += "\\";
                        outSymbol += expiration.Day;
                        outSymbol += code;
                        outSymbol += expiration.Year % 10;
                        outSymbol += "\\";
                        outSymbol += strike;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                outSymbol = inputSymbol;
                return true;
            }
        }

        public static bool TryParseTosSymbol(string inputSymbol, out string rootSymbol, out DateTime expiration, out double strike, out PutCall optionType)
        {
            rootSymbol = string.Empty;
            expiration = default;
            strike = default;
            optionType = default;
            try
            {
                if (string.IsNullOrWhiteSpace(inputSymbol) || inputSymbol.Length <= 1)
                {
                    _logger?.LogError("Symbol can not be empty");
                    return false;
                }

                Match result = _optionSymbolRegex.Match(inputSymbol);
                if (result.Success)
                {
                    rootSymbol = result.Groups[2].Value;
                    string exp = result.Groups[3].Value;
                    string type = result.Groups[4].Value;
                    string strikeString = result.Groups[5].Value;
                    if (DateTime.TryParseExact(exp, "yyMMdd", _enUSCultureInfo, DateTimeStyles.None, out expiration))
                    {
                        if (_amExpirationOptions.Contains(rootSymbol))
                        {
                            expiration += new TimeSpan(8, 30, 0);
                        }
                        else if (_extra15MinExpirationOptions.Contains(rootSymbol))
                        {
                            expiration += new TimeSpan(15, 15, 00);
                        }
                        else
                        {
                            expiration += new TimeSpan(15, 0, 0);
                        }

                        if (double.TryParse(strikeString, out strike))
                        {
                            strike = strikeString.Length == 8 ? strike / 1000.0 : strike;

                            if (type.Length > 0)
                            {
                                optionType = type[0] == 'C' ? PutCall.Call : PutCall.Put;
                                return true;
                            }
                        }
                    }
                }
                _logger?.LogError($"Parsing Expiration failed for {inputSymbol}");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryParseTosSymbol) + " Symbol parsed failed. Input: " + inputSymbol);
                return false;
            }
        }

        public bool TryParseEmsSymbol(string inputSymbol, out string symbol, out string root, out DateTime expiration, out double strike, out PutCall optionType)
        {
            symbol = string.Empty;
            root = string.Empty;
            expiration = default;
            strike = default;
            optionType = PutCall.Unknown;
            try
            {

                Match match = _optionSymbolEmsRegex.Match(inputSymbol);

                if (match.Success)
                {
                    root = match.Groups[2].Value;
                    string dayCode = match.Groups[4].Value;
                    if (int.TryParse(dayCode, out int day))
                    {
                        string expirationCode = match.Groups[5].Value;
                        if (TryParseMonth(expirationCode, out int month))
                        {
                            string yearCode = match.Groups[6].Value;
                            if (TryGetYear(yearCode, out int year))
                            {
                                if (TryGetOptionType(expirationCode, out optionType))
                                {
                                    string strikeCode = match.Groups[8].Value;
                                    if (double.TryParse(strikeCode, out strike))
                                    {
                                        expiration = new DateTime(year, month, day);
                                        symbol = "." + root + expiration.ToString("yyMMdd") + (optionType == PutCall.Put ? "P" : "C") + Math.Round(strike, 2);
                                        _logger?.LogInformation("Symbol parsed successfully. Input: " + inputSymbol + ", Output: " + symbol);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                _logger?.LogError("Symbol parsed failed. Input: " + inputSymbol);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryParseEmsSymbol) + " Symbol parsed failed. Input: " + inputSymbol);
                return false;
            }
        }

        private bool TryGetOptionType(string expirationCode, out PutCall optionType)
        {
            char typeCode = expirationCode[0];
            if (typeCode >= 'A' && typeCode <= 'L')
            {
                optionType = PutCall.Call;
                return true;
            }
            else if (typeCode >= 'M' && typeCode <= 'X')
            {
                optionType = PutCall.Put;
                return true;
            }
            else
            {
                optionType = PutCall.Unknown;
                return false;
            }
        }

        private bool TryGetYear(string value, out int year)
        {
            try
            {
                if (int.TryParse(value, out int yearCode))
                {
                    year = DateTime.Now.Year;
                    year = year - (year % 10) + yearCode;
                    return true;
                }
                year = 0;
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryGetYear));
                year = 0;
                return false;
            }
        }

        private bool TryParseMonth(string expirationCode, out int month)
        {
            try
            {
                month = 1 + ((expirationCode[0] - 'A') % 12);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryParseMonth));
                month = 0;
                return false;
            }
        }

        private static bool TryEncodeExpiration(DateTime expiration, PutCall optionType, out char code)
        {
            switch (expiration.Month)
            {
                case 1:
                    code = optionType == PutCall.Call ? 'A' : 'M';
                    return true;
                case 2:
                    code = optionType == PutCall.Call ? 'B' : 'N';
                    return true;
                case 3:
                    code = optionType == PutCall.Call ? 'C' : 'O';
                    return true;
                case 4:
                    code = optionType == PutCall.Call ? 'D' : 'P';
                    return true;
                case 5:
                    code = optionType == PutCall.Call ? 'E' : 'Q';
                    return true;
                case 6:
                    code = optionType == PutCall.Call ? 'F' : 'R';
                    return true;
                case 7:
                    code = optionType == PutCall.Call ? 'G' : 'S';
                    return true;
                case 8:
                    code = optionType == PutCall.Call ? 'H' : 'T';
                    return true;
                case 9:
                    code = optionType == PutCall.Call ? 'I' : 'U';
                    return true;
                case 10:
                    code = optionType == PutCall.Call ? 'J' : 'V';
                    return true;
                case 11:
                    code = optionType == PutCall.Call ? 'K' : 'W';
                    return true;
                case 12:
                    code = optionType == PutCall.Call ? 'L' : 'X';
                    return true;
            }
            code = 'Z';
            return false;
        }
    }
}