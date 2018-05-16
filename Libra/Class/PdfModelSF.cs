using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    /// <summary>
    /// Represents the Syncfusion PDF document model.
    /// This class operates directly on the PDF file selected/specified by the user.
    /// </summary>
    public class PdfModelSF
    {
        /// <summary>
        /// The Syncfusion loaded PDF document object loaded from the pdfFile.
        /// </summary>
        private PdfLoadedDocument PdfDoc;

        /// <summary>
        /// The PDF file specified by the user.
        /// </summary>
        private StorageFile pdfFile;

        /// <summary>
        /// Private constructor to initialize the properties.
        /// An instance of this class should be created by calling the LoadFromFile() static method.
        /// </summary>
        /// <param name="pdfStorageFile">The PDF file specified by the user.</param>
        private PdfModelSF(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        /// <summary>
        /// Outputs an asynchronous operation. 
        /// When the operation completes, a initialized PdfModelSF object, representing the PDF, is returned.
        /// </summary>
        /// <param name="pdfStorageFile">The PDF file</param>
        /// <returns>An initialized PdfModelSF object.</returns>
        public static async Task<PdfModelSF> LoadFromFile(StorageFile pdfStorageFile, string password = null)
        {
            PdfModelSF pdfModel = new PdfModelSF(pdfStorageFile);
            pdfModel.PdfDoc = new PdfLoadedDocument();
            // Load PDF from file
            try
            {
                if (password == null) await pdfModel.PdfDoc.OpenAsync(pdfStorageFile);
                else await pdfModel.PdfDoc.OpenAsync(pdfStorageFile, password);
            }
            catch (Exception ex)
            {
                // Failed to load the file
                AppEventSource.Log.Debug("PDFModelSF: " + ex.Message);
                // Notify the user?
                App.NotifyUser(typeof(ViewerPage), "Failed to open the file.\n" + ex.Message, true);
                pdfModel = null;
            }
            return pdfModel;
        }

        /// <summary>
        /// Saves the modified pdf document.
        /// </summary>
        /// <returns>If the file is saved successfully, true. Otherwise false.</returns>
        public async Task<bool> SaveAsync()
        {
            return await PdfDoc.SaveAsync(pdfFile);
        }

        /// <summary>
        /// Gets a page object from the file.
        /// </summary>
        /// <param name="pageNumber">1-based page number.</param>
        /// <returns></returns>
        public PdfLoadedPage GetPage(int pageNumber)
        {
            PdfLoadedPage page = PdfDoc.Pages[pageNumber - 1] as PdfLoadedPage;
            return page;
        }

        /// <summary>
        /// Gets the number of annotations in a page.
        /// The annotations include not all types of annotations.
        /// </summary>
        /// <param name="pageNumber">1-based page number</param>
        /// <returns>The number of annotations.</returns>
        public int AnnotationCount(int pageNumber)
        {
            PdfLoadedPage loadedPage = GetPage(pageNumber);
            return loadedPage.Annotations.Count;
        }

        /// <summary>
        /// Gets a list of ink annotations in a page
        /// </summary>
        /// <param name="pageNumber">1-based page number</param>
        /// <returns>A list of ink annotations.</returns>
        public List<PdfLoadedInkAnnotation> GetInkAnnotations(int pageNumber)
        {
            List<PdfLoadedInkAnnotation> inkAnnotations = new List<PdfLoadedInkAnnotation>();
            foreach (PdfLoadedAnnotation annotation in GetPage(pageNumber).Annotations)
            {
                if (annotation is PdfLoadedInkAnnotation)
                {
                    inkAnnotations.Add((PdfLoadedInkAnnotation)annotation);
                }
            }
            return inkAnnotations;
        }

        /// <summary>
        /// Removes a list of ink annotations from a page.
        /// </summary>
        /// <param name="page">1-based page number.</param>
        /// <param name="inkAnnotations">A list of ink annotations to be removed. 
        /// Annotation not found in the page will be ignored.</param>
        /// <returns>If at least one annotation is removed, true. Otherwise false.</returns>
        public bool RemoveInkAnnotations(PdfLoadedPage page, List<PdfInkAnnotation> inkAnnotations)
        {
            bool annotationRemoved = false;
            List<PdfLoadedAnnotation> toBeRemoved = new List<PdfLoadedAnnotation>();
            foreach (PdfLoadedAnnotation annotation in page.Annotations)
            {
                if (annotation is PdfLoadedInkAnnotation loadedInk)
                {
                    PdfInkAnnotation matched = null;
                    foreach (PdfInkAnnotation erasedInk in inkAnnotations)
                    {
                        if (MatchInkAnnotations(loadedInk, erasedInk))
                        {
                            toBeRemoved.Add(annotation);
                            matched = erasedInk;
                            break;
                        }
                    }
                    if (matched != null) inkAnnotations.Remove(matched);
                }
            }
            foreach(PdfLoadedAnnotation a in toBeRemoved)
            {
                page.Annotations.Remove(a);
                annotationRemoved = true;
            }
            return annotationRemoved;
        }

        /// <summary>
        /// Extracts a page as a pdf document to a memory stream.
        /// </summary>
        /// <param name="pageNumber">1-based page number</param>
        /// <returns>A memory stream containing the new document from a single page.</returns>
        public MemoryStream ExtractPageWithoutInking(int pageNumber)
        {
            PdfDocument pageDoc = new PdfDocument();
            pageDoc.ImportPageRange(PdfDoc, pageNumber - 1, pageNumber - 1);
            pageDoc.Pages[0].Annotations.Clear();
            foreach (PdfAnnotation annotation in GetPage(pageNumber).Annotations)
            {
                if (!(annotation is PdfLoadedInkAnnotation))
                    pageDoc.Pages[0].Annotations.Add(annotation);
            }
            MemoryStream stream = new MemoryStream();
            pageDoc.Save(stream);
            pageDoc.Close();
            return stream;
        }

        /// <summary>
        /// Checks if two ink annotations are matched by comparing their colors and ink points.
        /// </summary>
        /// <param name="loadedInk">The ink annotation in the pdf file.</param>
        /// <param name="appInk">The ink annotation constructed by the app.</param>
        /// <returns>If the two annotations are matched, true. Otherwise false.</returns>
        private bool MatchInkAnnotations(PdfLoadedInkAnnotation loadedInk, PdfInkAnnotation appInk)
        {
            // Color
            if (loadedInk.Color.R != appInk.Color.R ||
                loadedInk.Color.G != appInk.Color.G ||
                loadedInk.Color.B != appInk.Color.B)
            {
                return false;
            }

            // Points
            List<float> points1 = loadedInk.InkList;
            List<float> points2 = appInk.InkList;
            if (points1.Count != points2.Count) return false;
            for (int i = 0; i < points1.Count; i++)
            {
                double threshold = 0.5;

                if (Math.Abs(points1[i] - points2[i]) > threshold)
                    return false;
            }
            return true;
        }

        public void Close()
        {
            PdfDoc.Close();
        }

        public async Task CopyPages(StorageFile newFile)
        {
            PdfDocument newDoc = new PdfDocument();
            newDoc.ImportPageRange(PdfDoc, 0, PdfDoc.PageCount - 1);
            using (Stream stream = await newFile.OpenStreamForWriteAsync())
            {
                newDoc.Save(stream);
            }
            newDoc.Close();
        }
    }
}
