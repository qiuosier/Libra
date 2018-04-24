using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    /// <summary>
    /// Represent the Syncfusion PDF document model
    /// </summary>
    public class PdfModelSF
    {
        /// <summary>
        /// The loaded PDF document.
        /// </summary>
        public PdfLoadedDocument PdfDoc { get; private set; }

        /// <summary>
        /// The PDF file.
        /// </summary>
        private StorageFile pdfFile;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        private PdfModelSF(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        /// <summary>
        /// Outputs an asynchronous operation. 
        /// When the operation completes, a PdfModelSF object, representing the PDF, is returned.
        /// </summary>
        /// <param name="pdfStorageFile">The PDF file</param>
        /// <returns></returns>
        public static async Task<PdfModelSF> LoadFromFile(StorageFile pdfStorageFile, string password = null)
        {
            PdfModelSF file = new PdfModelSF(pdfStorageFile);
            file.PdfDoc = new PdfLoadedDocument();
            // Load PDF from file
            try
            {
                if (password == null) await file.PdfDoc.OpenAsync(pdfStorageFile);
                else await file.PdfDoc.OpenAsync(pdfStorageFile, password);
            }
            catch (Exception ex)
            {
                // Failed to load the file
                App.NotifyUser(typeof(ViewerPage), "Failed to open the file.\n" + ex.Message, true);
                file = null;
            }
            return file;
        }

        public int AnnotationCount(int pageNumber)
        {
            PdfLoadedPage loadedPage = PdfDoc.Pages[pageNumber - 1] as PdfLoadedPage;
            return loadedPage.Annotations.Count;
        }

        public List<InkStroke> GetInkAnnotations(int pageNumber)
        {
            List<InkStroke> inkStrokes = new List<InkStroke>();
            PdfLoadedPage loadedPage = PdfDoc.Pages[pageNumber - 1] as PdfLoadedPage;
            InkStrokeBuilder strokeBuilder = new InkStrokeBuilder();
            foreach (PdfLoadedAnnotation annotation in loadedPage.Annotations)
            {
                if (annotation is PdfLoadedInkAnnotation)
                {
                    List<float> strokePoints = ((PdfLoadedInkAnnotation)annotation).InkList;
                    List<Windows.Foundation.Point> inkPoints = new List<Windows.Foundation.Point>();
                    for (int i = 0; i < strokePoints.Count; i = i + 2)
                    {
                        inkPoints.Add(new Windows.Foundation.Point(strokePoints[i], strokePoints[i + 1]));
                    }
                    InkStroke inkStroke = strokeBuilder.CreateStroke(inkPoints);
                }
            }
            return inkStrokes;
        }

        public async Task<StorageFile> ExtractPageWithoutInking(int pageNumber, StorageFolder storageFolder)
        {
            PdfDocument pageDoc = new PdfDocument();
            StorageFile storageFile = await storageFolder.CreateFileAsync("page_" + pageNumber.ToString() + ".pdf", CreationCollisionOption.ReplaceExisting);
            pageDoc.ImportPageRange(PdfDoc, pageNumber - 1, pageNumber - 1);
            pageDoc.Pages[0].Annotations.Clear();
            PdfLoadedPage page = PdfDoc.Pages[pageNumber - 1] as PdfLoadedPage;
            foreach (PdfAnnotation annotation in page.Annotations)
            {
                Type c = annotation.GetType();
                if (!(annotation is PdfLoadedInkAnnotation))
                    pageDoc.Pages[0].Annotations.Add(annotation);
            }
            await pageDoc.SaveAsync(storageFile);
            pageDoc.Close();
            return storageFile;
        }
    }
}
