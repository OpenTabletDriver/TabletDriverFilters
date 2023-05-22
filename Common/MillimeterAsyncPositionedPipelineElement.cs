using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Output;

namespace TabletDriverFilters
{
    public abstract class MillimeterAsyncPositionedPipelineElement : AsyncDevicePipelineElement
    {
        protected readonly Vector2 MillimeterScale;

        protected MillimeterAsyncPositionedPipelineElement(InputDevice inputDevice, ITimer scheduler) : base(scheduler)
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
