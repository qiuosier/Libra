namespace Libra.Class
{
    /// <summary>
    /// Session State saves information about the state of the app.
    /// The information is saved when the app is suspended, so that it can be restored when the user re-open the app.
    /// </summary>
    public class SessionState
    {
        public SessionState()
        { }

        public SessionState(string token)
        {
            // A future access token for the pdf file.
            FileToken = token;
            // Indicates if the app is in viewer mode, i.e. user is viewing the PDF.
            ViewerMode = 1;
            // Data version of the session state.
            Version = CURRENT_SESSION_VERSION;
        }

        public string FileToken { get; set; }
        public int ViewerMode { get; set; }
        public int Version { get; set; }
        // Data version should be updated whenever the data structrue of this class is updated.
        public const int CURRENT_SESSION_VERSION = 1;
    }
}
