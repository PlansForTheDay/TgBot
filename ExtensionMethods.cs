using botTelegram.DateBase;
using botTelegram.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public static bool RegistrationCheck(ITelegramBotClient botClient, Message message)
        {
            User ch;
            using (BeerDbContext db = new BeerDbContext())
            {
                ch = db.Users.FirstOrDefault(x => x.Id == message.From.Id);
            }
            if (ch == null)
            {
                if (!message.Text.Contains("/"))
                {
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
                }
                else
                {
                    botClient.SendTextMessageAsync(message.Chat.Id, "В текущей версии бота, вы не авторизованы\n" +
                        "Введи имя под которым тебя многие узнают");

                    Console.WriteLine(message.Chat.FirstName + " | Запрос на имя отправлен...");
                }
                return false;
            }
            else
                return true;
        }
    }
}
