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
        private const int DEFAULT_PAGE_HEIGHT = 110;
        private const int DEFAULT_PAGE_WIDTH = 85;

        private BitmapImage _pageImage;
        public BitmapImage PageImage
        {
            get { return this._pageImage; }
            set
            {
                this._pageImage = value;
            }
        }

        private double pageHeight;
        private double pageWidth;

        public int PixelHeight { get { return PageImage == null ? (int)pageHeight : PageImage.PixelHeight; } }

        public int PixelWidth { get { return PageImage == null ? (int)pageWidth : PageImage.PixelWidth; } }

        private int _pageNumber;
        public int PageNumber { get { return this._pageNumber; } }

        public PageDetail(int pageNumber)
        {
            this._pageNumber = pageNumber;
            this.pageHeight = DEFAULT_PAGE_HEIGHT;
            this.pageWidth = DEFAULT_PAGE_WIDTH;
        }

        public PageDetail(int pageNumber, BitmapImage bitmap)
        {
            this.PageImage = bitmap;
            this._pageNumber = pageNumber;
        }

        public PageDetail(int pageNumber, double height, double width)
        {
            this._pageNumber = pageNumber;
            this.pageHeight = height;
            this.pageWidth = width;
        }
    }
}
