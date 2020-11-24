using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Interpolator;
using OpenTabletDriver.Plugin.Timers;

namespace TabletDriverFilters.Hawku
{
    using static Math;

    [PluginName("TabletDriver Smoothing Filter")]
    public class Smoothing : Interpolator
    {
        public Smoothing(ITimer scheduler) : base(scheduler) {  }

        private DateTime? lastFilterTime;
        private Vector3 targetPos;
        private Vector3 lastPos;
        private SyntheticTabletReport report;
        private const float threshold = 0.63f;

        public override void UpdateState(SyntheticTabletReport report)
        {
            this.targetPos = new Vector3(report.Position, report.Pressure);
            this.report = report;
        }

        public override SyntheticTabletReport Interpolate()
        {
            var newPoint = Filter(this.targetPos);
            report.Position = new Vector2(newPoint.X, newPoint.Y);
            report.Pressure = (uint)newPoint.Z;
            return report;
        }

        public Vector3 Filter(Vector3 point)
        {
            var timeDelta = DateTime.Now - this.lastFilterTime;
            // If a time difference hasn't been established or it has been 100 milliseconds since the last filter
            if (timeDelta == null || timeDelta.Value.TotalMilliseconds > 100)
            {
                this.lastPos = point;
                this.lastFilterTime = DateTime.Now;
                return point;
            }
            else
            {
                Vector3 delta = point - this.lastPos;

                double stepCount = Latency / TimerInterval;
                double target = 1 - threshold;
                double weight = 1.0 - (1.0 / Pow(1.0 / target, 1.0 / stepCount));

                this.lastPos += delta * (float)weight;
                this.lastFilterTime = DateTime.Now;
                return this.lastPos;
            }
        }

        public static FilterStage FilterStage => FilterStage.PostTranspose;

        [SliderProperty("Latency", 0f, 5f, 2f)]
        public float Latency { set; get; }

        public float TimerInterval
        {
            get => 1000 / Hertz;
        }
    }
}