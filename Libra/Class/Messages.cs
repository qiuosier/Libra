using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Libra.Class
{
    class Messages
    {
        public static string SAVE_INKING_CLICKED =
            "WARNING: \n" +
            "\n" +
            "Saving ink annotations to the PDF file is an experimental feature. \n" +
            "Please BACKUP YOUR PDF FILE before saving ink strokes. \n" +
            "\n" +
            "Pressure information in the ink strokes WILL BE LOST. \n" +
            "Ink strokes saved to the PDF file are no longer editable in this app. \n" +
            "However, you can edit or remove the ink strokes with Adobe reader. \n" +
            "\n" +
            "Save ink strokes to the file? \n";

        public static string INK_STROKE_WARNING =
            "Ink strokes collection is an experimental feature. \n" +
            "Ink strokes are saved automatically IN THIS APP. \n" +
            "To save the ink strokes into the pdf file, click the [Save Ink Annotations] button." +
            "\n" +
            "Ink strokes saved in this app will be lost if you reinstall windows. \n" +
            "You can also export the ink strokes along with pdf pages as image files.";

        public static string ERASER_WARNING =
            "Eraser deletes the entire stroke. \n" +
            "Eraser operation cannot be undo. \n" +
            "Please use with care. ";
    }
}
