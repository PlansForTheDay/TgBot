namespace botTelegram.Models
{
    public class Event
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public DateTime Start { get; set; }
        public string Location { get; set; }
        public string Code { get; set; }
        public ICollection<Presence>? Guests { get; set; }

        public Event()
        {

        }
        public Event(string title, DateTime startDate, string eventCode)
        {
            Id = DateTime.UtcNow.Ticks;

            Title = title;
            Start = startDate;
            Code = eventCode;
            Location = "Не выбрана";

            Guests = new List<Presence>();
        }

    }
}
