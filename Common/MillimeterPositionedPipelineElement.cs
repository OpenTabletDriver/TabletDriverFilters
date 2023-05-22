using System;
using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Attributes;
using OpenTabletDriver.Output;
using OpenTabletDriver.Tablet;

namespace TabletDriverFilters
{
    public abstract class MillimeterPositionedPipelineElement : IDevicePipelineElement
    {
        protected Vector2 MillimeterScale;

        public abstract PipelinePosition Position { get; }
        public abstract event Action<IDeviceReport> Emit;
        public abstract void Consume(IDeviceReport value);

        protected MillimeterPositionedPipelineElement(InputDevice inputDevice)
        {
            var digitizer = inputDevice.Configuration.Specifications.Digitizer;
            MillimeterScale = new Vector2
            {
                X = digitizer.Width / digitizer.MaxX,
                Y = digitizer.Height / digitizer.MaxY
            };
        }
    }
}
