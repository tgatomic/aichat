namespace ChatGpt.Data
{
    public class Message
    {
        public string Content { get; set; }
        public bool User { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
