using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace botTelegram.Models
{
    public class Presence
    {
        [Key]
        public long IdUser { get; set; }
        public User User { get; set; }

        [Key]
        public long IdEvent { get; set; }
        public Event Event { get; set; }

        public Rank Rank { get; set; }

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
