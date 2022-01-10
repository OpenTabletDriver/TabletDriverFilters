using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletDriverFilters
{
    public abstract class MillimeterPositionedPipelineElement : IPositionedPipelineElement<IDeviceReport>
    {
        protected Vector2 MillimeterScale;

        [TabletReference]
        public TabletReference TabletReference { set => HandleTabletReferenceInternal(value); }
        public abstract PipelinePosition Position { get; }
        public abstract event Action<IDeviceReport> Emit;
        public abstract void Consume(IDeviceReport value);

        private void HandleTabletReferenceInternal(TabletReference tabletReference)
        {
            var digitizer = tabletReference.Properties.Specifications.Digitizer;
            MillimeterScale = new Vector2
            {
                X = digitizer.Width / digitizer.MaxX,
                Y = digitizer.Height / digitizer.MaxY
            };
            HandleTabletReference(tabletReference);
        }

        protected virtual void HandleTabletReference(TabletReference tabletReference)
        {
            // Override when needed
        }
    }
}
