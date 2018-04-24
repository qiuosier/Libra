using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class PdfModel
    {
        private PdfModelMS msPdf;
        private PdfModelSF sfPdf;
        private StorageFile pdfFile;
        private StorageFolder cacheFolder;
        private StorageFolder backupFolder;

        private const string CACHE_FOLDER = "Cache";
        private const string BACKUP_FOLDER = "Backup";

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

        public async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Load exiting annotations
            if (sfPdf.AnnotationCount(pageNumber) == 0)
                return await msPdf.RenderPageImage(pageNumber, renderWidth);
            else
            {
                StorageFile storageFile = await sfPdf.ExtractPageWithoutInking(pageNumber, cacheFolder);
                PdfModelMS pageDoc = await PdfModelMS.LoadFromFile(storageFile);
                return await pageDoc.RenderPageImage(1, renderWidth);
            }
        }

        public List<InkStroke> LoadInkAnnotations(int pageNumber)
        {
            return sfPdf.GetInkAnnotations(pageNumber);
        }

        public static async Task<PdfModel> LoadFromFile(StorageFile pdfStorageFile, StorageFolder dataFolder)
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
            
            // Create cache and backup folder
            pdfModel.cacheFolder = await dataFolder.CreateFolderAsync(CACHE_FOLDER, CreationCollisionOption.OpenIfExists);
            pdfModel.backupFolder = await dataFolder.CreateFolderAsync(BACKUP_FOLDER, CreationCollisionOption.OpenIfExists);
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
