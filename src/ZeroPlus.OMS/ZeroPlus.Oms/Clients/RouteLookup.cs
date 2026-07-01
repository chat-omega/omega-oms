using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.OrderRouting;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Config;
using OrderRoutingOrderType = ZeroPlus.Models.Data.Models.OrderRouting.OrderType;
using RoutingVenue = ZeroPlus.Models.Data.Enums.Venue;

namespace ZeroPlus.Oms.Clients
{
    public class RouteLookup
    {
        private readonly OmsConfig _config;

        public RouteLookup(AutoTraderClient autoTraderProvider, OmsConfig config)
        {
            _config = config;
            AutoTrader = autoTraderProvider;
        }

        private AutoTraderClient AutoTrader { get; }

        #region Catalog

        public ICollection<string> GetRoutes(
            OrderRoutingOrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            return AutoTrader?.GetRoutes(orderTypes, account, venue, activeOnly) ?? Array.Empty<string>();
        }

        public ICollection<string> GetRoutesForBroker(
            string broker,
            OrderRoutingOrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            return AutoTrader?.GetRoutesForBroker(broker, orderTypes, account, venue, activeOnly) ?? Array.Empty<string>();
        }

        public AutoTraderClient.ClassifiedRoutes GetClassifiedRoutes(
            OrderRoutingOrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            return AutoTrader?.GetClassifiedRoutes(orderTypes, account, venue, activeOnly)
                   ?? AutoTraderClient.ClassifiedRoutes.Empty;
        }

        public AutoTraderClient.ClassifiedRoutes GetClassifiedRoutesForBroker(
            string broker,
            OrderRoutingOrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            return AutoTrader?.GetClassifiedRoutesForBroker(broker, orderTypes, account, venue, activeOnly)
                   ?? AutoTraderClient.ClassifiedRoutes.Empty;
        }

        public ICollection<string> GetBrokers()
        {
            return AutoTrader?.GetBrokers() ?? Array.Empty<string>();
        }

        #endregion

        #region Classification

        public bool IsSmartRoute(string route)
        {
            return AutoTrader?.IsZpSmartRoute(route) ?? false;
        }

        public RouteType GetRouteKind(string route)
        {
            return AutoTrader?.GetRouteKind(route) ?? RouteType.DMA;
        }

        public bool IsMatrixSmartRoute(string route, out SmartStrategyData strategyData)
        {
            var at = AutoTrader;
            if (at != null)
            {
                return at.IsMatrixSmartRoute(route, out strategyData);
            }
            strategyData = null;
            return false;
        }

        public bool IsAlgoRoute(string route)
        {
            return _config != null && _config.IsAlgoRoute(route);
        }

        #endregion

        #region UI helpers (shared with OrderTicket.Trunk wrappers)

        /// <summary>
        /// Apply broker prefix to a user-selected route. Applied universally
        /// (AT, AT-Direct, AND OPS paths) so the OG-style resolver in
        /// <see cref="TryGetCorrectRouteName"/> can disambiguate which broker's
        /// route to use when multiple brokers carry the same logical route.
        /// </summary>
        public string ApplyBrokerPrefix(string route, string broker)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return route;
            }

            if (route.Contains('-'))
            {
                return route;
            }

            if (_config != null && _config.IsAlgoRoute(route, out _))
            {
                return route;
            }

            if (IsSmartRoute(route))
            {
                return route;
            }

            if (string.IsNullOrWhiteSpace(broker))
            {
                return route;
            }

            return $"{broker}-{route}";
        }

        /// <summary>
        /// Detect a saved "Broker-Route" string and split it into broker / route
        /// parts when the broker is one of the known AvailableBrokers and the
        /// route exists in the AT catalog for that broker.
        /// </summary>
        public bool TryMigrateLegacyRoute(
            string saved,
            out string broker,
            out string routeName,
            OrderRoutingOrderType? orderType = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            broker = null;
            routeName = null;

            if (string.IsNullOrWhiteSpace(saved) || !saved.Contains('-'))
            {
                return false;
            }

            if (_config != null && _config.IsAlgoRoute(saved, out _))
            {
                return false;
            }

            if (IsSmartRoute(saved))
            {
                return false;
            }

            var dashIdx = saved.IndexOf('-');
            if (dashIdx <= 0 || dashIdx >= saved.Length - 1)
            {
                return false;
            }

            var brokerPart = saved.Substring(0, dashIdx);
            var routePart = saved.Substring(dashIdx + 1);

            var availableBrokers = _config?.AvailableBrokers;
            if (availableBrokers == null || !availableBrokers.Any(b => string.Equals(b, brokerPart, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            OrderRoutingOrderType[] orderTypes = orderType.HasValue ? [orderType.Value] : null;
            var brokerRoutes = GetRoutesForBroker(brokerPart, orderTypes, account, venue, activeOnly);
            if (brokerRoutes == null || !brokerRoutes.Any(r => string.Equals(r, routePart, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            broker = brokerPart;
            routeName = routePart;
            return true;
        }

        #endregion

        #region OG-style send-time resolver (OPS path only)

        /// <summary>
        /// Mirrors <c>OrderGatewayManager.TryGetCorrectRouteName</c>: take a
        /// user-selected route (typically broker-prefixed by the UI's
        /// <see cref="ApplyBrokerPrefix"/>) plus the target venue / order type
        /// and resolve to the broker session's wire-form name
        /// (<see cref="OrderRoutingInfoModel.ExpectedName"/> for Silexx,
        /// <see cref="OrderRoutingInfoModel.FixExpectedName"/> for TB / ZpFix).
        /// Used only inside <see cref="OrderClient.SendOpsOrder"/> because AT and
        /// AT-Direct paths terminate at the OG backend which performs the same
        /// resolution server-side.
        /// </summary>
        public bool TryGetCorrectRouteName(
            RoutingVenue venue,
            string selectedRoute,
            OrderRoutingOrderType orderType,
            out string wireRoute,
            out int brokerId)
        {
            wireRoute = selectedRoute;
            brokerId = 0;

            if (string.IsNullOrWhiteSpace(selectedRoute))
            {
                return false;
            }

            var at = AutoTrader;
            if (at == null)
            {
                return false;
            }

            var venueId = GetVenueId(at, venue);
            var orderTypeId = (int)orderType;

            // Internal/synthetic broker routes (e.g. "AUTO-CBOE") rely on OG's
            // server-side auto-priority table, which the client doesn't have.
            // Reject them at the OPS path so users get a clear error instead of
            // an unresolvable wire route flowing to the OPS server.
            if (TryStripInternalBrokerPrefix(at, selectedRoute, out var prefix, out _))
            {
                throw new SlimException($"{prefix} routes are not supported on the OPS path. Pick a specific broker.");
            }

            // SOR routes (e.g. "EXCH_ROLL") have a server-managed override row.
            // Mirror OrderGatewayManager._sorRouteOverrides: take the first
            // SOR-typed entry from a real (non-internal) broker for the bare
            // route at this venue/orderType and use its ExpectedName /
            // FixExpectedName. SOR override wins over standard route lookup.
            var bareRoute = StripAnyDashPrefix(selectedRoute);
            if (at.IsZpSmartRoute(bareRoute))
            {
                var sorMatch = FindSorMatch(at, bareRoute, venueId, orderTypeId);
                if (sorMatch != null)
                {
                    return ApplyMatch(venue, sorMatch, ref wireRoute, ref brokerId);
                }
                // SOR route with no row for this venue/orderType - pass the
                // bare route through as-is rather than letting it fall into
                // the DMA path (which would pick an unrelated entry).
                wireRoute = bareRoute;
                return true;
            }

            var match = FindRouteMatch(at, selectedRoute, venueId, orderTypeId);
            if (match != null)
            {
                return ApplyMatch(venue, match, ref wireRoute, ref brokerId);
            }

            // Silexx pass-through for raw, un-prefixed routes - matches OG's
            // legacy behavior (selectedRoute.IndexOf('-') < 0 on Silexx) so
            // OPS-only routes that aren't in the AT catalog still flow.
            if (venue == RoutingVenue.Silexx && selectedRoute.IndexOf('-') < 0)
            {
                return true;
            }
            return false;
        }

        private static bool ApplyMatch(
            RoutingVenue venue,
            OrderRoutingInfoModel match,
            ref string wireRoute,
            ref int brokerId)
        {
            brokerId = match.BrokerId;
            var expected = venue switch
            {
                RoutingVenue.TB or RoutingVenue.ZpFix => match.FixExpectedName,
                _ => match.ExpectedName,
            };
            if (!string.IsNullOrWhiteSpace(expected))
            {
                wireRoute = expected;
            }
            return true;
        }

        private static bool TryStripInternalBrokerPrefix(AutoTraderClient at, string selectedRoute, out string prefix, out string bareRoute)
        {
            prefix = null;
            bareRoute = null;
            if (string.IsNullOrEmpty(selectedRoute)) return false;
            var dash = selectedRoute.IndexOf('-');
            if (dash <= 0 || dash >= selectedRoute.Length - 1) return false;
            var candidate = selectedRoute.Substring(0, dash);
            if (!at.IsInternalBroker(candidate)) return false;
            prefix = candidate;
            bareRoute = selectedRoute.Substring(dash + 1);
            return true;
        }

        private static OrderRoutingInfoModel FindRouteMatch(
            AutoTraderClient at,
            string selectedRoute,
            int venueId,
            int orderTypeId)
        {
            OrderRoutingInfoModel anyTypeMatch = null;
            foreach (var info in at.EnumerateRouteInfos())
            {
                if (info.VenueId != venueId) continue;
                // Synthetic/internal-broker entries (e.g. AUTO) are never the
                // wire route - they get resolved by OG. Skip them here so that
                // an unprefixed selection like "CBOE" never hits "AUTO-CBOE".
                if (at.IsInternalBroker(info.Broker)) continue;
                if (IsSor(info)) continue;
                if (!RouteNameMatches(info, selectedRoute)) continue;

                if (info.OrderTypeId == orderTypeId)
                {
                    return info;
                }
                if (info.OrderTypeId == 0 && anyTypeMatch == null)
                {
                    anyTypeMatch = info;
                }
            }
            return anyTypeMatch;
        }

        private static OrderRoutingInfoModel FindSorMatch(
            AutoTraderClient at,
            string bareRoute,
            int venueId,
            int orderTypeId)
        {
            OrderRoutingInfoModel anyTypeMatch = null;
            foreach (var info in at.EnumerateRouteInfos())
            {
                if (!IsSor(info)) continue;
                if (info.VenueId != venueId) continue;
                if (at.IsInternalBroker(info.Broker)) continue;
                if (!string.Equals(info.Route, bareRoute, StringComparison.OrdinalIgnoreCase)) continue;

                if (info.OrderTypeId == orderTypeId)
                {
                    return info;
                }
                if (info.OrderTypeId == 0 && anyTypeMatch == null)
                {
                    anyTypeMatch = info;
                }
            }
            return anyTypeMatch;
        }

        private static bool IsSor(OrderRoutingInfoModel info)
        {
            return string.Equals(info?.RouteType, nameof(RouteType.SOR), StringComparison.OrdinalIgnoreCase);
        }

        private static string StripAnyDashPrefix(string route)
        {
            if (string.IsNullOrEmpty(route)) return route;
            var dash = route.IndexOf('-');
            if (dash <= 0 || dash >= route.Length - 1) return route;
            return route.Substring(dash + 1);
        }

        private static bool RouteNameMatches(OrderRoutingInfoModel info, string selectedRoute)
        {
            if (string.Equals(info.ExpectedName, selectedRoute, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(info.FixExpectedName, selectedRoute, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(info.Route, selectedRoute, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(info.Broker) && !string.IsNullOrEmpty(info.Route))
            {
                if (string.Equals($"{info.Broker}-{info.Route}", selectedRoute, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // Prefer the venue ids advertised by the AT server payload (so we stay
        // in sync with whatever the routing DB uses); fall back to the OG
        // hardcoded ids when the catalog hasn't loaded yet.
        private static int GetVenueId(AutoTraderClient at, RoutingVenue venue)
        {
            var name = venue.ToString();
            var venueIds = at?.VenueNameToId;
            if (venueIds != null && venueIds.TryGetValue(name, out var id))
            {
                return id;
            }
            return venue switch
            {
                RoutingVenue.Silexx => 101,
                RoutingVenue.TB => 102,
                RoutingVenue.ZpFix => 103,
                _ => 0,
            };
        }

        #endregion
    }
}
