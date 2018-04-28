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

        public List<PdfLoadedInkAnnotation> GetInkAnnotations(int pageNumber)
        {
            List<PdfLoadedInkAnnotation> inkAnnotations = new List<PdfLoadedInkAnnotation>();
            PdfLoadedPage loadedPage = PdfDoc.Pages[pageNumber - 1] as PdfLoadedPage;
            foreach (PdfLoadedAnnotation annotation in loadedPage.Annotations)
            {
                if (annotation is PdfLoadedInkAnnotation)
                {
                    inkAnnotations.Add((PdfLoadedInkAnnotation)annotation);
                }
            }
            return inkAnnotations;
        }

        public bool RemoveInkAnnotations(PdfLoadedPage page, List<PdfInkAnnotation> inkAnnotations)
        {
            bool annotationRemoved = false;
            List<PdfLoadedAnnotation> toBeRemoved = new List<PdfLoadedAnnotation>();
            foreach (PdfLoadedAnnotation annotation in page.Annotations)
            {
                if (annotation is PdfLoadedInkAnnotation)
                {
                    PdfLoadedInkAnnotation loadedInk = (PdfLoadedInkAnnotation)annotation;
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

        public MemoryStream ExtractPageWithoutInking(int pageNumber)
        {
            PdfDocument pageDoc = new PdfDocument();
            pageDoc.ImportPageRange(PdfDoc, pageNumber - 1, pageNumber - 1);
            pageDoc.Pages[0].Annotations.Clear();
            PdfLoadedPage page = PdfDoc.Pages[pageNumber - 1] as PdfLoadedPage;
            foreach (PdfAnnotation annotation in page.Annotations)
            {
                if (!(annotation is PdfLoadedInkAnnotation))
                    pageDoc.Pages[0].Annotations.Add(annotation);
            }
            MemoryStream stream = new MemoryStream();
            pageDoc.Save(stream);
            pageDoc.Close();
            return stream;
        }

        private bool MatchInkAnnotations(PdfLoadedInkAnnotation loadedInk, PdfInkAnnotation erasedInk)
        {
            // Color
            if (loadedInk.Color.R != erasedInk.Color.R ||
                loadedInk.Color.G != erasedInk.Color.G ||
                loadedInk.Color.B != erasedInk.Color.B)
            {
                return false;
            }

            // Points
            List<float> points1 = loadedInk.InkList;
            List<float> points2 = erasedInk.InkList;
            if (points1.Count != points2.Count) return false;
            for (int i = 0; i < points1.Count; i++)
            {
                double threshold = 0.5;

                if (Math.Abs(points1[i] - points2[i]) > threshold)
                    return false;
            }
            return true;
        }
    }
}
