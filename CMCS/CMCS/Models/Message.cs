namespace CMCS.Models
{
    public class Message
    {
        public int MessageId { get; set; }
        public string Sender { get; set; }
        public string Content { get; set; }
        public DateTime Date { get; set; }
        public bool IsRead { get; set; }
    }
}
