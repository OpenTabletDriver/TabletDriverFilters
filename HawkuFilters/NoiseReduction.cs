using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using TabletDriverFilters.Hawku.Utility;

namespace OpenTabletDriver.Plugin
{
    [PluginName("TabletDriver Noise Reduction")]
    public class TabletDriverNoiseReduction : IFilter
    {
        public TabletDriverNoiseReduction()
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

        [Property("Distance Threshold"), Unit("mm")]
        public float DistThreshold
        {
            set
            {
                this.distThreshold = Math.Clamp(value, 0, 10);
                distMax = value * 2;
            }
            get => this.distThreshold;
        }

        public FilterStage FilterStage => FilterStage.PreTranspose;

        private RingBuffer<Vector2> buffer;
        private float distThreshold, distMax;
        private const int iterations = 10;
        private int samples;
        private Vector2 lastPoint, mmScale;

        public Vector2 Filter(Vector2 point)
        {
            point *= mmScale;
            this.buffer.Insert(point);

            if (!this.buffer.IsFilled)
            {
                return SetOutput(point) / mmScale;
            }

            // Calculate geometric median from the buffer positions
            this.lastPoint = GetGeometricMedianVector(this.lastPoint);

            // Distance between latest position and ring buffer
            var distance = Vector2.Distance(point, this.lastPoint);

            // Distance larger than threshold -> modify the ring buffer
            if (distance > DistThreshold)
            {
                // Ratio between current distance and maximum distance
                float distanceRatio;

                // Distance ratio should be between 0.0 and 1.0
                // 0.0 -> distance == distanceThreshold
                // 1.0 -> distance == distanceMaximum
                distanceRatio = (distance - DistThreshold) / (this.distMax - DistThreshold);

                if (distanceRatio >= 1f)
                {
                    // Distance larger than maximum -> fill buffer with the latest target position
                    var bufCount = this.buffer.Size;
                    this.buffer.Clear();
                    for (int i = 0; i < bufCount; i++)
                        this.buffer.Insert(point);
                    return SetOutput(point) / mmScale;
                }
                else
                {
                    // Move buffer positions and current position towards the latest target using linear interpolation
                    // Amount of movement is the distance ratio between threshold and maximum

                    for (int i = 0; i < buffer.Size; i++)
                    {
                        buffer[i] += (point - buffer[i]) * distanceRatio;
                    }

                    this.lastPoint += (point - this.lastPoint) * distanceRatio;
                    return this.lastPoint / mmScale;
                }
            }
            return SetOutput(point) / mmScale;
        }

        private Vector2 SetOutput(Vector2 point)
        {
            this.lastPoint = point;
            return point;
        }

        private Vector2 GetGeometricMedianVector(Vector2 point)
        {
            var candidate = new Vector2();
            var next = new Vector2();
            var minimumDistance = 0.001f;

            float denominator, weight, distance;

            // Calculate the starting position
            if (!GetAverageVector(ref candidate))
                return this.lastPoint;

            // Iterate
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                denominator = 0;

                // Loop through the buffer and calculate a denominator.
                foreach (var bufferPoint in buffer)
                {
                    distance = Vector2.Distance(candidate, bufferPoint);

                    if (distance > minimumDistance)
                        denominator += 1f / distance;
                    else
                        denominator += 1f / minimumDistance;
                }

                // Reset the next vector
                next.X = 0;
                next.Y = 0;

                // Loop through the buffer and calculate a weighted average
                foreach (var bufferPoint in buffer)
                {
                    distance = Vector2.Distance(candidate, bufferPoint);

                    if (distance > minimumDistance)
                        weight = 1f / distance;
                    else
                        weight = 1f / minimumDistance;

                    next.X += (float)(bufferPoint.X * weight / denominator);
                    next.Y += (float)(bufferPoint.Y * weight / denominator);
                }

                // Set the new candidate vector
                candidate.X = next.X;
                candidate.Y = next.Y;
            }

            // Set output
            point.X = candidate.X;
            point.Y = candidate.Y;
            return point;
        }

        private bool GetAverageVector(ref Vector2 point)
        {
            if (buffer.Size == 0)
                return false;

            point.X = 0;
            point.Y = 0;

            foreach (var bufferPoint in buffer)
            {
                point.X += bufferPoint.X;
                point.Y += bufferPoint.Y;
            }

            point.X /= buffer.Size;
            point.Y /= buffer.Size;
            return true;
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