using System;

namespace Libra.Class
{
    public class RecentFile
    {
        public RecentFile()
        {

        }

        public RecentFile(string token)
        {
            this.mruToken = token;
        }

        public string Filename { get; set; }
        public string mruToken { get; set; }
        public string Identifier { get; set; }
        public DateTime LastAccessTime { get; set; }
    }
}
