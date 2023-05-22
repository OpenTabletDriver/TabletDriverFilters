using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletDriverFilters.Devocub
{
    [PluginName("Devocub Antichatter")]
    public class Antichatter : MillimeterAsyncPositionedPipelineElement
    {
        private const string LATENCY_TOOLTIP =
              "Smoothing latency\n"
            + " - Smoothing filter adds latency to the input, so don't enable it if you want to have the lowest possible input lag.\n"
            + " - On Wacom tablets you can use latency value between 15 and 25 to have a similar smoothing as in the Wacom drivers.\n"
            + " - You can test out different filter values, but the recommended maximum for osu! is around 50 milliseconds.\n"
            + " - Filter latency values lower than 4 milliseconds aren't recommended. Its better to disable the smoothing filter.\n"
            + " - You don't have to change the filter frequency, but you can use the highest frequency your computer can run without performance problems.";
        private const string ANTICHATTER_TOOLTIP =
              "Antichatter is meant to prevent cursor chattering/rattling/shaking/trembling when the pen doesn't move.\n"
            + " - Antichatter in its primary form is useful for tablets which don't have any hardware smoothing.\n"
            + " - Antichatter uses smoothing. Latency and Frequency values do have an effect on antichatter settings.\n"
            + "\n"
            + "Formula for smoothing is:\n"
            + "   y(x) = (x + OffsetX)^(Strength*-1)*Multiplier+OffsetY\n"
            + " - Where x is pen speed. And y(x) is the smoothing value. Slower speed = more smoothing. Faster speed = less smoothing.\n"
            + "\n"
            + "Strength : Useful values are from 1 up to 10. Higher values make smoothing sharper, lower are smoother.\n"
            + "Multiplier : Zooms in and zooms out the plot. Useful values are from 1 up to 1000. Makes smoothing softer. Default value is 1, which causes no change.\n"
            + "Offset X : Moves the plot to the right. Negative values move the plot to the left. Higher values make smoothing weaker,\n"
            + "   lower values stronger and activate stronger smoothing earlier in terms of cursor speed). Useful values are from -1 to 2. Default values is 0.\n"
            + "Offset Y : Moves the plot up. Useful values are from roughly -1 up to 10. If the Y value of smoothing is near 0 for any given point then it provides almost raw data with lowest delay.\n"
            + "   If value is near 1 then it's usual smoothing, also it defines minimal amount of smoothing. OffsetY 10 will make smoothing 10 times stronger.\n"
            + "   OffsetY 0.5 will make smoothing roughly twice as weak (and latency will be roughly half), 0.3 roughly one third weaker, etc. The default value is 1.\n"
            + "\n"
            + "Example Settings:\n"
            + " - Simple: Latency 5-50 ms, Strength 2-3, Multiplier 1, OffsetX 0, OffsetY 1.\n"
            + "\n"
            + " - Straight: Latency 20-40ms, Strength 20, Multiplier 1, OffsetX 0.7, OffsetY 0.6. This preset isn't good for high hovering.\n"
            + "\n"
            + " - Smooth: Latency ~10 ms, Strength 3, Multiplier 100, OffsetX 1.5, OffsetY 1.\n"
            + "      Change OffsetX between 0-2 to switch between stickiness and smooth.\n"
            + "      Increase Strength to 4-10 to get harper. Decrease Strength to 1-2 to get more smoothing.\n"
            + "\n"
            + " - Low latency: Set Offset Y to 0 (and potentially set Latency to 1-10 ms. However, with some settings this can break smoothing, usually OffsetY 0 is enough to being able to go to lowest latency).";
        private const string PREDICTION_TOOLTIP =
              "Prediction - How it works: It adds a predicted point to smoothing algorithm. It helps to preserve sharpness of movement, helps with small movements,\n"
            + "   Low values (~10-15ms) of smoothing latency can cause problems for cursor movement. It's very preferred to use at least 10-15ms of smoothing latency, 20-40 ms is even better and recommended.\n"
            + "   In some cases, cursor can even outdistance real position (similar to Wacom 6.3.95 drivers).\n"
            + "\n"
            + "Formula for prediction is:\n"
            + "   y(x) = 1/cosh((x-OffsetX)*Sharpness)*Strength+OffsetY\n"
            + " - Where x is pen speed. And y(x) is strength of prediction\n"
            + "\n"
            + "Strength : is max of peak of prediction. Useful values are from 0 to 2, or up to 3-4 depending on latency.\n"
            + "Sharpness : changes the width of the Strength.\n"
            + "Offset X : center of the prediction's peak. Useful values are from 0.5 up to 5-7, Increasing this value will shift the cursor speed up on bigger movements.\n"
            + "Offset Y : Moves the plot up/down (positive/negative values). Also defines the minimum amount of prediction.\n"
            + "\n"
            + "Example Settings:\n"
            + "   Simple+:\n"
            + "      Straight or Smooth preset for smoothing\n"
            + "      Strength 1-3 (for 5-50 ms respectively), Sharpness 1, OffsetX 0.8, OffsetY 0\n"
            + "\n"
            + "   Straight+:\n"
            + "      Straight preset for smoothing\n"
            + "      Strength 0.3, Sharpness 0.7, OffsetX 2, OffsetY 0.3\n"
            + "\n"
            + "   Fun:\n"
            + "      Smoothing: Latency 40ms, Strength 3, Multiplier 10, OffsetX 1, OffsetY 1\n"
            + "      Prediction: Strength 4, Sharpness 0.75, Offset 2.5, OffsetY 1";

        public override PipelinePosition Position => PipelinePosition.PreTransform;

        [SliderProperty("Latency", 0f, 1000f, 2f), DefaultPropertyValue(2f), ToolTip(LATENCY_TOOLTIP)]
        public float Latency
        {
            set => this.latency = Math.Clamp(value, 0, 1000);
            get => this.latency;
        }

        [Property("Antichatter Strength"), DefaultPropertyValue(3f), ToolTip(ANTICHATTER_TOOLTIP)]
        public float AntichatterStrength { set; get; }

        [Property("Antichatter Multiplier"), DefaultPropertyValue(1f), ToolTip(ANTICHATTER_TOOLTIP)]
        public float AntichatterMultiplier { set; get; }

        [Property("Antichatter Offset X"), ToolTip(ANTICHATTER_TOOLTIP)]
        public float AntichatterOffsetX { set; get; }

        [Property("Antichatter Offset Y"), DefaultPropertyValue(1f), ToolTip(ANTICHATTER_TOOLTIP)]
        public float AntichatterOffsetY { set; get; }

        [BooleanProperty("Prediction", ""), ToolTip(PREDICTION_TOOLTIP)]
        public bool PredictionEnabled { set; get; }

        [Property("Prediction Strength"), DefaultPropertyValue(1.1f), ToolTip(PREDICTION_TOOLTIP)]
        public float PredictionStrength { set; get; }

        [Property("Prediction Sharpness"), DefaultPropertyValue(1f), ToolTip(PREDICTION_TOOLTIP)]
        public float PredictionSharpness { set; get; }

        [Property("Prediction Offset X"), DefaultPropertyValue(3f), ToolTip(PREDICTION_TOOLTIP)]
        public float PredictionOffsetX { set; get; }

        [Property("Prediction Offset Y"), DefaultPropertyValue(0.3f), ToolTip(PREDICTION_TOOLTIP)]
        public float PredictionOffsetY { set; get; }

        private const float THRESHOLD = 0.9f;
        private bool isReady;
        private float timerInterval => 1000 / Frequency;
        private float latency = 2.0f;
        private float weight;
        private Vector2 position;
        private uint pressure;
        private Vector2 prevTargetPos, targetPos, calcTarget;

        protected override void ConsumeState()
        {
            if (State is ITabletReport report)
            {
                this.targetPos = report.Position * MillimeterScale;
                this.pressure = report.Pressure;

                if (PredictionEnabled)
                {
                    // Calculate predicted position onNewPacket
                    if (this.prevTargetPos.X != this.targetPos.X || this.prevTargetPos.Y != this.targetPos.Y)
                    {
                        // Calculate distance between last 2 packets and prediction
                        var delta = this.targetPos - this.prevTargetPos;
                        var distance = Vector2.Distance(this.prevTargetPos, this.targetPos);
                        var predictionModifier = 1 / MathF.Cosh((distance - PredictionOffsetX) * PredictionSharpness) * PredictionStrength + PredictionOffsetY;

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
            }
            else
            {
                OnEmit();
            }
        }

        protected override void UpdateState()
        {
            if (State is ITabletReport report && PenIsInRange())
            {
                report.Position = Filter(calcTarget) / MillimeterScale;
                report.Pressure = this.pressure;
                State = report;

                OnEmit();
            }
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
            var weightModifier = (float)(MathF.Pow(distance + AntichatterOffsetX, AntichatterStrength * -1) * AntichatterMultiplier);

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
    }
}
