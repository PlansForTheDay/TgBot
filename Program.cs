using Telegram.Bot;
using botTelegram.UpdateHandler;
using Update = Telegram.Bot.Types.Update;

namespace Bot
{
    class Program
    {
        static void Main()
        {
            var client = new TelegramBotClient("TOKEN");

            client.StartReceiving(Update, Error);
            string stop = "";
            while (!stop.ToLower().Contains("/stop"))
            {
                stop = Console.ReadLine();
            }
        }

        private static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            return UpdateHendler.UpdateHandlerAsync(botClient, update, token);
        }
        private static Task Error(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine("Ошибка...\n" + exception);
            return Task.CompletedTask;
        }
    }
}