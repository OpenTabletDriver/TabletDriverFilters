using System;
using System.Numerics;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet.Interpolator;
using OpenTabletDriver.Plugin.Timers;

namespace TabletDriverFilters.Devocub
{
    using static MathF;

    [PluginName("Devocub Antichatter")]
    public class Antichatter : Interpolator
    {
        private const string Latency_Tooltip = "Cursor position smoothing filter\n - Smoothing filter adds latency to the input, so don't enable it if you want to have the lowest possible input lag.\n - On Wacom tablets you can use latency value between 15 and 25 to have a similar smoothing as in the Wacom drivers.\n - You can test out different filter values, but the recommended maximum for osu! is around 50 milliseconds.\n - Filter latency values lower than 4 milliseconds aren't recommended. Its better to disable the smoothing filter.\n - You don't have to change the filter frequency, but you can use the highest frequency your computer can run without performance problems.";
        private const string Antichatter_Tooltip = "Antichatter is meant to prevent cursor chattering/rattling/shaking/trembling when the pen doesn't move.\nAntichatter in its primary form is useful for tablets which don't have any hardware smoothing.\nAntichatter uses smoothing. Latency and Frequency values do have an effect on antichatter settings.\n\nFormula for smoothing is:\ny(x) = (x + OffsetX)^(Strength*-1)*Multiplier+OffsetY\nWhere x is pen speed. And y(x) is the smoothing value. Slower speed = more smoothing. Faster speed = less smoothing.\n\nStrength : Is strength, useful values are from 1 up to 10. Higher values make smoothing sharper, lower are smoother.\nMultiplier : Zooms in and zooms out the plot. Useful values are from 1 up to 1000. Makes smoothing softer. Default value is 1, which causes no change.\nOffset X : Moves the plot to the right. Negative values move the plot to the left. Higher values make smoothing weaker,\nlower values stronger and activate stronger smoothing earlier in terms of cursor speed). Useful values are from -1 to 2. Default values is 0.\nOffset Y : Moves the plot up. Useful values are from roughly -1 up to 10. If the Y value of smoothing is near 0 for any given point then it provides almost raw data with lowest delay.\nIf value is near 1 then it's usual smoothing, also it defines minimal amount of smoothing. OffsetY 10 will make smoothing x10 (and latency).\nOffsetY 0.5 will make smoothing roughly twice as weak (and latency will be roughly half), 0.3 roughly one third weaker, etc. The default value is 1.\n\nExample Settings:\nSimple: Latency 5-50 ms, Strength 2-3, Multiplier 1, OffsetX 0, OffsetY 1.\n\nStraight: Latency 20-40ms, Strength 20, Multiplier 1, OffsetX 0.7, OffsetY 0.6. This preset isn’t good for high hovering.\n\nSmooth: Latency ~10 ms, Strength 3, Multiplier 100, OffsetX 1.5, OffsetY 1.\nChange OffsetX between 0-2 to switch between stickiness and smooth.\nIncrease Strength to 4-10 to get harper. Decrease Strength to 1-2 to get more smoothing.\n\nLow latency: Set Offset Y to 0 (and potentially set Latency to 1-10 ms. However, with some settings this can break smoothing, usually OffsetY 0 is enough to being able to go to lowest latency).";
        private const string Prediction_Tooltip = "Prediction - How it works: It adds a predicted point to smoothing algorithm. It helps to preserve sharpness of movement, help with small movements,\nLow values (~10-15ms) of smoothing latency can cause problems for cursor movement. It's very preferred to use at least 10-15ms of smoothing latency, 20-40 ms is even better and recommended.\nIn sum cursor can even outdistance real position (similar to Wacom 6.3.95 drivers).\n\nFormula for prediction is:\ny(x) = 1/cosh((x-OffsetX)*Sharpness)*Strength+OffsetY\nWhere x is pen speed. And y(x) is strength of prediction\n\nStrength : is max of peak of prediction. Useful values are from 0 to 2, or up to 3-4 depending on latency.\nSharpness : changes the width of the Strength.\nOffset X : center of the prediction's peak. Useful values are from 0.5 up to 5-7, Increasing this value will shift the cursor speed up on bigger movements.\nOffset Y : Moves the plot up/down (positive/negative values). Also defines the minimum amount of prediction.\n\nExample Settings:\nSimple+:\nStraight or Smooth preset for smoothing\nStrength 1-3 (for 5-50 ms respectively), Sharpness 1, OffsetX 0.8, OffsetY 0\n\nStraight+:\nStraight preset for smoothing\nStrength 0.3, Sharpness 0.7, OffsetX 2, OffsetY 0.3\n\nFun:\nSmoothing: Latency 40ms, Strength 3, Multiplier 10, OffsetX 1, OffsetY 1\nPrediction: Strength 4, Sharpness 0.75, Offset 2.5, OffsetY 1";

        public Antichatter(ITimer scheduler) : base(scheduler)
        {
            GetMMScale();
        }

        [SliderProperty("Latency", 0f, 1000f, 2f), DefaultPropertyValue(2f), ToolTip(Latency_Tooltip)]
        public float Latency
        {
            set => this.latency = Math.Clamp(value, 0, 1000);
            get => this.latency;
        }

        [Property("Antichatter Strength"), DefaultPropertyValue(3f), ToolTip(Antichatter_Tooltip)]
        public float AntichatterStrength { set; get; }

        [Property("Antichatter Multiplier"), DefaultPropertyValue(1f), ToolTip(Antichatter_Tooltip)]
        public float AntichatterMultiplier { set; get; }

        [Property("Antichatter Offset X"), ToolTip(Antichatter_Tooltip)]
        public float AntichatterOffsetX { set; get; }

        [Property("Antichatter Offset Y"), DefaultPropertyValue(1f), ToolTip(Antichatter_Tooltip)]
        public float AntichatterOffsetY { set; get; }

        [BooleanProperty("Prediction", ""), ToolTip(Prediction_Tooltip)]

        public bool PredictionEnabled { set; get; }
        [Property("Prediction Strength"), DefaultPropertyValue(1.1f), ToolTip(Prediction_Tooltip)]
        public float PredictionStrength { set; get; }

        [Property("Prediction Sharpness"), DefaultPropertyValue(1f), ToolTip(Prediction_Tooltip)]
        public float PredictionSharpness { set; get; }

        [Property("Prediction Offset X"), DefaultPropertyValue(3f), ToolTip(Prediction_Tooltip)]
        public float PredictionOffsetX { set; get; }

        [Property("Prediction Offset Y"), DefaultPropertyValue(0.3f), ToolTip(Prediction_Tooltip)]
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