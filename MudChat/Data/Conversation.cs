namespace ChatGpt.Data
{
    public class Conversation
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Title { get; set; }
        public bool Selected { get; set; }
        public bool GeneratedTitle { get; set; }

        public List<Message> Messages { get; set; }
    }
}
