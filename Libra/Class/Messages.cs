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
            "Please BACKUP YOUR PDF FILE before saving ink strokes. \n" +
            "Pressure information in the ink strokes WILL BE LOST. \n" +
            "\n" +
            "Save ink strokes to the file? \n";

        public static string INK_STROKE_WARNING =
            "Ink strokes are saved automatically in this App. \n" +
            "To save the ink strokes into the PDF file, click the [Save Ink Annotations] button." +
            "\n" +
            "Ink strokes saved in this App will be lost if you reinstall the App.";

        public static string ERASER_WARNING =
            "Eraser deletes the entire stroke. \n" +
            "Eraser operation cannot be undo. \n" +
            "Please use with care. ";
    }
}
