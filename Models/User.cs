using System.ComponentModel.DataAnnotations;
using botTelegram.DateBase;

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
            State = UserState.Menu;

            Events = new List<Presence>();
        }


        public void SetStateAndSave(BeerDbContext db, UserState n)
        {
            State = n;
            db.SaveChanges();
        }
    }
}
