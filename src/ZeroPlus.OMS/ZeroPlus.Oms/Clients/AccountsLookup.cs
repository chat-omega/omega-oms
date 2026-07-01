using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data;

namespace ZeroPlus.Oms.Clients
{
    public class AccountsLookup
    {
        public enum AccountsType
        {
            Options,
            Equity,
            MLeg,
            All,
        }

        private readonly ConcurrentDictionary<AccountsType, List<ZPAccount>> _accountTypeToAccountsListMap = new();
        private readonly ConcurrentDictionary<string, HashSet<Venue>> _routeNameToConnectionMap = new();

        public async Task<List<ZPAccount>> GetAccountsAsync(AccountsType accountsType)
        {
            return await Task.Run(() => GetAccounts(accountsType));
        }

        public List<ZPAccount> GetAccounts(AccountsType accountType)
        {
            switch (accountType)
            {
                case AccountsType.Options:
                    if (_accountTypeToAccountsListMap.TryGetValue(accountType, out List<ZPAccount> optionAccounts))
                    {
                        return optionAccounts;
                    }
                    break;
                case AccountsType.Equity:
                    if (_accountTypeToAccountsListMap.TryGetValue(accountType, out List<ZPAccount> equityAccounts))
                    {
                        return equityAccounts;
                    }
                    break;
                case AccountsType.MLeg:
                    if (_accountTypeToAccountsListMap.TryGetValue(accountType, out List<ZPAccount> mlegAccounts))
                    {
                        return mlegAccounts;
                    }
                    break;
                case AccountsType.All:
                    if (_accountTypeToAccountsListMap.Count > 0)
                    {
                        List<ZPAccount> allAccounts = _accountTypeToAccountsListMap.Values.SelectMany(x => x).ToList();

                        return allAccounts;
                    }
                    break;
            }
            return null;
        }

        internal void Add(AccountsType accountsType, List<ZPAccount> optionAccounts)
        {
            if (optionAccounts != null && optionAccounts.Count > 0)
            {
                _accountTypeToAccountsListMap[accountsType] = optionAccounts;

                foreach (ZPAccount newAccount in optionAccounts)
                {
                    foreach (RouteDetails route in newAccount.Routes)
                    {
                        Venue venue = Venue.Silexx;
                        if (route.Connection == Models.Data.Enums.Venue.ZpFix.ToString())
                        {
                            venue = Venue.ZP;
                        }
                        else if (Enum.TryParse(route.Connection, out Venue tempVenue))
                        {
                            venue = tempVenue;
                        }

                        if (!_routeNameToConnectionMap.TryGetValue(route.RouteName, out var venues))
                        {
                            venues = new();
                            _routeNameToConnectionMap.TryAdd(route.RouteName, venues);
                        }
                        venues.Add(venue);
                    }
                }
            }
        }

        public bool TryGetVenues(string route, out HashSet<Venue> venues)
        {
            var gotVenues = _routeNameToConnectionMap.TryGetValue(route, out venues);
            return gotVenues && venues.Count > 0;
        }
    }
}