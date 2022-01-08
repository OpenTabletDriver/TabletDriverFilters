using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using TabletDriverFilters;
using TabletDriverFilters.Hawku.Utility;

namespace OpenTabletDriver.Plugin
{
    [PluginName("Hawku Noise Reduction")]
    public class NoiseReduction : MillimeterPositionedPipelineElement
    {
        private const string NOISEREDUCTION_TOOLTIP =
              "Noise Reduction Filter:\n"
            + "   WARNING! This filter will cause more latency on smaller tablet areas(<20 mm), so consider using a larger area to increase the performance.\n"
            + "\n"
            + "Buffer:\n"
            + " - Buffer value is how many of the last pen positions will be stored in the buffer.\n"
            + " - Lower buffer value means lower latency, but lower noise reduction.\n"
            + " - At 133 RPS, the buffer size of 10 means a maximum latency of 75 milliseconds.\n"
            + "\n"
            + "Threshold:\n"
            + " - Threshold value sets the movement distance threshold per pen position report.\n"
            + " - The amount of noise reduction will be at it's maximum if the pen movement is shorter than the threshold value.\n"
            + " - Noise reduction and latency will be almost zero if the pen position movement is double the distance of the threshold value.\n"
            + " - At 133 RPS, a threshold value of 0.5 mm means for speeds of ~66.5 mm/s noise reduction and latency will be applied but for ~133 mm/s the noise reduction and latency will be near zero.\n"
            + "\n"
            + "Recommendations:\n"
            + "   Samples = 5 - 20, Threshold = 0.2 - 1.0 mm.";

        [Property("Buffer"), DefaultPropertyValue(10), ToolTip(NOISEREDUCTION_TOOLTIP)]
        public int Samples
        {
            set
            {
                this.samples = Math.Clamp(value, 0, 20);
                this.buffer = new RingBuffer<Vector2>(this.samples);
            }
            get => this.samples;
        }

        [Property("Distance Threshold"), Unit("mm"), DefaultPropertyValue(0.5f), ToolTip(NOISEREDUCTION_TOOLTIP)]
        public float DistanceThreshold
        {
            set
            {
                this.distThreshold = Math.Clamp(value, 0, 10);
                distanceMax = this.distThreshold * 2;
            }
            get => this.distThreshold;
        }

        public override PipelinePosition Position => PipelinePosition.PreTransform;

        private RingBuffer<Vector2> buffer;
        private float distThreshold, distanceMax;
        private const float minimumDistance = 0.001f;
        private int samples;
        private Vector2 outputPosition;

        public override event Action<IDeviceReport> Emit;

        public override void Consume(IDeviceReport value)
        {
            if (value is ITabletReport report)
            {
                report.Position = Filter(report.Position);
                value = report;
            }

            Emit?.Invoke(value);
        }

        public Vector2 Filter(Vector2 point)
        {
            point *= MillimeterScale;
            this.buffer.Insert(point);

            if (!this.buffer.IsFilled)
            {
                this.outputPosition = point;
                return this.outputPosition / MillimeterScale;
            }

            // Calculate geometric median from the buffer positions
            this.outputPosition = GetGeometricMedianVector();

            // Distance between latest position and ring buffer
            var distance = Vector2.Distance(point, this.outputPosition);

            // Distance larger than threshold -> modify the ring buffer
            if (distance > DistanceThreshold)
            {
                // Ratio between current distance and maximum distance
                float distanceRatio;

                // Distance ratio should be between 0.0 and 1.0
                // 0.0 -> distance == distanceThreshold
                // 1.0 -> distance == distanceMaximum
                distanceRatio = (distance - DistanceThreshold) / (distanceMax - DistanceThreshold);

                if (distanceRatio >= 1f)
                {
                    // Distance larger than maximum -> fill buffer with the latest target position
                    this.buffer.Clear(point);
                    this.outputPosition = point;
                }
                else
                {
                    // Move buffer positions and current position towards the latest target using linear interpolation
                    // Amount of movement is the distance ratio between threshold and maximum
                    for (int i = 0; i < buffer.Size; i++)
                    {
                        buffer[i] += (point - buffer[i]) * distanceRatio;
                    }

                    this.outputPosition += (point - this.outputPosition) * distanceRatio;
                    return this.outputPosition / MillimeterScale;
                }
            }
            return this.outputPosition / MillimeterScale;
        }

        private Vector2 GetGeometricMedianVector()
        {
            var next = new Vector2();
            float denominator, weight;

            // Calculate the starting position
            GetAverageVector(out var candidate);

            denominator = 0;

            // Loop through the buffer and calculate a denominator.
            foreach (var bufferPoint in buffer)
            {
                var distance = Vector2.Distance(candidate, bufferPoint);

                if (distance > minimumDistance)
                    denominator += 1f / distance;
                else
                    denominator += 1f / minimumDistance;
            }

            // Loop through the buffer and calculate a weighted average
            foreach (var bufferPoint in buffer)
            {
                var distance = Vector2.Distance(candidate, bufferPoint);

                if (distance > minimumDistance)
                    weight = 1f / distance;
                else
                    weight = 1f / minimumDistance;

                next += bufferPoint * weight / denominator;
            }

            return next;
        }

        private void GetAverageVector(out Vector2 point)
        {
            point = new Vector2();

            foreach (var bufferPoint in buffer)
                point += bufferPoint;

            point /= buffer.Size;
        }
    }
}
