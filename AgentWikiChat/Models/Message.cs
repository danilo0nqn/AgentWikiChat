namespace AgentWikiChat.Models;

public class Message
{
    public string Role { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }

    public Message(string role, string content)
    {
        Role = role;
        Content = content;
        Timestamp = DateTime.Now;
    }
}
