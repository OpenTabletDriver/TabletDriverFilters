using System;
using System.Numerics;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet.Interpolator;
using OpenTabletDriver.Plugin.Timers;

namespace TabletDriverFilters.Hawku
{
    [PluginName("Hawku Smoothing Filter")]
    public class Smoothing : Interpolator
    {
        public Smoothing(ITimer scheduler) : base(scheduler)
        {
            GetMMScale();
        }

        [SliderProperty("Latency", 0.0f, 1000.0f, 2.0f), DefaultPropertyValue(2f), ToolTip
        ("Smoothing Filter\n" +
        "Smoothing filter adds latency to the input, so don't enable it if you want the lowest possible input latency.\n\n" +

        "Recommendations\n" +
        "On Wacom tablets you can use latency value between 15 and 25 to have a similar smoothing as in the Wacom drivers.\n" +
        "You can test out different filter values, but recommended maximum for osu! is around 50 milliseconds.\n" +
        "Filter latency value lower than 4 milliseconds isn't recommended. Its better to just disable the smoothing filter.\n" +
        "You don't have to change the filter frequency, but you can use the highest frequency your computer can run without performance problems.")]
        public float Latency { set; get; }

        private const float THRESHOLD = 0.63f;
        private float timerInterval => 1000 / Frequency;
        private float weight;
        private DateTime? lastFilterTime;
        private Vector3 mmScale;
        private Vector3 targetPos;
        private Vector3 lastPos;
        private SyntheticTabletReport report;

        public override void UpdateState(SyntheticTabletReport report)
        {
            this.targetPos = new Vector3(report.Position, report.Pressure) * mmScale;
            this.report = report;
        }

        public override SyntheticTabletReport Interpolate()
        {
            var newPoint = Filter(this.targetPos) / mmScale;
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
                SetWeight(Latency);
                return point;
            }
            else
            {
                Vector3 delta = point - this.lastPos;

                this.lastPos += delta * weight;
                this.lastFilterTime = DateTime.Now;
                return this.lastPos;
            }
        }

        private void SetWeight(float latency)
        {
            float stepCount = latency / timerInterval;
            float target = 1 - THRESHOLD;
            this.weight = 1f - (1f / MathF.Pow(1f / target, 1f / stepCount));
        }

        private void GetMMScale()
        {
            var digitizer = Info.Driver.Tablet.Digitizer;
            this.mmScale = new Vector3
            {
                X = digitizer.Width / digitizer.MaxX,
                Y = digitizer.Height / digitizer.MaxY,
                Z = 1  // passthrough
            };
        }
    }
}