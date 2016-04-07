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
        Task<InkStrokeContainer> loadInking(int pageNumber);
        Task saveInking(int pageNumber, InkStrokeContainer inkStrokes);
    }
}
