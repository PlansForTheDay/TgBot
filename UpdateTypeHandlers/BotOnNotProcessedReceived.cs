using Update = Telegram.Bot.Types.Update;

namespace botTelegram.UpdateTypeHandlers
{
    internal class BotOnNotProcessedReceived
    {
        public async Task Handler(Update update)
        {
            Console.WriteLine("Необрабатываемый Update:\n ID\n  =>" + update.Id + "\n Type\n  =>" + update.Type);
        }
    }
}
