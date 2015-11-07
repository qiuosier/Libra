using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Libra.Class
{
    public class SessionState
    {
        public SessionState()
        { }

        public SessionState(string token)
        {
            this.FileToken = token;
            this.ViewerMode = 1;
        }

        public string FileToken { get; set; }
        public int ViewerMode { get; set; }
    }
}
