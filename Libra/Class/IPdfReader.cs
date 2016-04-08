using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    interface IPdfReader
    {
        PdfDocument PdfDoc { get; }
        int PageCount();
        Size PageSize(int pageNumeber);
        Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth);
        Task ExportPageImage(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile);
    }
}
