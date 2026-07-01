using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using ZeroPlus.Comms.Helper.Concurrency;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Notifications
{
    public class NotificationManager
    {
        private readonly ProducerConsumer _notificationsQueue;
        private readonly Thread _notificationPumpThread;
        private readonly BulletinBroker _bulletinBroker;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly SpeechSynthesizer _synthesizer = new();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public ObservableCollection<CustomNotificationModel> CustomNotifications { get; set; } = [];

        public static List<string> SoundsList => SoundManager.LoadedSounds;

        public NotificationManager(BulletinBroker bulletinBroker)
        {
            _bulletinBroker = bulletinBroker;
            _notificationsQueue = new ProducerConsumer();
            void NotificationConsumer(object notificationsViewModel) => NotificationPumpHandler(_notificationsQueue, notificationsViewModel as NotificationsViewModel);
            _notificationPumpThread = new Thread(NotificationConsumer)
            {
                IsBackground = true
            };
            LoadCustomNotifications();
            SoundManager.InitializeSoundPlayers();
        }

        public void Subscribe(NotificationsViewModel notificationsViewModel)
        {
            _notificationPumpThread.Start(notificationsViewModel);
        }

        public void LoadCustomNotifications()
        {
            try
            {
                string file = OmsConfig.GetCustomNotificationsExportPath();

                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<CustomNotification> customNotifications = JsonConvert.DeserializeObject<List<CustomNotification>>(json).Where(x => !string.IsNullOrWhiteSpace(x.Tag)).ToList();
                    CustomNotifications.Clear();
                    foreach (CustomNotification customNotification in customNotifications)
                    {
                        CustomNotificationModel model = new();
                        model.Derserialize(customNotification);
                        CustomNotifications.Add(model);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCustomNotifications));
            }
        }

        public void SaveCustomNotifications()
        {
            try
            {
                string file = OmsConfig.GetCustomNotificationsExportPath();

                List<CustomNotification> export = CustomNotifications.Select(x => x.Serialize()).ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveCustomNotifications));
            }
        }

        public void AddTransaction(IOrder order)
        {
            if (!OmsCore.Config.NotificationsForMyOrdersOnly ||
                (OmsCore.HerculesClientConfig.TransactionSubscriptionMode is TransactionSubscriptionMode.All &&
                 string.Equals(OmsCore.User.Username, order.Trader, StringComparison.OrdinalIgnoreCase)))
            {
                AddOrder(order);
            }
        }

        public bool AddOrder(IOrder order)
        {
            try
            {
                bool notified = false;
                switch (order.OrderStatus)
                {
                    case OrderStatus.New:
                        NotifyNew(order);
                        notified = true;
                        break;
                    case OrderStatus.PendingNew:
                        break;
                    case OrderStatus.PartiallyFilled:
                        NotifyPartialFill(order);
                        notified = true;
                        break;
                    case OrderStatus.Filled:
                        NotifyFill(order);
                        notified = true;
                        break;
                    case OrderStatus.Canceled:
                        NotifyCancel(order);
                        notified = true;
                        break;
                    case OrderStatus.Replaced:
                        NotifyReplace(order);
                        notified = true;
                        break;
                    case OrderStatus.Rejected:
                        NotifyReject(order);
                        notified = true;
                        break;
                }

                return notified;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddTransaction));
                return default;
            }
        }

        private void NotifyNew(IOrder order)
        {
            if (OmsCore.Config.ShowOrderAckNotificationsV2)
            {
                ShowVisualNotification(order);
            }

            if (OmsCore.Config.PlayOrderAckNotificationSoundV2)
            {
                SoundManager.Play("OrderBook_Ack");
            }
        }

        internal void NotifyPartialFill(IOrder order)
        {
            if (OmsCore.Config.ShowOrderPartialFillNotificationsV2)
            {
                ShowVisualNotification(order);
            }

            if (OmsCore.Config.PlayOrderPartialFillNotificationSoundV2)
            {
                SoundManager.Play("OrderBook_PartFill");
            }

            PlayPartialsOnlyCustomNotification(order.Tag, order.SubType);
        }

        internal void NotifyFill(IOrder order)
        {
            if (OmsCore.Config.ShowOrderFillNotificationsV2)
            {
                ShowVisualNotification(order);
            }

            if (OmsCore.Config.PlayOrderFillNotificationSoundV2)
            {
                SoundManager.Play("OrderBook_Fill");
            }

            PlayCustomNotification(order.Tag, order.SubType);
        }

        private void NotifyCancel(IOrder order)
        {
            if (OmsCore.Config.ShowOrderCancelNotificationsV2)
            {
                ShowVisualNotification(order);
            }

            if (OmsCore.Config.PlayOrderCancelNotificationSoundV2)
            {
                SoundManager.Play("OrderBook_Cancel");
            }
        }

        private void NotifyReplace(IOrder order)
        {
            if (OmsCore.Config.ShowOrderReplaceNotificationsV2)
            {
                ShowVisualNotification(order);
            }

            if (OmsCore.Config.PlayOrderReplaceNotificationSoundV2)
            {
                SoundManager.Play("OrderBook_Replace");
            }
        }

        private void NotifyReject(IOrder order)
        {
            if (OmsCore.Config.ShowOrderRejectNotificationsV2)
            {
                ShowVisualNotification(order);
            }

            if (OmsCore.Config.PlayOrderRejectNotificationSoundV2)
            {
                SoundManager.Play("OrderBook_Reject");
            }
        }

        private void ShowVisualNotification(IOrder order)
        {
            _notificationsQueue.Produce(new Notification()
            {
                Account = order.AccountAcronym,
                Time = order.LastUpdateTime,
                Type = GetNotificationType(order),
                Text = GetNotificationText(order),
                Tag = order
            });
        }

        private void PlayPartialsOnlyCustomNotification(string tag, OrderSubType? subType)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tag = tag.ToUpper();
                CustomNotificationModel customNotification = CustomNotifications.FirstOrDefault(x => x.PartiallsOnly && x.Tag != null && tag.Contains(x.Tag.ToUpper()) && (string.IsNullOrWhiteSpace(x.SubType) || x.SubType == subType?.ToString().FromCamelCase()));
                if (customNotification != null)
                {
                    SoundManager.Play(customNotification.Sound);
                }
                else
                {
                    PlayCustomNotification(tag, subType);
                }
            }
        }

        private void PlayCustomNotification(string tag, OrderSubType? subType)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tag = tag.ToUpper();
                CustomNotificationModel customNotification = CustomNotifications.FirstOrDefault(x => !x.CloseOnly && !x.PartiallsOnly && x.Tag != null && tag.Contains(x.Tag.ToUpper()) && (string.IsNullOrWhiteSpace(x.SubType) || x.SubType == subType?.ToString().FromCamelCase()));
                if (customNotification != null)
                {
                    SoundManager.Play(customNotification.Sound);
                }
            }
        }

        private static void NotificationPumpHandler(ProducerConsumer notificationsQueue, NotificationsViewModel notificationViewModel)
        {
            while (notificationsQueue.Consume() is Notification notification)
            {
                notificationViewModel.AddNotification(notification);
            }
        }

        private static NotificationType GetNotificationType(IOrder order)
        {
            if (!order.IsComplexOrder)
            {
                switch (order.OrderStatus)
                {
                    case OrderStatus.New:
                        return order.Side == Side.Buy ? NotificationType.ORDER_BUY_PLACED : NotificationType.ORDER_SELL_PLACED;
                    case OrderStatus.PartiallyFilled:
                        return order.Side == Side.Buy ? NotificationType.ORDER_BUY_PART_FILL : NotificationType.ORDER_SELL_PART_FILL;
                    case OrderStatus.Filled:
                        return order.Side == Side.Buy ? NotificationType.ORDER_BUY_FILL : NotificationType.ORDER_SELL_FILL;
                    case OrderStatus.Canceled:
                        return NotificationType.ORDER_CANCEL;
                    case OrderStatus.Replaced:
                        return NotificationType.ORDER_REPLACE;
                    case OrderStatus.Rejected:
                        return NotificationType.ORDER_REJECT;
                }
            }
            else if (order.IsComplexOrder)
            {
                switch (order.OrderStatus)
                {
                    case OrderStatus.New:
                        return order.Price >= 0.0 ? NotificationType.ORDER_BUY_PLACED : NotificationType.ORDER_SELL_PLACED;
                    case OrderStatus.PartiallyFilled:
                        return order.Price >= 0.0 ? NotificationType.ORDER_BUY_PART_FILL : NotificationType.ORDER_SELL_PART_FILL;
                    case OrderStatus.Filled:
                        return order.Price >= 0.0 ? NotificationType.ORDER_BUY_FILL : NotificationType.ORDER_SELL_FILL;
                    case OrderStatus.Canceled:
                        return NotificationType.ORDER_CANCEL;
                    case OrderStatus.Replaced:
                        return NotificationType.ORDER_REPLACE;
                    case OrderStatus.Rejected:
                        return NotificationType.ORDER_REJECT;
                }
            }
            return NotificationType.NONE;
        }

        private static string GetNotificationText(IOrder order)
        {
            return order.OrderStatus switch
            {
                OrderStatus.Filled or OrderStatus.PartiallyFilled => string.Format($"[{GetAction(order.OrderStatus.ToString())}] {order.Side} {order.FilledQty} {order.Symbol} @ {order.AveragePrice}"),
                _ => string.Format($"[{GetAction(order.OrderStatus.ToString())}] {order.Side} {order.Quantity} {order.Symbol} @ {order.Price}"),
            };
        }

        public static string GetAction(string orderStatus)
        {
            return orderStatus switch
            {
                "New" => "ACK",
                "PartiallyFilled" => "PART FILL",
                "Filled" => "FILL",
                "Canceled" => "CXL",
                "Replaced" => "RPLC",
                "Rejected" => "REJ",
                _ => "?",
            };
        }

        public void FirstEdgeAcquired(IPosition position)
        {
            if (OmsCore.Config.ShowFirstEdgeNotificationsV2 && position.FirstEdge >= OmsCore.Config.ShowFirstEdgeNotificationsThreshold)
            {
                _notificationsQueue.Produce(new Notification()
                {
                    Time = DateTime.Now,
                    Type = NotificationType.FIRST_EDGE_ACQUIRED,
                    Text = "FIRST EDGE - " + position.FirstEdge.ToString("C2") + "\n" + position.Name.ToUpper(),
                    Tag = position
                });
            }

            if (OmsCore.Config.PlayFirstEdgeNotificationSoundV2 && position.FirstEdge >= OmsCore.Config.PlayFirstEdgeNotificationsThreshold)
            {
                SoundManager.Play(OmsCore.Config.FirstEdgeNotificationSound);
            }
        }

        public void AddInfo(string text, DateTime time, object tag = null)
        {
            _notificationsQueue.Produce(new Notification()
            {
                Time = time,
                Type = NotificationType.FIRST_EDGE_ACQUIRED,
                Text = text,
                Tag = tag
            });
        }

        public void AddAlert(string text, DateTime time, string source, object tag = null)
        {
            _notificationsQueue.Produce(new Notification()
            {
                Time = time,
                Type = NotificationType.ALERT,
                Text = text,
                Tag = tag
            });

            BulletinMessage message = new BulletinMessage
            {
                Time = time,
                Message = text,
                Source = source
            };
            _bulletinBroker.AddMessage(message);
        }

        internal void PlayTts(string text)
        {
            try
            {
                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PlayTts));
            }
        }

        public void AddIndicatorUpdate(IOrder order)
        {
            try
            {
                double rounded = Math.Round(order.EdgeGiveUp, 2);
                if (rounded >= 0 && string.Equals(OmsCore.User.Username, order.Trader, StringComparison.OrdinalIgnoreCase))
                {
                    if (OmsCore.Config.PlayEdgeGiveUpNotificationV2 &&
                        Math.Round(OmsCore.Config.PlayEdgeGiveUpNotificationThreshold, 2) <= rounded)
                    {
                        SoundManager.Play(OmsCore.Config.EdgeGiveUpNotificationSound);
                    }

                    if (OmsCore.Config.ShowEdgeGiveUpNotificationV2)
                    {
                        _notificationsQueue.Produce(new Notification()
                        {
                            Time = order.LastUpdateTime,
                            Type = NotificationType.FIRST_EDGE_ACQUIRED,
                            Text = $"{order.EdgeGiveUp:N2} Edge Give UP!\n{order.SpreadId}",
                            Tag = order.SpreadId
                        });
                    }

                    _log.Info($"User Edge Give Up! Spread: {order.SpreadId}, Time: {order.LastUpdateTime:T}, Give-Up: {order.EdgeGiveUp}");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddIndicatorUpdate));
            }
        }
    }
}
