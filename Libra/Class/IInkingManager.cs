using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public interface IInkingManager
    {
        InkStrokeContainer loadInking(int pageNumber);
        Task saveInking(int pageNumber, InkStrokeContainer inkStrokeContainer);
        Task addStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes);
        Task eraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes);
    }
}
