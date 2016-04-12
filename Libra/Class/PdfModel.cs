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
        private StorageFile pdfFile;

        private PdfModel(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
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
            PdfModel pdfModel = new PdfModel(pdfStorageFile);
            pdfModel.msPdf = await MSPdfModel.LoadFromFile(pdfStorageFile);
            pdfModel.sfPdf = await SFPdfModel.LoadFromFile(pdfStorageFile);
            return pdfModel;
        }

        public async Task<bool> SaveInkingToPdf(InkingManager inkManager)
        {
            bool status = await sfPdf.SaveInkingToPdf(inkManager, msPdf.PdfDoc);
            return status;
        }

        public async Task ReloadFile()
        {
            msPdf = await MSPdfModel.LoadFromFile(pdfFile);
        }
    }
}
