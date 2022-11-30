using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using botTelegram.DateBase;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace botTelegram.Models
{
    public class User
    {
        [Key]
        public long Id { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string AboutMe { get; set; }
        public UserState State { get; set; }
        public ICollection<Presence>? Events { get; set; }

        public User()
        {

        }

        public User(Telegram.Bot.Types.User user, string nick)
        {
            Id = user.Id;
            Name = (user.FirstName + " " + user.LastName).Trim();
            Nickname = nick;
            AboutMe = "Не имеется";
            State = UserState.menu;

            Events = new List<Presence>();
        }

        public void SetStateAndSave(BeerDbContext db, UserState n)
        {
            State = n;
            db.SaveChanges();
        }
    }
}
