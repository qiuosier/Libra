using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    public class PageDetail
    {
        private const int DEFAULT_GRID_EDGE_SIZE = 300;
        private BitmapImage _pageImage;
        public BitmapImage PageImage
        {
            get { return this._pageImage; }
            set
            {
                this._pageImage = value;
            }
        }

        public int PixelHeight { get { return PageImage == null ? DEFAULT_GRID_EDGE_SIZE : PageImage.PixelHeight; } }

        public int PixelWidth { get { return PageImage == null ? DEFAULT_GRID_EDGE_SIZE : PageImage.PixelWidth; } }

        private int _pageNumber;
        public int PageNumber { get { return this._pageNumber; } }

        public PageDetail(int pageNumber)
        {
            this._pageNumber = pageNumber;
        }

        public PageDetail(int pageNumber, BitmapImage bitmap)
        {
            this.PageImage = bitmap;
            this._pageNumber = pageNumber;
        }
    }
}
