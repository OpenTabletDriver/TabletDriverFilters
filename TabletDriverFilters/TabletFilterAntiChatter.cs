using System;
using TabletDriverPlugin;
using TabletDriverPlugin.Attributes;
using TabletDriverPlugin.Tablet;

namespace TabletDriverFilters
{
    using static Math;

    [PluginName("TabletDriver AntiChatterFilter")]
    public class TabletFilterAntiChatter : IFilter
    {
        private Point _lastPos;
        private float _timerInterval;
        private const float _threshold = 0.63f;
        private float _latency;

        public Point Filter(Point point)
        {
            Point calcTarget = new Point();
            double deltaX, deltaY, distance, weightModifier, predictionModifier;

            if (_lastPos == null)
            {
                _lastPos = point;
                return point;
            }

            if (PredictionEnabled)
            {
                // Calculate predicted position onNewPacket
                if (_lastPos.X != point.X || _lastPos.Y != point.Y)
                {
                    // Calculate distance between last 2 packets and prediction
                    deltaX = point.X - _lastPos.X;
                    deltaY = point.Y - _lastPos.Y;
                    distance = Sqrt(deltaX * deltaX + deltaY * deltaY);
                    predictionModifier = 1 / Cosh((distance - PredictionOffsetX) * PredictionSharpness) * PredictionStrength + PredictionOffsetY;

                    // Apply prediction
                    deltaX *= predictionModifier;
                    deltaY *= predictionModifier;

                    // Update predicted position
                    calcTarget.X = (float)(point.X + deltaX);
                    calcTarget.Y = (float)(point.Y + deltaY);

                    // Update old position for further prediction
                    _lastPos.X = point.X;
                    _lastPos.Y = point.Y;
                }
            }
            else
            {
                calcTarget.X = point.X;
                calcTarget.Y = point.Y;
            }

            deltaX = calcTarget.X - _lastPos.X;
            deltaY = calcTarget.Y - _lastPos.Y;
            distance = Sqrt(deltaX * deltaX + deltaY * deltaY);

            double stepCount = Latency / TimerInterval;
            double target = 1 - _threshold;
            double weight = 1.0 - (1.0 / Pow(1.0 / target, 1.0 / stepCount));

            // Devocub smoothing
            // Increase weight of filter in {formula} times
            weightModifier = (float)(Pow(distance + AntichatterOffsetX, AntichatterStrength * -1) * AntichatterMultiplier);

            // Limit minimum
            if (weightModifier + AntichatterOffsetY < 0)
                weightModifier = 0;
            else
                weightModifier += AntichatterOffsetY;

            weightModifier = weight / weightModifier;
            weightModifier = Clamp(weightModifier, 0, 1);
            _lastPos.X += (float)(deltaX * weightModifier);
            _lastPos.Y += (float)(deltaY * weightModifier);

            // OTDPlugin 0.3.2 feature
            // TabletDriverPlugin.Log.Write("Antichatter", $"orig: ({point}) new: ({_lastPos}) dist: ({point.DistanceFrom(_lastPos)})", LogLevel.Debug);
            return _lastPos;
        }

        public FilterStage FilterStage => FilterStage.PostTranspose;

        [SliderProperty("Latency", 0f, 5f, 2f)]
        public float Latency
        {
            set => _latency = Clamp(value, 0, 1000);
            get => _latency;
        }

        [UnitProperty("Timer Interval", "hz")]
        public float TimerInterval
        {
            set => _timerInterval = 1000f / value;
            get => _timerInterval;
        }

        [Property("Antichatter Strength")]
        public float AntichatterStrength { set; get; } = 3;

        [Property("Antichatter Multiplier")]
        public float AntichatterMultiplier { set; get; } = 1;

        [Property("Antichatter Offset X")]
        public float AntichatterOffsetX { set; get; }

        [Property("Antichatter Offset Y")]
        public float AntichatterOffsetY { set; get; }

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