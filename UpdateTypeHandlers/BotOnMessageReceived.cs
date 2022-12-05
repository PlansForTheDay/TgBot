using botTelegram.DateBase;
using botTelegram.Models;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using User = botTelegram.Models.User;
using botTelegram.UpdateHandler;
using botTelegram.ExtensionMethods;

namespace botTelegram.UpdateTypeHandlers
{
    public class BotOnMessageReceived
    {
        static Task NoProcessingType(Message message)
        {
            Console.Write("Необрабатываемый тип сообщения...\n => " + message.Type);
            return Task.CompletedTask;
        }

        async public Task Handler(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine(message.Chat.FirstName + " " + message.Chat.LastName + "   |   " + message.Text);

            if (!ExtensionMethods.ExtensionMethods.RegistrationCheck(botClient, message))
                return;

            var messageHandler = message.Type switch
            {
                MessageType.Text => TextHandler(message, botClient),
                MessageType.Photo => PhotoHandler(message, botClient),
                MessageType.Sticker => StickerHandler(message, botClient),
                MessageType.VideoNote => VideoNoteHandler(message, botClient),

                _ => NoProcessingType(message)
            };
            await messageHandler;
        }

        async Task TextHandler(Message message, ITelegramBotClient botClient)
        {
            if (message.Text.Length > 120)
            {
                await botClient.SendTextMessageAsync(message.From.Id, "Слишком много символов.\nМаксимально бот воспринимает 120 символов.");
                Console.WriteLine("Слишком большой объём текста...");
                return;
            }

            if (message.Text[0] == '/')
            {
                var textCommandHendler = message.Text switch
                {
                    "/help" => HelpTextCommand(message, botClient),
                    "/me" => MeTextCommand(message, botClient),
                    "/events" => EventsTextCommand(message, botClient),
                    "/back" => BackTextCommand(message, botClient),
                    "/admin_panel" => AdminPanelTextCommand(message, botClient),

                    _ => botClient.SendTextMessageAsync(message.Chat.Id, "Такого не знаем.")
                };

                await textCommandHendler;
                return;
            }

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == message.From.Id);
            string password = "pass";

            var textHendler = user.State switch
            {
                UserState.WaitingNick => ChangeNick(message, botClient),
                UserState.WaitingAboutMe => ChangeAboutMe(message, botClient),
                UserState.WaitingJoinCode => JoinEvent(message, botClient),
                UserState.WainingLeaveCode => LeaveEvent(message, botClient),
                UserState.WaitingAdminRassword => AdminPasswordChek(message, botClient, password),

                UserState.WaitingInfoCreateEvent => CreateEvent(message, botClient),
                UserState.WaitingRemovedEventCode => DeleteEvent(message, botClient),

                UserState.WaitingChangeEvent => ChangeEvent(message, botClient),

                _ => botClient.SendTextMessageAsync(message.From.Id, "Ну и что ты только что сказал?\nТы в меню и я не жду от тебя сообщения.")
            };

            await textHendler;
        }


        async Task HelpTextCommand(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine(message.Chat.FirstName + " | Пользователь узнаёт команды...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Вот список команд:\n" +
                            "| /help - Показывает все нужные команды.\n" +
                            "| /me - Показывает информацию о вас.\n" +
                            "| /events - Взаимодействие с мероприятиями.\n" +
                            "| /back - Прекращает текущее действие.");
        }
        async Task MeTextCommand(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine(message.Chat.FirstName + " | Пользователь решил узнать информацию о себе...");

            using var db = new BeerDbContext();
            User user = db.Users.First(u => u.Id == message.From.Id);

            InlineKeyboardMarkup button = new(new[]
            {
                new[] {InlineKeyboardButton.WithCallbackData("Изменить имя", $"changeNickname:{user.Id}")},
                new[] {InlineKeyboardButton.WithCallbackData("Изменить о себе", $"changeAboutMe:{user.Id}")},
                new[] {InlineKeyboardButton.WithCallbackData("Изменить фото", $"changePhoto:{user.Id}")}
            });

            using (var fileStream = new FileStream(Path.GetFullPath(UpdateHendler.PathToPhoto(user.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await botClient.SendPhotoAsync(message.Chat.Id, photo: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream),
                //$"Информация о пользователе: \n" +
                $"| Ваше имя - {user.Nickname}.\n" +
                $"| Ваш ID - {user.Id}.\n" +
                $"| О вас -\n {user.AboutMe}", replyMarkup: button);
            }
            user.SetStateAndSave(db, UserState.Menu);
        }
        async Task EventsTextCommand(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine("Пользователь просмотривает мероприятия...");
            try
            {
                await using var db = new BeerDbContext();
                User user = db.Users.First(u => u.Id == message.From.Id);

                IQueryable<Presence> presence = db.Presences.Where(e => e.IdUser == user.Id);
                if (presence != null)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вот мероприятия на которые ты планируешь прийти и панель взаимодействия с ними:");
                    foreach (var thisPresence in presence)
                    {
                        SendListEvent(thisPresence, message, botClient);
                    }
                }
                if (presence == null)
                { 
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Тебя нет ни на одном мероприятии.");
                }

                InlineKeyboardMarkup button = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Присоединиться", $"joinEvent:{user.Id}")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Покинуть", $"leaveEvent:{user.Id}")
                    }
                });

                await botClient.SendTextMessageAsync(message.Chat.Id, "Взаимодействие с мероприятиями.", replyMarkup: button);

                Console.WriteLine(message.Chat.FirstName + " | Сообщение отправленно...");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
            }
        }
        public async static void SendListEvent(Presence presence, Message message, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var @event = db.Events.First(e => e.Id == presence.IdEvent);
            var user = db.Users.First(u => u.Id == presence.IdUser);

            InlineKeyboardMarkup button = presence.Rank switch
            {
                Rank.Administrator => new(new[]
                {
                    new [] {InlineKeyboardButton.WithCallbackData("Гости", $"guestsEvents:{user.Id}:{@event.Id}")},
                    new [] {InlineKeyboardButton.WithCallbackData("Редактировать мероприятие", $"adminButtons:{user.Id}:{@event.Id}")}
                }),

                _ => new(new[]
                {
                    new [] {InlineKeyboardButton.WithCallbackData("Гости", $"guestsEvents:{user.Id}:{@event.Id}")},
                    new [] {InlineKeyboardButton.WithCallbackData("Изменить статус", $"changeEventRank:{user.Id}:{@event.Id}")}
                })
            };

            await botClient.SendTextMessageAsync(message.Chat.Id,
                 $"Название: {@event.Title}\n" +
                 $"Дата: {@event.Start.ToLongDateString()}\n" +
                 $"Время: {@event.Start.ToShortTimeString()}\n" +
                 $"Локация: {@event.Location}\n"+
                 $"Код присоединения: {@event.Code}\n" +
                 $"Статус на мероприятии: {presence.Rank.ToLocaleString()}", replyMarkup: button);
        }

        async Task BackTextCommand(Message message, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            Console.WriteLine(message.Chat.FirstName + " | Возврат в меню...");
            await botClient.SendTextMessageAsync(message.From.Id, "Вы вернулись в меню");           //желательно сделать всплывающим сообщением а не обычным

            User user = db.Users.First(u => u.Id == message.From.Id);
            user.SetStateAndSave(db, UserState.Menu);
        }
        async Task AdminPanelTextCommand(Message message, ITelegramBotClient botClient)
        {
            await using var db = new BeerDbContext();
            User user = db.Users.First(u => u.Id == message.From.Id);

            await botClient.SendTextMessageAsync(message.From.Id, "Введите пароль администратора что бы продолжить.");
            user.SetStateAndSave(db, UserState.WaitingAdminRassword);
        }

        async Task ChangeNick(Message message, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(db => db.Id == message.From.Id);
            user.Nickname = message.Text;
            user.State = UserState.Menu;
            db.SaveChanges();

            await botClient.SendTextMessageAsync(message.Chat.Id, $"Теперь все тебя знают как '{user.Nickname}'.");
            Console.WriteLine(message.Chat.FirstName + " | Пользователь поменял ник...");
        }
        async Task ChangeAboutMe(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine(message.Chat.FirstName + " | Пользователь поменял о себе...");

            using var db = new BeerDbContext();

            User user = db.Users.First(db => db.Id == message.From.Id);
            user.AboutMe = message.Text;
            db.SaveChanges();

            await botClient.SendTextMessageAsync(message.Chat.Id, $"Вот что получилось:\n '{user.AboutMe}'.");

            user.SetStateAndSave(db, UserState.Menu);
        }
        async Task JoinEvent(Message message, ITelegramBotClient botClient)
        {
            try
            {
                using var db = new BeerDbContext();

                var @event = db.Events.FirstOrDefault(x => x.Code == message.Text);
                User user = db.Users.First(i => i.Id == message.From.Id);
                if (@event == null)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятия с таким кодом нет.\nВозврат в меню.");
                    Console.WriteLine(message.Chat.FirstName + " | Неверный код мероприятия...");
                    user.SetStateAndSave(db, UserState.Menu);
                    return;
                }

                var w = new Presence(user, @event);

                var eventMembership = db.Presences.FirstOrDefault(i => i.IdUser == user.Id && i.IdEvent == @event.Id);
                if (eventMembership == null)
                {
                    db.Presences.Add(w);
                    db.SaveChanges();

                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вы присоединились к мероприятию как приглашённый.");
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вы уже состоите в списке гостей этого мероприятия.");
                }
                user.SetStateAndSave(db, UserState.Menu);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
            }
        }
        async Task LeaveEvent(Message message, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();

            var even = db.Events.FirstOrDefault(x => x.Code == message.Text);
            if (even == null)
            {
                Console.WriteLine(message.Chat.FirstName + " | Неверный код мероприятия...");
                await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятия с таким кодом нет.");
                return;
            }
            Console.WriteLine(message.Chat.FirstName + " | Верный код мероприятия...");

            var user = db.Users.First(db => db.Id == message.From.Id);
            var w = db.Presences.FirstOrDefault(t => t.IdUser == user.Id && t.IdEvent == even.Id);
            if (w != null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Вы покинули мероприятие.");
                db.Presences.Remove(w);
                db.SaveChanges();
            }
            else
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Вы не участвовали в этом мероприятии.");
            }
            user.SetStateAndSave(db, UserState.Menu);
        }
        async Task AdminPasswordChek(Message message, ITelegramBotClient botClient, string password)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == message.From.Id);

            if (!message.Text.Contains(password))
            {
                Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл неверный код...");
                await botClient.SendTextMessageAsync(message.Chat.Id, "Неверный код. Возврат в меню.");

                user.SetStateAndSave(db, UserState.Menu);
                return;
            }
            Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл верный код...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Верно.");

            InlineKeyboardMarkup eventButtons = new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Создать мероприятие", $"EventCreate:{user.Id}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Удалить мероприятие", $"EventDelete:{user.Id}")
                }
            });

            await botClient.SendTextMessageAsync(message.Chat.Id, "Взаимодействие с мероприятиями.", replyMarkup: eventButtons);
            user.SetStateAndSave(db, UserState.Menu);
        }
        async Task CreateEvent(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine(message.Chat.FirstName + " | Создание мероприятия...");

            string[] words;
            int[] dateParts;

            try
            {
                words = message.Text.Split('\n');
                dateParts = new int[words[1].Length];

                dateParts = words[1].Split('.').Select(x => int.Parse(x)).ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
                await botClient.SendTextMessageAsync(message.Chat.Id, "Видимо вы некорректно ввели информацию.\nПопробуйте ещё раз");
                return;
            }

            string eventCode = words[2];
            string eventTitle = words[0];
            DateTime eventDate = new DateTime(dateParts[2], dateParts[1], dateParts[0]);

            await using var db = new BeerDbContext();
            User user = db.Users.First(i => i.Id == message.From.Id);

            try
            {
                Event @event = new(eventTitle, eventDate, eventCode);
                
                db.Events.Add(@event);

                var newPresece = new Presence(user, @event)
                {
                    Rank = Rank.Administrator
                };

                db.Presences.Add(newPresece);
                db.SaveChanges();
                
                user.SetStateAndSave(db, UserState.Menu);

                Console.WriteLine($"Мероприятие '{@event.Title}' создано...");
                await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие создано.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
                await botClient.SendTextMessageAsync(message.Chat.Id, "Ошибка сохранения мероприятия.\nВозврат в меню.");

                user.SetStateAndSave(db, UserState.Menu);
            }
        }
        async Task DeleteEvent(Message message, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == message.From.Id);
            try
            {
                var @event = db.Events.FirstOrDefault(e => e.Code == message.Text);
                if (@event == null)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие не найдено.\nВозврат в меню.");
                    user.SetStateAndSave(db, UserState.Menu);
                    return;
                }
                //var presence = db.Presences.FirstOrDefault(p => p.IdUser == user.Id && p.IdEvent == @event.Id);
                //if (presence == null)
                //{
                //    await botClient.SendTextMessageAsync(message.Chat.Id, "Вы не числитесь на этом мероприятии.\nВозврат в меню.");
                //    Console.WriteLine("Отказ по причине отсутствия на мероприятии...");
                //    return;
                //}

                db.Events.Remove(@event);
                db.SaveChanges();

                Console.WriteLine($"Мероприятие {@event.Title} удалено...");
                await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие удалено.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
                await botClient.SendTextMessageAsync(message.Chat.Id, "Ошибка.");
            }
            user.SetStateAndSave(db, UserState.Menu);
        }
        async Task ChangeEvent(Message message, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(db => db.Id == message.From.Id);

            try
            {
                string path = $"event_change_files\\{user.Id}.txt";
                string stringParameter = "";

                using (StreamReader streamReader = System.IO.File.OpenText(path))
                {
                    stringParameter = streamReader.ReadLine();
                };

                if (stringParameter == null)
                {
                    Console.WriteLine($"Файл пусной...\n{path}");
                    await botClient.SendTextMessageAsync(message.From.Id, "Нужный файл не найден.\nВозврат в меню.");
                    user.SetStateAndSave(db, UserState.Menu);
                    return;
                }

                string[] @object = stringParameter.Split('.');
                var @event = db.Events.First(e => e.Id == long.Parse(@object[0]));


                if (@object[1] == "Date")
                {

                    string[] oldDateTime = @event.Start.ToString().Split(" ");
                    int[] newDate = message.Text.Split('.').Select(x => int.Parse(x)).ToArray();
                    int[] oldTime = oldDateTime[1].Split(':').Select(x => int.Parse(x)).ToArray();


                    @event.Start = new DateTime(newDate[2], newDate[1], newDate[0], oldTime[0], oldTime[1], oldTime[2]);

                    db.SaveChanges();
                    user.SetStateAndSave(db, UserState.Menu);

                    await botClient.SendTextMessageAsync(message.From.Id, $"Дата обновлена.\n{@event.Start.ToLongDateString()}");
                    Console.WriteLine($"Дата обновлена...");
                    return;
                }
                else if (@object[1] == "Time")
                {
                    string[] oldDateTime = @event.Start.ToString().Split(" ");
                    int[] newTime = message.Text.Split(':').Select(x => int.Parse(x)).ToArray();
                    int[] oldDate = oldDateTime[0].Split('.').Select(x => int.Parse(x)).ToArray();

                    @event.Start = new DateTime(oldDate[2], oldDate[1], oldDate[0], newTime[0], newTime[1],0);

                    db.SaveChanges();
                    user.SetStateAndSave(db, UserState.Menu);

                    await botClient.SendTextMessageAsync(message.From.Id, $"Время обновлено.\n{@event.Start.ToShortTimeString()}");
                    Console.WriteLine($"Время обновлено...");
                    return;
                }
                else
                {
                    var changeInfo = @object[1] switch
                    {
                        "Title" => @event.Title = message.Text,
                        "Location" => @event.Location = message.Text,

                        _ => "NoProcessing"
                    };

                    if (changeInfo == "NoProcessing")
                    {
                        Console.WriteLine($"Ошибка обработки...");
                        await botClient.SendTextMessageAsync(message.From.Id, "Проблема обработки.\nВозврат в меню.");
                        user.SetStateAndSave(db, UserState.Menu);
                        return;
                    }

                    await botClient.SendTextMessageAsync(message.From.Id, "Обновлено.\nВозврат в меню.");

                    db.SaveChanges();
                    user.SetStateAndSave(db, UserState.Menu);
                }
                System.IO.File.Delete(path);
            }
            catch(Exception ex)
            {
                user.SetStateAndSave(db, UserState.Menu);
                await botClient.SendTextMessageAsync(message.From.Id, "Ошибка, это может быть связяно с некоректным вводом даты или времени.\nВозврат в меню.");
                Console.WriteLine("Ошибка...\n" + ex);
            }
        }


        async Task PhotoHandler(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine("Пользователь отправил фото...");
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == message.From.Id);
            if (!Directory.Exists("user_photo"))
            {
                await botClient.SendTextMessageAsync(message.From.Id, "Возникли технические шоколадки и пока что у меня нет возможности работать с фото.");
                Console.WriteLine("Директории с видео не обнаружено...");
                user.SetStateAndSave(db, UserState.Menu);
                return;
            }

            var photoHanler = user.State switch
            {
                UserState.WaitingPhoto => ChangePhoto(message, botClient),

                _ => Task.CompletedTask
            };
        }


        async Task ChangePhoto(Message message, ITelegramBotClient botClient)
        {
            User user;
            using var db = new BeerDbContext();
            user = db.Users.First(u => u.Id == message.From.Id);

            bool chek = false;
            for (int i = 0; i < message.Photo.Length; i++)
            {
                if (message.Photo[i].Width < 513 && message.Photo[i].Height < 513)
                {
                    var fileId = message.Photo.Last().FileId;
                    var fileInfo = await botClient.GetFileAsync(fileId);
                    var filePath = fileInfo.FilePath;

                    string destinationFilePath = $"user_photo\\{user.Id}.png";
                    await using (FileStream fileStream = System.IO.File.OpenWrite(destinationFilePath))
                    {
                        await botClient.DownloadFileAsync(filePath: filePath, destination: fileStream);
                    }
                    chek = true;
                }
            }

            if (!chek)
            {
                await botClient.SendTextMessageAsync(message.From.Id, "Размеры не подходят.");
                return;
            }
            await botClient.SendTextMessageAsync(message.From.Id, "Фото обновлено.");
            Console.WriteLine($"Пользователь {user.Nickname} поменял фото...");
            user.SetStateAndSave(db, UserState.Menu);
        }

        async Task StickerHandler(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine("Пользователь отправил стикер...");
            try
            {
                string pathToStickers = "stickers";
                string[] listStickers;
                Random randomSticker = new Random();

                if (!Directory.Exists(pathToStickers))
                {
                    await botClient.SendTextMessageAsync(message.From.Id, "Возникли технические шоколадки и пока что у меня нет возможности ответить как надо.");
                    Console.WriteLine("Директории со стикерами не обнаружено...");
                    return;
                }
                listStickers = Directory.GetFiles(pathToStickers, "*.web?");      //"*.webp" , "*.webm"
                int directorySize = listStickers.Length;

                string sticker = listStickers[randomSticker.Next(0, directorySize)];

                if (sticker.Contains(".webm"))
                {
                    using (var fileStream = new FileStream(Path.GetFullPath(sticker), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await botClient.SendVideoAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream));
                    }
                }
                if (sticker.Contains(".webp"))
                {
                    using (var stick = System.IO.File.Open(sticker, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await botClient.SendStickerAsync(message.From.Id, stick);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
            }
        }

        async Task VideoNoteHandler(Message message, ITelegramBotClient botClient)
        {
            Console.WriteLine("Пользователь отправил кружок...");
            if (!Directory.Exists("video"))
            {
                await botClient.SendTextMessageAsync(message.From.Id, "Возникли технические шоколадки и пока что у меня нет возможности ответить как надо.");
                Console.WriteLine("Директории с видео не обнаружено...");
                return;
            }

            string path = "video\\hmmm.mp4";

            using (var fileStream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await botClient.SendVideoNoteAsync(message.Chat.Id, videoNote: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream));
            }
        }
    }
}
