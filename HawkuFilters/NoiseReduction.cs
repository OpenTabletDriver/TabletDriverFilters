using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using TabletDriverFilters.Hawku.Utility;

namespace OpenTabletDriver.Plugin
{
    [PluginName("Hawku Noise Reduction")]
    public class NoiseReduction : IFilter
    {
        public NoiseReduction()
        {
            GetMMScale();
        }

        [Property("Buffer"), DefaultPropertyValue(10)]
        public int Samples
        {
            set
            {
                this.samples = Math.Clamp(value, 0, 20);
                this.buffer = new RingBuffer<Vector2>(this.samples);
            }
            get => this.samples;
        }

        [Property("Distance Threshold"), Unit("mm"), DefaultPropertyValue(0.5f)]
        public float DistanceThreshold
        {
            set
            {
                this.distThreshold = Math.Clamp(value, 0, 10);
                distanceMax = this.distThreshold * 2;
            }
            get => this.distThreshold;
        }

        public FilterStage FilterStage => FilterStage.PreTranspose;

        private RingBuffer<Vector2> buffer;
        private float distThreshold, distanceMax;
        private const float minimumDistance = 0.001f;
        private int samples;
        private Vector2 outputPosition, mmScale;

        public Vector2 Filter(Vector2 point)
        {
            point *= mmScale;
            this.buffer.Insert(point);

            if (!this.buffer.IsFilled)
            {
                this.outputPosition = point;
                return this.outputPosition / mmScale;
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
                    return this.outputPosition / mmScale;
                }
            }
            return this.outputPosition / mmScale;
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