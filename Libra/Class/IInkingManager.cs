using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    interface IInkingManager
    {
        Task<InkStrokeContainer> loadInking(int pageNumber);
        Task saveInking(int pageNumber, InkStrokeContainer inkStrokeContainer);
        Task addStrokes(int pageNumber, IReadOnlyList<InkStroke> inkStrokes);
        Task eraseStrokes(int pageNumber, IReadOnlyList<InkStroke> inkStrokes);
    }
}
