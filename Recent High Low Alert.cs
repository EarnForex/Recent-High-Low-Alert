// -------------------------------------------------------------------------------
//   Draws lines on the high/low of the recent N bars from selected timeframe.
//   Alerts when the Bid price of the current bar crosses previous high/low.
//   Shift parameter allows displaying High/Low from previous periods.
//
//   Version 1.02
//   Copyright 2025, EarnForex.com
//   https://www.earnforex.com/indicators/Recent-High-Low-Alert/
// -------------------------------------------------------------------------------

using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MTFRecentHighLowAlert : Indicator
    {
        [Parameter("Timeframe", DefaultValue = "Current")]
        public TimeFrame SelectedTimeFrame { get; set; }

        [Parameter("N", DefaultValue = 20, MinValue = 1, Group = "Main")]
        public int N { get; set; }

        [Parameter("Shift", DefaultValue = 0, MinValue = 0, Group = "Main")]
        public int Shift { get; set; }

        [Parameter("Trigger Candle", DefaultValue = TriggerCandle.Previous, Group = "Main")]
        public TriggerCandle TriggerCandleOption { get; set; }

        // Not implemented?
        //[Parameter("Native Alerts", DefaultValue = false, Group = "Alerts")]
        //public bool EnableNativeAlerts { get; set; }

        [Parameter("Sound Alerts", DefaultValue = false, Group = "Alerts")]
        public bool EnableSoundAlerts { get; set; }

        [Parameter("Sound Type", DefaultValue = SoundType.Announcement, Group = "Alerts")]
        public SoundType SoundType { get; set; }

        [Parameter("Email Alerts", DefaultValue = false, Group = "Alerts")]
        public bool EnableEmailAlerts { get; set; }

        [Parameter("Email Address", DefaultValue = "", Group = "Alerts")]
        public string EmailAddress { get; set; }

        [Output("High", LineColor = "DodgerBlue", PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries High { get; set; }

        [Output("Low", LineColor = "Yellow", PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries Low { get; set; }

        private DateTime LastHighAlert = new DateTime(1970, 1, 1);
        private DateTime LastLowAlert = new DateTime(1970, 1, 1);
        private bool PriceAboveHigh = false;
        private bool PriceBelowLow = false;
        private Bars _timeframeBars;
        private TimeFrame _usedTimeFrame;

        public enum TriggerCandle
        {
            Current = 0,
            Previous = 1
        }

        private enum Direction
        {
            HIGH,
            LOW
        }

        protected override void Initialize()
        {
            // Determine the timeframe to use
            if (SelectedTimeFrame.ToString() == "Current")
                _usedTimeFrame = TimeFrame;
            else
                _usedTimeFrame = SelectedTimeFrame;

            // Check if selected timeframe is lower than current
            if (_usedTimeFrame < TimeFrame)
            {
                _usedTimeFrame = TimeFrame;
                Print("Selected timeframe is lower than current chart timeframe. Using current timeframe instead.");
            }

            // Get bars for the selected timeframe
            _timeframeBars = MarketData.GetBars(_usedTimeFrame);
        }

        public override void Calculate(int index)
        {
            // Get the corresponding index on the selected timeframe
            var currentTime = Bars.OpenTimes[index];
            var tfIndex = _timeframeBars.OpenTimes.GetIndexByTime(currentTime);

            if (tfIndex < 0)
            {
                // No corresponding bar found, use previous values
                if (index > 0)
                {
                    High[index] = High[index - 1];
                    Low[index] = Low[index - 1];
                }
                return;
            }

            // Apply shift
            tfIndex -= Shift;

            // Check if we have enough bars
            if (tfIndex - N + 1 < 0)
            {
                // Not enough data, use previous values
                if (index > 0)
                {
                    High[index] = High[index - 1];
                    Low[index] = Low[index - 1];
                }
                return;
            }

            // Find highest high and lowest low in the last N bars of selected timeframe
            double highest = 0;
            double lowest = double.MaxValue;

            for (int i = 0; i < N; i++)
            {
                if (tfIndex - i < 0) break;

                double highValue = _timeframeBars.HighPrices[tfIndex - i];
                double lowValue = _timeframeBars.LowPrices[tfIndex - i];

                if (highValue > highest) highest = highValue;
                if (lowValue < lowest) lowest = lowValue;
            }

            High[index] = highest;
            Low[index] = lowest;

            // Alert checking (only in real-time)
            if (IsLastBar && index == Bars.Count - 1)
            {
                int triggerIndex = (int)TriggerCandleOption;
                double currentHigh = High[index - triggerIndex];
                double currentLow = Low[index - triggerIndex];

                // Check if price crossed above high
                if (Symbol.Bid > currentHigh)
                {
                    // Only alert if we weren't already above high
                    if (!PriceAboveHigh && LastHighAlert != Bars.OpenTimes[index])
                    {
                        SendAlert(Direction.HIGH, currentHigh, index);
                        PriceAboveHigh = true;
                    }
                }
                else
                {
                    // Price is not above high, reset flag
                    PriceAboveHigh = false;
                }

                // Check if price crossed below low
                if (Symbol.Bid < currentLow)
                {
                    // Only alert if we weren't already below low
                    if (!PriceBelowLow && LastLowAlert != Bars.OpenTimes[index])
                    {
                        SendAlert(Direction.LOW, currentLow, index);
                        PriceBelowLow = true;
                    }
                }
                else
                {
                    // Price is not below low, reset flag
                    PriceBelowLow = false;
                }
            }
        }

        // Issues alerts and remembers last sent alert time.
        private void SendAlert(Direction direction, double price, int index)
        {
            string alert = "Local ";
            string subject;
            string tfStr = _usedTimeFrame.ToString();

            if (direction == Direction.HIGH)
            {
                alert += $"high ({tfStr})";
                subject = $"High broken @ {Symbol.Name} - {tfStr}";
                LastHighAlert = Bars.OpenTimes[index];
            }
            else if (direction == Direction.LOW)
            {
                alert += $"low ({tfStr})";
                subject = $"Low broken @ {Symbol.Name} - {tfStr}";
                LastLowAlert = Bars.OpenTimes[index];
            }
            else
            {
                // Default (some enum error)
                subject = "Error";
            }

            alert += $" broken at {price.ToString("F" + Symbol.Digits.ToString())}.";

            //if (EnableNativeAlerts)
            //{
                // Not implemented?
                //Notifications.ShowPopup("Recent High/Low Alert", alert, PopupNotificationState.Information);
            //}

            if (EnableSoundAlerts)
            {
                Notifications.PlaySound(SoundType);
            }

            if (EnableEmailAlerts && !string.IsNullOrEmpty(EmailAddress))
            {
                Notifications.SendEmail(EmailAddress, EmailAddress, subject, 
                                      $"{Server.Time.ToString("yyyy-MM-dd HH:mm:ss")} {alert}");
            }
        }
    }
}