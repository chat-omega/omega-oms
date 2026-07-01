using DevExpress.Mvvm;
using System;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SpreadHeatmapRowModel : BindableBase
    {
        private readonly NotificationManager _notificationManager;


        public DateTime ExpirationDateTime { get; internal set; }

        [Bindable]
        public partial string Expiration { get; set; }

        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol1 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol2 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol3 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol4 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol5 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol6 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol7 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol8 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol9 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol10 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol11 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol12 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol13 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol14 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol15 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol16 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol17 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol18 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol19 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol20 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol21 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol22 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol23 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol24 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol25 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol26 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol27 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol28 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol29 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol30 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol31 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol32 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol33 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol34 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol35 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol36 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol37 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol38 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol39 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol40 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol41 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol42 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol43 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol44 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol45 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol46 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol47 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol48 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol49 { get; set; }
        [Bindable]
        public partial SpreadHeatmapCell SpreadHeatmapCellCol50 { get; set; }


        public SpreadHeatmapRowModel(Notifications.NotificationManager notificationManager)
        {
            _notificationManager = notificationManager;
            SpreadHeatmapCell spreadCell = new(notificationManager);

            SpreadHeatmapCellCol1 = spreadCell;
            SpreadHeatmapCellCol2 = spreadCell;
            SpreadHeatmapCellCol3 = spreadCell;
            SpreadHeatmapCellCol4 = spreadCell;
            SpreadHeatmapCellCol5 = spreadCell;
            SpreadHeatmapCellCol6 = spreadCell;
            SpreadHeatmapCellCol7 = spreadCell;
            SpreadHeatmapCellCol8 = spreadCell;
            SpreadHeatmapCellCol9 = spreadCell;
            SpreadHeatmapCellCol10 = spreadCell;
            SpreadHeatmapCellCol11 = spreadCell;
            SpreadHeatmapCellCol12 = spreadCell;
            SpreadHeatmapCellCol13 = spreadCell;
            SpreadHeatmapCellCol14 = spreadCell;
            SpreadHeatmapCellCol15 = spreadCell;
            SpreadHeatmapCellCol16 = spreadCell;
            SpreadHeatmapCellCol17 = spreadCell;
            SpreadHeatmapCellCol18 = spreadCell;
            SpreadHeatmapCellCol19 = spreadCell;
            SpreadHeatmapCellCol20 = spreadCell;
            SpreadHeatmapCellCol21 = spreadCell;
            SpreadHeatmapCellCol22 = spreadCell;
            SpreadHeatmapCellCol23 = spreadCell;
            SpreadHeatmapCellCol24 = spreadCell;
            SpreadHeatmapCellCol25 = spreadCell;
            SpreadHeatmapCellCol26 = spreadCell;
            SpreadHeatmapCellCol27 = spreadCell;
            SpreadHeatmapCellCol28 = spreadCell;
            SpreadHeatmapCellCol29 = spreadCell;
            SpreadHeatmapCellCol30 = spreadCell;
            SpreadHeatmapCellCol31 = spreadCell;
            SpreadHeatmapCellCol32 = spreadCell;
            SpreadHeatmapCellCol33 = spreadCell;
            SpreadHeatmapCellCol34 = spreadCell;
            SpreadHeatmapCellCol35 = spreadCell;
            SpreadHeatmapCellCol36 = spreadCell;
            SpreadHeatmapCellCol37 = spreadCell;
            SpreadHeatmapCellCol38 = spreadCell;
            SpreadHeatmapCellCol39 = spreadCell;
            SpreadHeatmapCellCol40 = spreadCell;
            SpreadHeatmapCellCol41 = spreadCell;
            SpreadHeatmapCellCol42 = spreadCell;
            SpreadHeatmapCellCol43 = spreadCell;
            SpreadHeatmapCellCol44 = spreadCell;
            SpreadHeatmapCellCol45 = spreadCell;
            SpreadHeatmapCellCol46 = spreadCell;
            SpreadHeatmapCellCol47 = spreadCell;
            SpreadHeatmapCellCol48 = spreadCell;
            SpreadHeatmapCellCol49 = spreadCell;
            SpreadHeatmapCellCol50 = spreadCell;
        }

        internal void SetCell(int nextColIndex,
                              DateTime optionExpiration,
                              OptionChainModel optionChain,
                              HeatmapSettingsModel heatmapSettingsModel,
                              HeatmapSettingsModel globalHeatmapSettingsModel,
                              SpreadHeatmapAlert groupAlert,
                              Subscription.DataStore deltaStore)
        {
            SpreadHeatmapCell spreadCell = new(heatmapSettingsModel, globalHeatmapSettingsModel, _notificationManager)
            {
                Symbol = optionChain.Symbol,
                Expiration = optionExpiration,
                Title = optionChain.Symbol.PadRight(7) + optionExpiration.ToString("MMM-dd-yy").ToUpper(),
                GroupAlert = groupAlert,
                Initialized = true,
            };

            switch (nextColIndex)
            {
                case 1:
                    SpreadHeatmapCellCol1 = spreadCell;
                    break;
                case 2:
                    SpreadHeatmapCellCol2 = spreadCell;
                    break;
                case 3:
                    SpreadHeatmapCellCol3 = spreadCell;
                    break;
                case 4:
                    SpreadHeatmapCellCol4 = spreadCell;
                    break;
                case 5:
                    SpreadHeatmapCellCol5 = spreadCell;
                    break;
                case 6:
                    SpreadHeatmapCellCol6 = spreadCell;
                    break;
                case 7:
                    SpreadHeatmapCellCol7 = spreadCell;
                    break;
                case 8:
                    SpreadHeatmapCellCol8 = spreadCell;
                    break;
                case 9:
                    SpreadHeatmapCellCol9 = spreadCell;
                    break;
                case 10:
                    SpreadHeatmapCellCol10 = spreadCell;
                    break;
                case 11:
                    SpreadHeatmapCellCol11 = spreadCell;
                    break;
                case 12:
                    SpreadHeatmapCellCol12 = spreadCell;
                    break;
                case 13:
                    SpreadHeatmapCellCol13 = spreadCell;
                    break;
                case 14:
                    SpreadHeatmapCellCol14 = spreadCell;
                    break;
                case 15:
                    SpreadHeatmapCellCol15 = spreadCell;
                    break;
                case 16:
                    SpreadHeatmapCellCol16 = spreadCell;
                    break;
                case 17:
                    SpreadHeatmapCellCol17 = spreadCell;
                    break;
                case 18:
                    SpreadHeatmapCellCol18 = spreadCell;
                    break;
                case 19:
                    SpreadHeatmapCellCol19 = spreadCell;
                    break;
                case 20:
                    SpreadHeatmapCellCol20 = spreadCell;
                    break;
                case 21:
                    SpreadHeatmapCellCol21 = spreadCell;
                    break;
                case 22:
                    SpreadHeatmapCellCol22 = spreadCell;
                    break;
                case 23:
                    SpreadHeatmapCellCol23 = spreadCell;
                    break;
                case 24:
                    SpreadHeatmapCellCol24 = spreadCell;
                    break;
                case 25:
                    SpreadHeatmapCellCol25 = spreadCell;
                    break;
                case 26:
                    SpreadHeatmapCellCol26 = spreadCell;
                    break;
                case 27:
                    SpreadHeatmapCellCol27 = spreadCell;
                    break;
                case 28:
                    SpreadHeatmapCellCol28 = spreadCell;
                    break;
                case 29:
                    SpreadHeatmapCellCol29 = spreadCell;
                    break;
                case 30:
                    SpreadHeatmapCellCol30 = spreadCell;
                    break;
                case 31:
                    SpreadHeatmapCellCol31 = spreadCell;
                    break;
                case 32:
                    SpreadHeatmapCellCol32 = spreadCell;
                    break;
                case 33:
                    SpreadHeatmapCellCol33 = spreadCell;
                    break;
                case 34:
                    SpreadHeatmapCellCol34 = spreadCell;
                    break;
                case 35:
                    SpreadHeatmapCellCol35 = spreadCell;
                    break;
                case 36:
                    SpreadHeatmapCellCol36 = spreadCell;
                    break;
                case 37:
                    SpreadHeatmapCellCol37 = spreadCell;
                    break;
                case 38:
                    SpreadHeatmapCellCol38 = spreadCell;
                    break;
                case 39:
                    SpreadHeatmapCellCol39 = spreadCell;
                    break;
                case 40:
                    SpreadHeatmapCellCol40 = spreadCell;
                    break;
                case 41:
                    SpreadHeatmapCellCol41 = spreadCell;
                    break;
                case 42:
                    SpreadHeatmapCellCol42 = spreadCell;
                    break;
                case 43:
                    SpreadHeatmapCellCol43 = spreadCell;
                    break;
                case 44:
                    SpreadHeatmapCellCol44 = spreadCell;
                    break;
                case 45:
                    SpreadHeatmapCellCol45 = spreadCell;
                    break;
                case 46:
                    SpreadHeatmapCellCol46 = spreadCell;
                    break;
                case 47:
                    SpreadHeatmapCellCol47 = spreadCell;
                    break;
                case 48:
                    SpreadHeatmapCellCol48 = spreadCell;
                    break;
                case 49:
                    SpreadHeatmapCellCol49 = spreadCell;
                    break;
                case 50:
                    SpreadHeatmapCellCol50 = spreadCell;
                    break;

                default:
                    return;
            }

            spreadCell.LoadAsync(optionChain, deltaStore);
        }

        internal void RemoveCol(int col)
        {
            switch (col)
            {
                case 1:
                    SpreadHeatmapCellCol1?.Dispose();
                    break;
                case 2:
                    SpreadHeatmapCellCol2?.Dispose();
                    break;
                case 3:
                    SpreadHeatmapCellCol3?.Dispose();
                    break;
                case 4:
                    SpreadHeatmapCellCol4?.Dispose();
                    break;
                case 5:
                    SpreadHeatmapCellCol5?.Dispose();
                    break;
                case 6:
                    SpreadHeatmapCellCol6?.Dispose();
                    break;
                case 7:
                    SpreadHeatmapCellCol7?.Dispose();
                    break;
                case 8:
                    SpreadHeatmapCellCol8?.Dispose();
                    break;
                case 9:
                    SpreadHeatmapCellCol9?.Dispose();
                    break;
                case 10:
                    SpreadHeatmapCellCol10?.Dispose();
                    break;
                case 11:
                    SpreadHeatmapCellCol11?.Dispose();
                    break;
                case 12:
                    SpreadHeatmapCellCol12?.Dispose();
                    break;
                case 13:
                    SpreadHeatmapCellCol13?.Dispose();
                    break;
                case 14:
                    SpreadHeatmapCellCol14?.Dispose();
                    break;
                case 15:
                    SpreadHeatmapCellCol15?.Dispose();
                    break;
                case 16:
                    SpreadHeatmapCellCol16?.Dispose();
                    break;
                case 17:
                    SpreadHeatmapCellCol17?.Dispose();
                    break;
                case 18:
                    SpreadHeatmapCellCol18?.Dispose();
                    break;
                case 19:
                    SpreadHeatmapCellCol19?.Dispose();
                    break;
                case 20:
                    SpreadHeatmapCellCol20?.Dispose();
                    break;
                case 21:
                    SpreadHeatmapCellCol21?.Dispose();
                    break;
                case 22:
                    SpreadHeatmapCellCol22?.Dispose();
                    break;
                case 23:
                    SpreadHeatmapCellCol23?.Dispose();
                    break;
                case 24:
                    SpreadHeatmapCellCol24?.Dispose();
                    break;
                case 25:
                    SpreadHeatmapCellCol25?.Dispose();
                    break;
                case 26:
                    SpreadHeatmapCellCol26?.Dispose();
                    break;
                case 27:
                    SpreadHeatmapCellCol27?.Dispose();
                    break;
                case 28:
                    SpreadHeatmapCellCol28?.Dispose();
                    break;
                case 29:
                    SpreadHeatmapCellCol29?.Dispose();
                    break;
                case 30:
                    SpreadHeatmapCellCol30?.Dispose();
                    break;
                case 31:
                    SpreadHeatmapCellCol31?.Dispose();
                    break;
                case 32:
                    SpreadHeatmapCellCol32?.Dispose();
                    break;
                case 33:
                    SpreadHeatmapCellCol33?.Dispose();
                    break;
                case 34:
                    SpreadHeatmapCellCol34?.Dispose();
                    break;
                case 35:
                    SpreadHeatmapCellCol35?.Dispose();
                    break;
                case 36:
                    SpreadHeatmapCellCol36?.Dispose();
                    break;
                case 37:
                    SpreadHeatmapCellCol37?.Dispose();
                    break;
                case 38:
                    SpreadHeatmapCellCol38?.Dispose();
                    break;
                case 39:
                    SpreadHeatmapCellCol39?.Dispose();
                    break;
                case 40:
                    SpreadHeatmapCellCol40?.Dispose();
                    break;
                case 41:
                    SpreadHeatmapCellCol41?.Dispose();
                    break;
                case 42:
                    SpreadHeatmapCellCol42?.Dispose();
                    break;
                case 43:
                    SpreadHeatmapCellCol43?.Dispose();
                    break;
                case 44:
                    SpreadHeatmapCellCol44?.Dispose();
                    break;
                case 45:
                    SpreadHeatmapCellCol45?.Dispose();
                    break;
                case 46:
                    SpreadHeatmapCellCol46?.Dispose();
                    break;
                case 47:
                    SpreadHeatmapCellCol47?.Dispose();
                    break;
                case 48:
                    SpreadHeatmapCellCol48?.Dispose();
                    break;
                case 49:
                    SpreadHeatmapCellCol49?.Dispose();
                    break;
                case 50:
                    SpreadHeatmapCellCol50?.Dispose();
                    break;

                default:
                    return;
            }
        }

        internal bool IsEmpty()
        {
            if (SpreadHeatmapCellCol1.Initialized && !SpreadHeatmapCellCol1.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol2.Initialized && !SpreadHeatmapCellCol2.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol3.Initialized && !SpreadHeatmapCellCol3.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol4.Initialized && !SpreadHeatmapCellCol4.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol5.Initialized && !SpreadHeatmapCellCol5.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol6.Initialized && !SpreadHeatmapCellCol6.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol7.Initialized && !SpreadHeatmapCellCol7.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol8.Initialized && !SpreadHeatmapCellCol8.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol9.Initialized && !SpreadHeatmapCellCol9.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol10.Initialized && !SpreadHeatmapCellCol10.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol11.Initialized && !SpreadHeatmapCellCol11.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol12.Initialized && !SpreadHeatmapCellCol12.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol13.Initialized && !SpreadHeatmapCellCol13.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol14.Initialized && !SpreadHeatmapCellCol14.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol15.Initialized && !SpreadHeatmapCellCol15.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol16.Initialized && !SpreadHeatmapCellCol16.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol17.Initialized && !SpreadHeatmapCellCol17.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol18.Initialized && !SpreadHeatmapCellCol18.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol19.Initialized && !SpreadHeatmapCellCol19.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol20.Initialized && !SpreadHeatmapCellCol20.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol21.Initialized && !SpreadHeatmapCellCol21.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol22.Initialized && !SpreadHeatmapCellCol22.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol23.Initialized && !SpreadHeatmapCellCol23.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol24.Initialized && !SpreadHeatmapCellCol24.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol25.Initialized && !SpreadHeatmapCellCol25.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol26.Initialized && !SpreadHeatmapCellCol26.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol27.Initialized && !SpreadHeatmapCellCol27.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol28.Initialized && !SpreadHeatmapCellCol28.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol29.Initialized && !SpreadHeatmapCellCol29.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol30.Initialized && !SpreadHeatmapCellCol30.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol31.Initialized && !SpreadHeatmapCellCol31.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol32.Initialized && !SpreadHeatmapCellCol32.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol33.Initialized && !SpreadHeatmapCellCol33.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol34.Initialized && !SpreadHeatmapCellCol34.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol35.Initialized && !SpreadHeatmapCellCol35.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol36.Initialized && !SpreadHeatmapCellCol36.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol37.Initialized && !SpreadHeatmapCellCol37.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol38.Initialized && !SpreadHeatmapCellCol38.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol39.Initialized && !SpreadHeatmapCellCol39.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol40.Initialized && !SpreadHeatmapCellCol40.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol41.Initialized && !SpreadHeatmapCellCol41.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol42.Initialized && !SpreadHeatmapCellCol42.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol43.Initialized && !SpreadHeatmapCellCol43.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol44.Initialized && !SpreadHeatmapCellCol44.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol45.Initialized && !SpreadHeatmapCellCol45.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol46.Initialized && !SpreadHeatmapCellCol46.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol47.Initialized && !SpreadHeatmapCellCol47.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol48.Initialized && !SpreadHeatmapCellCol48.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol49.Initialized && !SpreadHeatmapCellCol49.IsDisposed)
            {
                return false;
            }
            if (SpreadHeatmapCellCol50.Initialized && !SpreadHeatmapCellCol50.IsDisposed)
            {
                return false;
            }
            return true;
        }

        internal void Update()
        {
            SpreadHeatmapCellCol1?.Update();
            SpreadHeatmapCellCol2?.Update();
            SpreadHeatmapCellCol3?.Update();
            SpreadHeatmapCellCol4?.Update();
            SpreadHeatmapCellCol5?.Update();
            SpreadHeatmapCellCol6?.Update();
            SpreadHeatmapCellCol7?.Update();
            SpreadHeatmapCellCol8?.Update();
            SpreadHeatmapCellCol9?.Update();
            SpreadHeatmapCellCol10?.Update();
            SpreadHeatmapCellCol11?.Update();
            SpreadHeatmapCellCol12?.Update();
            SpreadHeatmapCellCol13?.Update();
            SpreadHeatmapCellCol14?.Update();
            SpreadHeatmapCellCol15?.Update();
            SpreadHeatmapCellCol16?.Update();
            SpreadHeatmapCellCol17?.Update();
            SpreadHeatmapCellCol18?.Update();
            SpreadHeatmapCellCol19?.Update();
            SpreadHeatmapCellCol20?.Update();
            SpreadHeatmapCellCol21?.Update();
            SpreadHeatmapCellCol22?.Update();
            SpreadHeatmapCellCol23?.Update();
            SpreadHeatmapCellCol24?.Update();
            SpreadHeatmapCellCol25?.Update();
            SpreadHeatmapCellCol26?.Update();
            SpreadHeatmapCellCol27?.Update();
            SpreadHeatmapCellCol28?.Update();
            SpreadHeatmapCellCol29?.Update();
            SpreadHeatmapCellCol30?.Update();
            SpreadHeatmapCellCol31?.Update();
            SpreadHeatmapCellCol32?.Update();
            SpreadHeatmapCellCol33?.Update();
            SpreadHeatmapCellCol34?.Update();
            SpreadHeatmapCellCol35?.Update();
            SpreadHeatmapCellCol36?.Update();
            SpreadHeatmapCellCol37?.Update();
            SpreadHeatmapCellCol38?.Update();
            SpreadHeatmapCellCol39?.Update();
            SpreadHeatmapCellCol40?.Update();
            SpreadHeatmapCellCol41?.Update();
            SpreadHeatmapCellCol42?.Update();
            SpreadHeatmapCellCol43?.Update();
            SpreadHeatmapCellCol44?.Update();
            SpreadHeatmapCellCol45?.Update();
            SpreadHeatmapCellCol46?.Update();
            SpreadHeatmapCellCol47?.Update();
            SpreadHeatmapCellCol48?.Update();
            SpreadHeatmapCellCol49?.Update();
            SpreadHeatmapCellCol50?.Update();
        }

        internal void ClearAlerts()
        {
            SpreadHeatmapCellCol1?.ClearAlerts();
            SpreadHeatmapCellCol2?.ClearAlerts();
            SpreadHeatmapCellCol3?.ClearAlerts();
            SpreadHeatmapCellCol4?.ClearAlerts();
            SpreadHeatmapCellCol5?.ClearAlerts();
            SpreadHeatmapCellCol6?.ClearAlerts();
            SpreadHeatmapCellCol7?.ClearAlerts();
            SpreadHeatmapCellCol8?.ClearAlerts();
            SpreadHeatmapCellCol9?.ClearAlerts();
            SpreadHeatmapCellCol10?.ClearAlerts();
            SpreadHeatmapCellCol11?.ClearAlerts();
            SpreadHeatmapCellCol12?.ClearAlerts();
            SpreadHeatmapCellCol13?.ClearAlerts();
            SpreadHeatmapCellCol14?.ClearAlerts();
            SpreadHeatmapCellCol15?.ClearAlerts();
            SpreadHeatmapCellCol16?.ClearAlerts();
            SpreadHeatmapCellCol17?.ClearAlerts();
            SpreadHeatmapCellCol18?.ClearAlerts();
            SpreadHeatmapCellCol19?.ClearAlerts();
            SpreadHeatmapCellCol20?.ClearAlerts();
            SpreadHeatmapCellCol21?.ClearAlerts();
            SpreadHeatmapCellCol22?.ClearAlerts();
            SpreadHeatmapCellCol23?.ClearAlerts();
            SpreadHeatmapCellCol24?.ClearAlerts();
            SpreadHeatmapCellCol25?.ClearAlerts();
            SpreadHeatmapCellCol26?.ClearAlerts();
            SpreadHeatmapCellCol27?.ClearAlerts();
            SpreadHeatmapCellCol28?.ClearAlerts();
            SpreadHeatmapCellCol29?.ClearAlerts();
            SpreadHeatmapCellCol30?.ClearAlerts();
            SpreadHeatmapCellCol31?.ClearAlerts();
            SpreadHeatmapCellCol32?.ClearAlerts();
            SpreadHeatmapCellCol33?.ClearAlerts();
            SpreadHeatmapCellCol34?.ClearAlerts();
            SpreadHeatmapCellCol35?.ClearAlerts();
            SpreadHeatmapCellCol36?.ClearAlerts();
            SpreadHeatmapCellCol37?.ClearAlerts();
            SpreadHeatmapCellCol38?.ClearAlerts();
            SpreadHeatmapCellCol39?.ClearAlerts();
            SpreadHeatmapCellCol40?.ClearAlerts();
            SpreadHeatmapCellCol41?.ClearAlerts();
            SpreadHeatmapCellCol42?.ClearAlerts();
            SpreadHeatmapCellCol43?.ClearAlerts();
            SpreadHeatmapCellCol44?.ClearAlerts();
            SpreadHeatmapCellCol45?.ClearAlerts();
            SpreadHeatmapCellCol46?.ClearAlerts();
            SpreadHeatmapCellCol47?.ClearAlerts();
            SpreadHeatmapCellCol48?.ClearAlerts();
            SpreadHeatmapCellCol49?.ClearAlerts();
            SpreadHeatmapCellCol50?.ClearAlerts();
        }

        internal void Dispose()
        {
            SpreadHeatmapCellCol1?.Dispose();
            SpreadHeatmapCellCol2?.Dispose();
            SpreadHeatmapCellCol3?.Dispose();
            SpreadHeatmapCellCol4?.Dispose();
            SpreadHeatmapCellCol5?.Dispose();
            SpreadHeatmapCellCol6?.Dispose();
            SpreadHeatmapCellCol7?.Dispose();
            SpreadHeatmapCellCol8?.Dispose();
            SpreadHeatmapCellCol9?.Dispose();
            SpreadHeatmapCellCol10?.Dispose();
            SpreadHeatmapCellCol11?.Dispose();
            SpreadHeatmapCellCol12?.Dispose();
            SpreadHeatmapCellCol13?.Dispose();
            SpreadHeatmapCellCol14?.Dispose();
            SpreadHeatmapCellCol15?.Dispose();
            SpreadHeatmapCellCol16?.Dispose();
            SpreadHeatmapCellCol17?.Dispose();
            SpreadHeatmapCellCol18?.Dispose();
            SpreadHeatmapCellCol19?.Dispose();
            SpreadHeatmapCellCol20?.Dispose();
            SpreadHeatmapCellCol21?.Dispose();
            SpreadHeatmapCellCol22?.Dispose();
            SpreadHeatmapCellCol23?.Dispose();
            SpreadHeatmapCellCol24?.Dispose();
            SpreadHeatmapCellCol25?.Dispose();
            SpreadHeatmapCellCol26?.Dispose();
            SpreadHeatmapCellCol27?.Dispose();
            SpreadHeatmapCellCol28?.Dispose();
            SpreadHeatmapCellCol29?.Dispose();
            SpreadHeatmapCellCol30?.Dispose();
            SpreadHeatmapCellCol31?.Dispose();
            SpreadHeatmapCellCol32?.Dispose();
            SpreadHeatmapCellCol33?.Dispose();
            SpreadHeatmapCellCol34?.Dispose();
            SpreadHeatmapCellCol35?.Dispose();
            SpreadHeatmapCellCol36?.Dispose();
            SpreadHeatmapCellCol37?.Dispose();
            SpreadHeatmapCellCol38?.Dispose();
            SpreadHeatmapCellCol39?.Dispose();
            SpreadHeatmapCellCol40?.Dispose();
            SpreadHeatmapCellCol41?.Dispose();
            SpreadHeatmapCellCol42?.Dispose();
            SpreadHeatmapCellCol43?.Dispose();
            SpreadHeatmapCellCol44?.Dispose();
            SpreadHeatmapCellCol45?.Dispose();
            SpreadHeatmapCellCol46?.Dispose();
            SpreadHeatmapCellCol47?.Dispose();
            SpreadHeatmapCellCol48?.Dispose();
            SpreadHeatmapCellCol49?.Dispose();
            SpreadHeatmapCellCol50?.Dispose();
        }
    }
}
