using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Libra.Class
{
    public class PageRange
    {
        public int first { get; set; }
        public int last { get; set; }
        public PageRange()
        {
            this.first = 0;
            this.last = 0;
        }
        public PageRange(int firstPage, int lastPage)
        {
            this.first = firstPage;
            this.last = lastPage;
        }

        public override string ToString()
        {
            return first == last ?
                "Page " + first.ToString() :
                "Page " + first.ToString() + "-" + last.ToString();
        }
    }
}
