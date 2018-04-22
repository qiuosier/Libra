using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    public class PdfModel
    {
        private PdfModelMS msPdf;
        private PdfModelSF sfPdf;
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

        public double ScaleRatio()
        {
            return sfPdf.ScaleRatio(msPdf.PdfDoc);
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
            // Load the file to Microsoft PDF document model
            // The Microsoft model is used to render the PDF pages.
            pdfModel.msPdf = await PdfModelMS.LoadFromFile(pdfStorageFile);
            // Return null if failed to load the file to Microsoft model
            if (pdfModel.msPdf == null) return null;
            // Load the file to Syncfusion PDF document model
            // The Syncfusion model is used to save ink annotations.
            if (pdfModel.msPdf.isPasswordProtected) 
            {
                pdfModel.sfPdf = await PdfModelSF.LoadFromFile(pdfStorageFile, pdfModel.msPdf.Password);
            }
            else pdfModel.sfPdf = await PdfModelSF.LoadFromFile(pdfStorageFile);
            // Return null if failed to load the file to Syncfusion model
            if (pdfModel.sfPdf == null) return null;
            return pdfModel;
        }

        public async Task<bool> SaveInkingToPdf(InkingManager inkManager)
        {
            bool status = await sfPdf.SaveInkingToPdf(inkManager, msPdf.PdfDoc);
            return status;
        }

        public async Task ReloadFile()
        {
            msPdf = await PdfModelMS.LoadFromFile(pdfFile);
        }
    }
}
