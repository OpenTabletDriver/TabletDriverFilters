using System;
using System.Numerics;
using OpenTabletDriver.Plugin;
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
        public AntiChatter(ITimer scheduler) : base(scheduler)
        {
            GetMMScale();
        }

        [SliderProperty("Latency", 0f, 1000f, 2f), DefaultPropertyValue(2f)]
        public float Latency
        {
            set => this.latency = Math.Clamp(value, 0, 1000);
            get => this.latency;
        }

        [Property("Antichatter Strength"), DefaultPropertyValue(3f)]
        public float AntichatterStrength { set; get; }

        [Property("Antichatter Multiplier"), DefaultPropertyValue(1f)]
        public float AntichatterMultiplier { set; get; }

        [Property("Antichatter Offset X")]
        public float AntichatterOffsetX { set; get; }

        [Property("Antichatter Offset Y"), DefaultPropertyValue(1f)]
        public float AntichatterOffsetY { set; get; }

        [BooleanProperty("Prediction", "")]
        public bool PredictionEnabled { set; get; }

        [Property("Prediction Strength"), DefaultPropertyValue(1.1f)]
        public float PredictionStrength { set; get; }

        [Property("Prediction Sharpness"), DefaultPropertyValue(1f)]
        public float PredictionSharpness { set; get; }

        [Property("Prediction Offset X"), DefaultPropertyValue(3f)]
        public float PredictionOffsetX { set; get; }

        [Property("Prediction Offset Y"), DefaultPropertyValue(0.3f)]
        public float PredictionOffsetY { set; get; }

        private const float THRESHOLD = 0.9f;
        private bool isReady;
        private float timerInterval => 1000 / Frequency;
        private float latency = 2.0f;
        private float weight;
        private Vector2 mmScale;
        private Vector2 position;
        private Vector2 prevTargetPos, targetPos, calcTarget;
        private SyntheticTabletReport report;

        public override void UpdateState(SyntheticTabletReport report)
        {
            this.targetPos = report.Position * mmScale;

            if (PredictionEnabled)
            {
                // Calculate predicted position onNewPacket
                if (this.prevTargetPos.X != this.targetPos.X || this.prevTargetPos.Y != this.targetPos.Y)
                {
                    // Calculate distance between last 2 packets and prediction
                    var delta = this.targetPos - this.prevTargetPos;
                    var distance = Vector2.Distance(this.prevTargetPos, this.targetPos);
                    var predictionModifier = 1 / Cosh((distance - PredictionOffsetX) * PredictionSharpness) * PredictionStrength + PredictionOffsetY;

                    // Apply prediction
                    delta *= predictionModifier;

                    // Update predicted position
                    this.calcTarget = this.targetPos + delta;

                    // Update old position for further prediction
                    this.prevTargetPos = this.targetPos;
                }
            }
            else
                calcTarget = targetPos;

            this.report = report;
        }

        public override SyntheticTabletReport Interpolate()
        {
            this.report.Position = Filter(this.calcTarget) / mmScale;
            return this.report;
        }

        public Vector2 Filter(Vector2 calcTarget)
        {
            if (!this.isReady)
            {
                this.position = calcTarget;
                SetWeight(Latency);
                this.isReady = true;
                return calcTarget;
            }

            var delta = calcTarget - this.position;
            var distance = Vector2.Distance(this.position, calcTarget);

            // Devocub smoothing
            // Increase weight of filter in {formula} times
            var weightModifier = (float)(Pow(distance + AntichatterOffsetX, AntichatterStrength * -1) * AntichatterMultiplier);

            // Limit minimum
            if (weightModifier + AntichatterOffsetY < 0)
                weightModifier = 0;
            else
                weightModifier += AntichatterOffsetY;

            weightModifier = weight / weightModifier;
            weightModifier = Math.Clamp(weightModifier, 0, 1);
            this.position += delta * weightModifier;

            return this.position;
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
            this.mmScale = new Vector2
            {
                X = digitizer.Width / digitizer.MaxX,
                Y = digitizer.Height / digitizer.MaxY
            };
        }
    }
}