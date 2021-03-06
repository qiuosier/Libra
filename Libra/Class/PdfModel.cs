﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Input.Inking;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using System.IO;

namespace Libra.Class
{
    public class PdfModel
    {
        private PdfModelMS msPdf;
        private PdfModelSF sfPdf;
        private StorageFile pdfFile;
        private StorageFile backupFile;
        private StorageFile sfFile;
        private StorageFolder backupFolder;

        private const string BACKUP_FOLDER = "Backup";

        public double ScaleRatio { get; private set; }

        /// <summary>
        /// Private contstructor. Use LoadFromFile() static method to initialize a new instance.
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        private PdfModel(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        /// <summary>
        /// Exports a PDF page to an image file.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="inkCanvas"></param>
        /// <param name="saveFile"></param>
        /// <returns></returns>
        public Task ExportPageImage(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile)
        {
            return msPdf.ExportPageImage(pageNumber, inkCanvas, saveFile);
        }

        /// <summary>
        /// The number of pages in the PDF document.
        /// </summary>
        public int PageCount
        {
            get
            {
                return msPdf.PageCount;
            }
        }

        /// <summary>
        /// Get's a PDF page's size
        /// </summary>
        /// <param name="pageNumeber">1-based page number</param>
        /// <returns></returns>
        public Size PageSize(int pageNumeber)
        {
            return msPdf.PageSize(pageNumeber);
        }
        
        /// <summary>
        /// Gets a MS PDF page from the PDF document.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        public Windows.Data.Pdf.PdfPage GetPage(int pageNumber)
        {
            return msPdf.GetPage(pageNumber);
        }

        /// <summary>
        /// Renders a page as image in the memory.
        /// </summary>
        /// <param name="pageNumber">1-based page number.</param>
        /// <param name="renderWidth">Width of the image.</param>
        /// <returns></returns>
        public async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Load exiting annotations
            if (sfPdf.AnnotationCount(pageNumber) == 0)
                return await msPdf.RenderPageImage(pageNumber, renderWidth);
            else
            {
                MemoryStream stream = sfPdf.ExtractPageWithoutInking(pageNumber);
                return await PdfModelMS.RenderFirstPageFromStream(stream.AsRandomAccessStream(), renderWidth);
            }
        }

        /// <summary>
        /// Loads the ink annotations in the PDF file.
        /// </summary>
        /// <param name="pageNumber">1-based page number.</param>
        /// <returns></returns>
        public List<InkStroke> LoadInFileInkAnnotations(int pageNumber)
        {
            List<PdfLoadedInkAnnotation> inkAnnotations = sfPdf.GetInkAnnotations(pageNumber);
            List<InkStroke> strokes = new List<InkStroke>();
            // Get page information from SF model
            PdfLoadedPage sfPage = sfPdf.GetPage(pageNumber);
            // Get page information from MS model
            Windows.Data.Pdf.PdfPage msPage = msPdf.GetPage(pageNumber);
            // Calculate page mapping
            PageMapping mapping = new PageMapping(msPage, sfPage);
            foreach (PdfLoadedInkAnnotation annotation in inkAnnotations)
            {
                strokes.Add(mapping.InkAnnotation2InkStroke(annotation));
            }
            return strokes;
        }

        /// <summary>
        /// Initialize a new PDFModel instance from a PDF file.
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        /// <param name="dataFolder"></param>
        /// <returns></returns>
        public static async Task<PdfModel> LoadFromFile(StorageFile pdfStorageFile, StorageFolder dataFolder)
        {
            PdfModel pdfModel = new PdfModel(pdfStorageFile);
            await pdfModel.InitializeComponents(dataFolder);
            if (pdfModel.msPdf == null || pdfModel.sfPdf == null) return null;
            return pdfModel;
        }

        /// <summary>
        /// Initializes the components of a new PDFModel instance.
        /// </summary>
        /// <param name="dataFolder">The folder storing the in-app user data.</param>
        /// <returns></returns>
        private async Task InitializeComponents(StorageFolder dataFolder)
        {
            // Create backup folder
            backupFolder = await dataFolder.CreateFolderAsync(BACKUP_FOLDER, CreationCollisionOption.OpenIfExists);
            // Delete existing backup copies
            foreach (StorageFile file in await backupFolder.GetFilesAsync())
            {
                try
                {
                    await file.DeleteAsync();
                }
                catch
                {

                }
            }

            // Create a backup copy
            backupFile = await pdfFile.CopyAsync(backupFolder, pdfFile.Name, NameCollisionOption.GenerateUniqueName);
            sfFile = await pdfFile.CopyAsync(backupFolder, "SF_" + pdfFile.Name, NameCollisionOption.GenerateUniqueName);
            // Load the file to Microsoft PDF document model
            // The Microsoft model is used to render the PDF pages.
            msPdf = await PdfModelMS.LoadFromFile(backupFile);
            // Return null if failed to load the file to Microsoft model
            if (msPdf == null) return;
            // Load the file to Syncfusion PDF document model
            // The Syncfusion model is used to save ink annotations.
            if (msPdf.IsPasswordProtected)
            {
                sfPdf = await PdfModelSF.LoadFromFile(sfFile, msPdf.Password);
            }
            else sfPdf = await PdfModelSF.LoadFromFile(sfFile);
            // Return null if failed to load the file to Syncfusion model
            if (sfPdf == null) return;

            ScaleRatio = sfPdf.GetPage(1).Size.Width / msPdf.GetPage(1).Dimensions.MediaBox.Width;
        }

        /// <summary> 
        /// Save the ink annotations into the pdf file. 
        /// </summary> 
        /// <param name="inkDictionary"></param> 
        /// <returns></returns> 
        /// <remarks> 
        /// The page size returned from Syncfusion pdf is the media box size. 
        /// The page size displayed to the end user is the crop box size. 
        /// The size of the ink canvas is the same as the crop box size. 
        /// Syncfusion uses the bottom left corner as the origin, while ink canvas uses the top left corner. 
        /// </remarks> 
        public async Task<bool> SaveInkingToPdf(InkingManager inkManager)
        {
            // Indicate whether any ink annotation is added to the PDF file
            bool fileChanged = false;
            // Remove ereased ink annotations
            foreach (KeyValuePair<int, List<InkStroke>> entry in await inkManager.ErasedStrokesDictionary())
            {
                // The key of the dictionary is page number, which is 1-based.
                int pageNumber = entry.Key;
                PdfLoadedPage sfPage = sfPdf.GetPage(pageNumber);
                // Get page information from MS model
                Windows.Data.Pdf.PdfPage msPage = msPdf.GetPage(pageNumber);

                PageMapping mapping = new PageMapping(msPage, sfPage);
                List<PdfInkAnnotation> erasedAnnotations = new List<PdfInkAnnotation>();
                // Add each ink stroke to the page
                foreach (InkStroke stroke in entry.Value)
                {
                    PdfInkAnnotation inkAnnotation = mapping.InkStroke2InkAnnotation(stroke);
                    erasedAnnotations.Add(inkAnnotation);
                }
                if (sfPdf.RemoveInkAnnotations(sfPage, erasedAnnotations)) fileChanged = true;
            }


            // Add new ink annotations
            foreach (KeyValuePair<int, InkStrokeContainer> entry in await inkManager.InAppInkDictionary())
            {
                PdfLoadedPage sfPage = sfPdf.GetPage(entry.Key);
                // Get page information from MS model
                Windows.Data.Pdf.PdfPage msPage = msPdf.GetPage(entry.Key);

                PageMapping mapping = new PageMapping(msPage, sfPage);
                
                // Add each ink stroke to the page
                foreach (InkStroke stroke in entry.Value.GetStrokes())
                {
                    PdfInkAnnotation inkAnnotation = mapping.InkStroke2InkAnnotation(stroke);
                    sfPage.Annotations.Add(inkAnnotation);
                    fileChanged = true;
                }
            }

            // Save the file only if there are changes.
            bool inkSaved = false;
            if (fileChanged)
            {
                try
                {
                    inkSaved = await sfPdf.SaveAsync();
                    // Copy and replace the actual file
                    await sfFile.CopyAndReplaceAsync(pdfFile);
                }
                catch (Exception ex)
                {
                    // Try to save the file by extracting the pages.
                    StorageFile newFile = await backupFolder.CreateFileAsync("COPY_" + pdfFile.Name, CreationCollisionOption.GenerateUniqueName);
                    try
                    {
                        await sfPdf.CopyPages(newFile);
                        inkSaved = true;
                        // Copy and replace the actual file
                        await newFile.CopyAndReplaceAsync(pdfFile);
                    }
                    catch
                    {
                        App.NotifyUser(typeof(ViewerPage), "Error: \n" + ex.Message, true);
                    }
                }
            }
            return !(inkSaved ^ fileChanged);
        }

        public async Task ReloadFile()
        {
            msPdf = await PdfModelMS.LoadFromFile(pdfFile);
        }
    }
}
