using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Plugin
{
    [PluginName("TabletDriver Noise Reduction")]
    public class TabletDriverNoiseReduction : IFilter
    {
        private LinkedList<Vector2> _buffer = new LinkedList<Vector2>();
        private float _distThreshold, _distMax;
        private const int _iterations = 10;
        private int _samples = 10;
        private Vector2 _lastPoint;

        public Vector2 Filter(Vector2 point)
        {
            SetTarget(point);

            if (_buffer.Count <= 1)
            {
                return SetOutput(point);
            }

            // Calculate geometric median from the buffer positions
            GetGeometricMedianVector(ref _lastPoint);

            // Distance between latest position and ring buffer
            var distance = Vector2.Distance(point, _lastPoint);

            // Distance larger than threshold -> modify the ring buffer
            if (distance > DistThreshold)
            {
                // Ratio between current distance and maximum distance
                double distanceRatio;

                // Distance ratio should be between 0.0 and 1.0
                // 0.0 -> distance == distanceThreshold
                // 1.0 -> distance == distanceMaximum
                distanceRatio = (distance - DistThreshold) / (_distMax - DistThreshold);

                if (distanceRatio >= 1f)
                {
                    // Distance larger than maximum -> fill buffer with the latest target position
                    var bufCount = _buffer.Count;
                    _buffer.Clear();
                    for (int i = 0; i < bufCount; i++)
                        _buffer.AddLast(point);
                    return SetOutput(point);
                }
                else
                {
                    // Move buffer positions and current position towards the latest target using linear interpolation
                    // Amount of movement is the distance ratio between threshold and maximum

                    var bufNode = _buffer.First;

                    while (bufNode != null)
                    {
                        var bufPoint = bufNode.Value;
                        bufPoint.X += (float)((point.X - bufPoint.X) * distanceRatio);
                        bufPoint.Y += (float)((point.Y - bufPoint.Y) * distanceRatio);
                        bufNode.Value = bufPoint;
                        bufNode = bufNode.Next;
                    }

                    _lastPoint.X += (float)((point.X - _lastPoint.X) * distanceRatio);
                    _lastPoint.Y += (float)((point.Y - _lastPoint.Y) * distanceRatio);

                    return _lastPoint;
                }
            }
            return SetOutput(point);
        }

        private void SetTarget(Vector2 point)
        {
            _buffer.AddLast(point);
            while (_buffer.Count > Samples)
                _buffer.RemoveFirst();
        }

        private Vector2 SetOutput(Vector2 point)
        {
            _lastPoint = point;
            return point;
        }

        private Vector2 GetGeometricMedianVector(ref Vector2 point)
        {
            var candidate = new Vector2();
            var next = new Vector2();
            var minimumDistance = 0.001;

            double denominator, weight, distance;

            // Calculate the starting position
            if (!GetAverageVector(ref candidate))
                return _lastPoint;

            // Iterate
            for (int iteration = 0; iteration < _iterations; iteration++)
            {
                denominator = 0;

                // Loop through the buffer and calculate a denominator.
                foreach (var bufferPoint in _buffer)
                {
                    distance = Vector2.Distance(candidate, bufferPoint);

                    if (distance > minimumDistance)
                        denominator += 1.0 / distance;
                    else
                        denominator += 1.0 / minimumDistance;
                }

                // Reset the next vector
                next.X = 0;
                next.Y = 0;

                // Loop through the buffer and calculate a weighted average
                foreach (var bufferPoint in _buffer)
                {
                    distance = Vector2.Distance(candidate, bufferPoint);

                    if (distance > minimumDistance)
                        weight = 1.0 / distance;
                    else
                        weight = 1.0 / minimumDistance;

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
            if (_buffer.Count == 0)
                return false;

            point.X = 0;
            point.Y = 0;

            foreach (var bufferPoint in _buffer)
            {
                point.X += bufferPoint.X;
                point.Y += bufferPoint.Y;
            }
            
            point.X /= _buffer.Count;
            point.Y /= _buffer.Count;
            return true;
        }

        [Property("Buffer")]
        public int Samples
        { 
            set
            {
                _samples = Math.Clamp(value, 0, 20);
            }
            get => _samples;
        }

        [UnitProperty("Distance Threshold", "px")]
        public float DistThreshold
        {
            set
            {
                _distThreshold = Math.Clamp(value, 0, 10);
                _distMax = value * 2;
            }
            get => _distThreshold;
        }

        public FilterStage FilterStage => FilterStage.PostTranspose;
    }
}