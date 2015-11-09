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
            this.version = CURRENT_SESSION_VERSION;
        }

        public string FileToken { get; set; }
        public int ViewerMode { get; set; }
        public int version { get; set; }

        public const int CURRENT_SESSION_VERSION = 1;
    }
}
