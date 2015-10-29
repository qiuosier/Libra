using System;
using Windows.UI;
using Windows.UI.Core;

namespace Libra
{
    public class InkingSetting
    {
        public InkingSetting()
        { }

        public InkingSetting(double width)
        {
            penSize = 1;
            highlighterSize = 12;
            penColor = Colors.Red;
            pageWidth = width;
            drawingDevice = Windows.UI.Core.CoreInputDeviceTypes.Mouse | Windows.UI.Core.CoreInputDeviceTypes.Pen;
        }

        public int penSize;
        public int highlighterSize;
        public Color penColor;
        public double pageWidth;
        public CoreInputDeviceTypes drawingDevice;
    }
}
