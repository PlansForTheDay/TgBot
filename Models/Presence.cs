using System.ComponentModel.DataAnnotations;

namespace botTelegram.Models
{
    public class Presence
    {
        [Key]
        public long IdUser { get; set; }
        [Key]
        public long IdEvent { get; set; }
        public User User { get; set; }
        public Event Event { get; set; }
        public Rank Rank { get; set; }

        public Presence()
        {

        }
        public Presence(User thisUser, Event thisEvent)
        {
            IdUser = thisUser.Id;
            User = thisUser;
            IdEvent = thisEvent.Id;
            Event = thisEvent;

            Rank = Rank.Invited;
        }

    }
}
