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
        private BitmapImage _pageImage;
        public BitmapImage PageImage
        {
            get { return this._pageImage; }
            set
            {
                this._pageImage = value;
            }
        }

        public int PixelHeight { get { return this._pageImage.PixelHeight; } }

        public int PixelWidth { get { return this._pageImage.PixelWidth; } }

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
