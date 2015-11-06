using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;

namespace Libra
{
    /// <summary>
    /// Contains user preferences for inking.
    /// </summary>
    public class InkingPreference
    {
        public InkingPreference()
        { 
            penSize = 1;
            highlighterSize = 12;
            penColor = Colors.Red;
            highlighterColor = Colors.Yellow;
            drawingDevice = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
        }

        public int penSize;
        public int highlighterSize;
        public Color penColor;
        public Color highlighterColor;
        public CoreInputDeviceTypes drawingDevice;
    }
}
