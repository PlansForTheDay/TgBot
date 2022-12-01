using botTelegram.DateBase;
using botTelegram.Models;
using Telegram.Bot.Types;
using Telegram.Bot;
using User = botTelegram.Models.User;

namespace botTelegram.ExtensionMethods
{
    public static class ExtensionMethods
    {
        public static string ToLocaleString(this Rank r)
        {
            return r switch
            {
                Rank.Administrator => "Администратор",
                Rank.Member => "Гость",
                Rank.Doubting => "В раздумьях",
                Rank.Invited => "Приглашённый",
                _ => "Не опознан"
            };
        }

        public static bool CheckUserInDb(long id)
        {
            using var db = new BeerDbContext();
            User chek = db.Users.FirstOrDefault(x => x.Id == id);

            if (chek != null)
                return true;
            return false;
        }

        public static bool RegistrationCheck(ITelegramBotClient botClient, Message message)
        {
            if (CheckUserInDb(message.From.Id))
                return true;

            if (message.Text.Contains("/"))
            {
                botClient.SendTextMessageAsync(message.Chat.Id, "В текущей версии бота, вы не авторизованы\nВведи имя под которым тебя многие узнают");
                Console.WriteLine(message.Chat.FirstName + " | Запрос на имя отправлен...");
                return false;
            }
                
            try
            {
                string nickname = message.Text;
                User one = new User(message.From, nickname);

                using (BeerDbContext db = new BeerDbContext())
                {
                    db.Users.Add(one);
                    db.SaveChanges();
                }

                Console.WriteLine("Пользователь зарегистрирован без ошибок...\n");
                botClient.SendTextMessageAsync(message.Chat.Id, $"Теперь ты зарегистрирован и можешь взаимодействовать с ботом\n" +
                    $"Тебя зовут {message.Text}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
                botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка");
            }
            return false;
        }
    }
}
