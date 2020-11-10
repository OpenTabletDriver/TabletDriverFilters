using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletDriverFilters.Devocub
{
    using static MathF;

    [PluginName("TabletDriver AntiChatter Filter")]
    public class AntiChatter : IFilter
    {
        private Vector2 _lastPos;
        private float _timerInterval;
        private const float _threshold = 0.9f;
        private float _latency = 2.0f;

        public Vector2 Filter(Vector2 point)
        {
            Vector2 calcTarget = new Vector2();
            float deltaX, deltaY, distance, weightModifier, predictionModifier;

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

            float stepCount = Latency / TimerInterval;
            float target = 1 - _threshold;
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
            _lastPos.X += (float)(deltaX * weightModifier);
            _lastPos.Y += (float)(deltaY * weightModifier);

            return _lastPos;
        }

        public FilterStage FilterStage => FilterStage.PostTranspose;

        [SliderProperty("Latency", 0f, 5f, 2f)]
        public float Latency
        {
            set => _latency = Math.Clamp(value, 0, 1000);
            get => _latency;
        }

        [Property("Timer Interval"), Unit("hz")]
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