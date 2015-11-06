using System;
using Windows.UI;
using Windows.UI.Core;

namespace Libra
{
    /// <summary>
    /// Contains the parameters for ink presenting.
    /// </summary>
    public class InkingProfile
    {
        public InkingProfile()
        { }

        public InkingProfile(double width)
        {
            _pageWidth = width;
        }

        private double _pageWidth;
        public double PageWidth { get { return _pageWidth; } }
    }
}
