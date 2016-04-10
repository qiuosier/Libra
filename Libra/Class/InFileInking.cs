using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class InFileInking : IInkingManager
    {
        public Task addStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            throw new NotImplementedException();
        }

        public Task eraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            throw new NotImplementedException();
        }

        public InkStrokeContainer loadInking(int pageNumber)
        {
            throw new NotImplementedException();
        }

        public Task saveInking(int pageNumber, InkStrokeContainer inkStrokes)
        {
            throw new NotImplementedException();
        }
    }
}
