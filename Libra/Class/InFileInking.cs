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
        public Task addStrokes(int pageNumber, IReadOnlyList<InkStroke> inkStrokes)
        {
            throw new NotImplementedException();
        }

        public Task eraseStrokes(int pageNumber, IReadOnlyList<InkStroke> inkStrokes)
        {
            throw new NotImplementedException();
        }

        public Task<InkStrokeContainer> loadInking(int pageNumber)
        {
            throw new NotImplementedException();
        }

        public Task saveInking(int pageNumber, InkStrokeContainer inkStrokes)
        {
            throw new NotImplementedException();
        }
    }
}
