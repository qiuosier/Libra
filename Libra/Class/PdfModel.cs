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
    public class PdfModel : IPdfReader
    {
        private MSPdfModel msPdf;
        private SFPdfModel sfPdf;

        private PdfModel()
        {

        }

        public PdfDocument PdfDoc
        {
            get
            {
                return msPdf.PdfDoc;
            }
        }

        public Task ExportPageImage(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile)
        {
            return msPdf.ExportPageImage(pageNumber, inkCanvas, saveFile);
        }

        public int PageCount()
        {
            return msPdf.PageCount();
        }

        public Size PageSize(int pageNumeber)
        {
            return msPdf.PageSize(pageNumeber);
        }

        public Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            return msPdf.RenderPageImage(pageNumber, renderWidth);
        }

        public static async Task<PdfModel> LoadFromFile(StorageFile pdfStorageFile)
        {
            PdfModel pdfModel = new PdfModel();
            pdfModel.msPdf = await MSPdfModel.LoadFromFile(pdfStorageFile);
            double z = pdfModel.msPdf.PdfDoc.GetPage(0).PreferredZoom;
            int r = (int)pdfModel.msPdf.PdfDoc.GetPage(0).Rotation;
            double tH = pdfModel.msPdf.PdfDoc.GetPage(0).Dimensions.TrimBox.Height;
            double tW = pdfModel.msPdf.PdfDoc.GetPage(0).Dimensions.TrimBox.Width;
            double mH = pdfModel.msPdf.PdfDoc.GetPage(0).Dimensions.MediaBox.Height;
            double mW = pdfModel.msPdf.PdfDoc.GetPage(0).Dimensions.MediaBox.Width;
            double tT = pdfModel.msPdf.PdfDoc.GetPage(0).Dimensions.TrimBox.Top;
            double tL = pdfModel.msPdf.PdfDoc.GetPage(0).Dimensions.TrimBox.Left;
            pdfModel.sfPdf = await SFPdfModel.LoadFromFile(pdfStorageFile);
            
            return pdfModel;
        }

        public async Task<bool> SaveInkingToPdf(InkingManager inkManager)
        {
            return await sfPdf.SaveInkingToPdf(inkManager, msPdf.PdfDoc);
        }
    }
}
