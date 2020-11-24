using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Interpolator;
using OpenTabletDriver.Plugin.Timers;

namespace TabletDriverFilters.Devocub
{
    using static MathF;

    [PluginName("TabletDriver AntiChatter Filter")]
    public class AntiChatter : Interpolator
    {
        public AntiChatter(ITimer scheduler) : base(scheduler) {  }

        private Vector2 lastPos;
        private Vector2 targetPos;
        private SyntheticTabletReport report;
        private const float threshold = 0.9f;
        private float latency = 2.0f;

        public override void UpdateState(SyntheticTabletReport report)
        {
            this.targetPos = report.Position;
            this.report = report;
        }

        public override SyntheticTabletReport Interpolate()
        {
            this.report.Position = Filter(this.targetPos);
            return this.report;
        }

        public Vector2 Filter(Vector2 point)
        {
            Vector2 calcTarget = new Vector2();
            Vector2 delta;
            float distance, weightModifier, predictionModifier;

            if (PredictionEnabled)
            {
                // Calculate predicted position onNewPacket
                if (this.lastPos.X != point.X || this.lastPos.Y != point.Y)
                {
                    // Calculate distance between last 2 packets and prediction
                    delta = point - lastPos;
                    distance = Vector2.Distance(lastPos, point);
                    predictionModifier = 1 / Cosh((distance - PredictionOffsetX) * PredictionSharpness) * PredictionStrength + PredictionOffsetY;

                    // Apply prediction
                    delta *= predictionModifier;

                    // Update predicted position
                    calcTarget = point + delta;

                    // Update old position for further prediction
                    this.lastPos = point;
                }
            }
            else
                calcTarget = point;

            delta = calcTarget - lastPos;
            distance = Vector2.Distance(lastPos, calcTarget);

            float stepCount = Latency / TimerInterval;
            float target = 1 - threshold;
            float weight = (float)(1.0 - (1.0 / Pow((float)(1.0 / target), (float)(1.0 / stepCount))));

            // Devocub smoothing
            // Increase weight of filter in {formula} times
            weightModifier = (float)(Pow(distance + AntichatterOffsetX, AntichatterStrength * -1) * AntichatterMultiplier);

            // Limit minimum
            if (weightModifier + AntichatterOffsetY < 0)
                weightModifier = 0;
            else
                weightModifier += AntichatterOffsetY;

            weightModifier = weight / weightModifier;
            weightModifier = Math.Clamp(weightModifier, 0, 1);
            this.lastPos += delta * weightModifier;

            return lastPos;
        }

        public static FilterStage FilterStage => FilterStage.PostTranspose;

        [SliderProperty("Latency", 0f, 1000f, 2f)]
        public float Latency
        {
            set => this.latency = Math.Clamp(value, 0, 1000);
            get => this.latency;
        }

        public float TimerInterval
        {
            get => 1000 / Hertz;
        }

        [Property("Antichatter Strength")]
        public float AntichatterStrength { set; get; } = 3;

        [Property("Antichatter Multiplier")]
        public float AntichatterMultiplier { set; get; } = 1;

        [Property("Antichatter Offset X")]
        public float AntichatterOffsetX { set; get; }

        [Property("Antichatter Offset Y")]
        public float AntichatterOffsetY { set; get; } = 1;

        [BooleanProperty("Prediction", "")]
        public bool PredictionEnabled { set; get; }

        [Property("Prediction Strength")]
        public float PredictionStrength { set; get; } = 1.1f;

        [Property("Prediction Sharpness")]
        public float PredictionSharpness { set; get; } = 1;

        [Property("Prediction Offset X")]
        public float PredictionOffsetX { set; get; } = 3;

        [Property("Prediction Offset Y")]
        public float PredictionOffsetY { set; get; } = 0.3f;
    }
}