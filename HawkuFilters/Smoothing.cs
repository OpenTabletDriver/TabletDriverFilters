using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletDriverFilters.Hawku
{
    using static Math;

    [PluginName("TabletDriver Smoothing Filter")]
    public class Smoothing : IFilter
    {
        private DateTime? _lastFilterTime;
        private Vector2 _lastPos;
        private float _timerInterval;
        private const float _threshold = 0.63f;

        public Vector2 Filter(Vector2 point)
        {
            var timeDelta = DateTime.Now - _lastFilterTime;
            // If a time difference hasn't been established or it has been 100 milliseconds since the last filter
            if (timeDelta == null || timeDelta.Value.TotalMilliseconds > 100 || _lastPos == null)
            {
                SetPreviousState(point);
                return point;
            }
            else
            {
                Vector2 pos = new Vector2(_lastPos.X, _lastPos.Y);
                float deltaX = point.X - _lastPos.X;
                float deltaY = point.Y - _lastPos.Y;

                double stepCount = Latency / TimerInterval;
                double target = 1 - _threshold;
                double weight = 1.0 - (1.0 / Pow(1.0 / target, 1.0 / stepCount));

                pos.X += (float)(deltaX * weight);
                pos.Y += (float)(deltaY * weight);
                SetPreviousState(pos);
                return pos;
            }
        }

        private void SetPreviousState(Vector2 lastPosition)
        {
            _lastPos = lastPosition;
            _lastFilterTime = DateTime.Now;
        }

        public FilterStage FilterStage => FilterStage.PostTranspose;

        [SliderProperty("Latency", 0f, 5f, 2f)]
        public float Latency { set; get; }

        [UnitProperty("Timer Interval", "hz")]
        public float TimerInterval
        {
            set => _timerInterval = 1000f / value;
            get => _timerInterval;
        }
    }
}