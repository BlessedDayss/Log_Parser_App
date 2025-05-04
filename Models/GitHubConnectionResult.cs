namespace Log_Parser_App.Models
{
    public class GitHubConnectionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TagName { get; set; }
        public string? DownloadUrl { get; set; }
    }
}
