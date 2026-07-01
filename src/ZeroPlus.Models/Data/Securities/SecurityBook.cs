using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Utils;

namespace ZeroPlus.Models.Data.Securities
{
    public class SecurityBook : ISecurityBook
    {
        private readonly object _lock;
        private readonly ConcurrentDictionary<string, Security> _symbolToSecurityMap;

        public SecurityBook()
        {
            _lock = new object();
            _symbolToSecurityMap = new ConcurrentDictionary<string, Security>();
        }

        public Security? GetSecurity(string? symbol)
        {
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return default;
                }
                symbol = symbol.Trim().Replace("+", "").Replace("-", "").ToUpper();
                lock (_lock)
                {
                    if (!_symbolToSecurityMap.TryGetValue(symbol, out Security? security))
                    {
                        security = SymbolParser.GetSecurityFromSymbol(symbol, out string? underlying);
                        if (security == null)
                        {
                            return default;
                        }
                        if (security.SecurityType == Enums.SecurityType.Option && security is Option option)
                        {
                            option.Underlying = GetSecurity(underlying);
                        }
                        _symbolToSecurityMap[symbol] = security;
                    }
                    return security;
                }
            }
            catch (System.Exception)
            {
                return default;
            }
        }
    }
}
