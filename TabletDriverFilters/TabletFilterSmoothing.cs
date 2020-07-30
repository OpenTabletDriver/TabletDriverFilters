using System;
using TabletDriverPlugin;
using TabletDriverPlugin.Attributes;
using TabletDriverPlugin.Tablet;

namespace TabletDriverFilters
{
    using static Math;

    [PluginName("TabletDriver Smoothing Filter")]
    public class TabletFilterSmoothing : IFilter
    {
        private DateTime? _lastFilterTime;
        private Point _lastPos;

        public Point Filter(Point point)
        {
            var timeDelta = DateTime.Now - _lastFilterTime;
            // If a time difference hasn't been established or it has been 100 milliseconds since the last filter
            if (timeDelta == null || timeDelta.Value.TotalMilliseconds > 100)
            {
                SetPreviousState(point);
                return point;
            }
            else
            {
                Point pos = new Point(point.X, point.Y);
                float deltaX = point.X - _lastPos.X;
                float deltaY = point.Y - _lastPos.Y;

                float stepCount = Latency / TimerInterval;
                float target = 1 - Threshold;
                float weight = 1f - (1f / (float)Pow(1 / target, 1 / stepCount));

                pos.X += deltaX * weight;
                pos.Y += deltaY * weight;
                SetPreviousState(point);
                return pos;
            }
        }

        private void SetPreviousState(Point lastPosition)
        {
            _lastPos = lastPosition;
            _lastFilterTime = DateTime.Now;
        }

        public FilterStage FilterStage => FilterStage.PreTranspose;

        [SliderProperty("Latency", 0f, 5f, 2f)]
        public float Latency { set; get; }

        [SliderProperty("Weight", 0f, 1f, 1f)]
        public float Weight { set; get; }

        [SliderProperty("Threshold", 0f, 1f, 0.9f)]
        public float Threshold { set; get; }

        [UnitProperty("Timer Interval", "hz")]
        public float TimerInterval { set; get; }
    }
}