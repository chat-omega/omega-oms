using DevExpress.Xpf.Charts;
using System;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class IntervalTimer
    {
        private readonly DispatcherTimer timer;
        private ChartIntervalItem interval;

        public IntervalTimer()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            timer.Tick += OnTimerTick;
        }

        public event EventHandler OnTickChanged;

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (ActualIntervalChanged() && OnTickChanged != null)
            {
                OnTickChanged.Invoke(sender, e);
            }
        }

        private bool ActualIntervalChanged()
        {
            DateTime now = DateTime.Now;
            switch (interval.MeasureUnit)
            {
                case DateTimeMeasureUnit.Millisecond:
                    return true;
                case DateTimeMeasureUnit.Second:
                    if (now.Second % interval.MeasureUnitMultiplier == 0)
                    {
                        return true;
                    }

                    break;
                case DateTimeMeasureUnit.Minute:
                    if (now.Minute % interval.MeasureUnitMultiplier == 0
                        && now.Second == 0)
                    {
                        return true;
                    }

                    break;
                case DateTimeMeasureUnit.Hour:
                    if (now.Hour % interval.MeasureUnitMultiplier == 0
                        && now.Second == 0
                        && now.Minute == 0)
                    {
                        return true;
                    }

                    break;
                case DateTimeMeasureUnit.Day:
                    if (now.Day % interval.MeasureUnitMultiplier == 0
                        && now.Second == 0
                        && now.Minute == 0
                        && now.Hour == 0)
                    {
                        return true;
                    }

                    break;
                case DateTimeMeasureUnit.Week:
                    if (now.DayOfWeek == DayOfWeek.Monday
                        && now.Second == 0
                        && now.Minute == 0
                        && now.Hour == 0)
                    {
                        return true;
                    }

                    break;
                case DateTimeMeasureUnit.Month:
                    if (now.Month % interval.MeasureUnitMultiplier == 0
                        && now.Second == 0
                        && now.Minute == 0
                        && now.Hour == 0
                        && now.Day == 1)
                    {
                        return true;
                    }

                    break;
            }
            return false;
        }

        public void Stop()
        {
            if (timer.IsEnabled)
            {
                timer.Stop();
            }
        }

        public void SetInterval(ChartIntervalItem interval)
        {
            this.interval = interval;
            if (!timer.IsEnabled)
            {
                timer.Start();
            }
        }
    }
}
