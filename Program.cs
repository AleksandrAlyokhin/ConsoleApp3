using Newtonsoft.Json;
using ProgramSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConsoleApp3
{
    public class Program
    {
        class User
        {
            public long chatId { get; set; }            //айди чата 
            public long migration { get; set; }         //значение миграции в чате с chatId
            public bool setValueCheck { get; set; }     //состояние указывающее что нужно принять значение migration
            public  Message setterValueId { get; set; } //Message в котором хранится id пользователя запустившего команду set_migration
            public User(long tmp, long Id, bool check, Message msg)
            {
                chatId = Id;
                migration = tmp;
                setValueCheck = check;
                setterValueId = msg;
            }
            public User()
            {
            }
        }

        private static TelegramBotClient Bot;

        static long tmpChatId;                          //класс для работы с json файлом
        const string path = "gg.json";                  //путь к файлу json

        static User user = new User();                  //буффераня переменная, в которой будех храниться информация о чате
        static List<User> Users = new List<User>();     //лист, в котором хранится информация о чате


        public static async Task Main()
        {
            Bot = new TelegramBotClient(Configuration.BotToken);
            var me = await Bot.GetMeAsync();
            var cts = new CancellationTokenSource();
            Users = JsonConvert.DeserializeObject<List<User>>(System.IO.File.ReadAllText(path));
            Bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync), cts.Token);       //подключаем обработчик на обновления и ошибки

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            cts.Cancel();
        }

        // обработчик на события чата
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            //switch переключатель между задачами/апдейтами
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(update, update.Message, cancellationToken),
                UpdateType.EditedMessage => BotOnMessageReceived(update, update.EditedMessage, cancellationToken),
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        // метод отвечающий за обработку входящего сообщения
        private static async Task BotOnMessageReceived(Update update, Message message, CancellationToken cancellationToken)
        {         
            tmpChatId = update.Message.Chat.Id;
            await CheckUser();

            Console.WriteLine($"Receive message type: {message.Type}");
            Message sentMessage = message;

            //проверяем является ли сообщенией командой 
            user = Users.Where(n => n.chatId == tmpChatId).FirstOrDefault();
            if (message.Type == MessageType.Text && message.Text.StartsWith('/'))
            {
                var action = (message.Text.Split('@').First()) switch
                {
                    "/show_migration" => ShowMigration(message),
                    "/next_migration" => NextMigration(message),
                    "/set_migration" => GetValue(message),
                    _ => Usage(message)
                };
                sentMessage = await action;
            }// или это сообщение для ввода после метод setvalue
            else if (message.Type == MessageType.Text && user.setValueCheck)
            {
                if (message.Text.StartsWith('№'))
                {
                    var admin = Bot.GetChatMemberAsync(update.Message.Chat.Id, update.Message.From.Id, cancellationToken).Result;
                    
                    if (user.setterValueId.From == message.From && user.setterValueId.Chat.Id == message.Chat.Id && (admin.Status != ChatMemberStatus.Administrator || admin.Status != ChatMemberStatus.Creator))
                    {
                      
                        Console.WriteLine("зашел с сообщением - " + message.Text + " " + message.From + " " + admin.Status);

                        await setValue(message);
                    }
                }
                else
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Не удалось записать значение", replyMarkup: new ReplyKeyboardRemove());
                    user.setValueCheck = false;
                }

            }

            await SaveUsers();
            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
        }

        //сохранений изменений состояния чата
        static async Task SaveUsers()
        {
            string jsonPath = JsonConvert.SerializeObject(Users);
            using (StreamWriter sw = new StreamWriter(path, false, System.Text.Encoding.Default))
            {
                sw.WriteLine(jsonPath);
            }
        }

        // Проверка при первом запуске бота в чате
        static async Task CheckUser()
        {
            if (Users.Count == 0)
            {
                Users.Add(new User(0, tmpChatId, false, null));
                await SaveUsers();
            }
            else if (Users.Where(n => n.chatId == tmpChatId).FirstOrDefault() != null)
            {
                Users = JsonConvert.DeserializeObject<List<User>>(System.IO.File.ReadAllText(path));
                Console.WriteLine("Chat id - " + tmpChatId);
            }
            else
            {
                Users.Add(new User(0, tmpChatId, false, null));
                await SaveUsers();
            }
        }

        //обработка состояния чата после записи значения миграции
        public static async Task<Message> setValue(Message message)
        {
            if (long.TryParse(message.Text.TrimStart('№'), out long temp))
            {
                user.migration = temp;
                user.setValueCheck = false;
                await SaveUsers();
                return await Bot.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Последняя миграция - {user.migration}",
                                             replyMarkup: new ReplyKeyboardRemove()); ;
            }
            else
            {

                user.setValueCheck = false;
                return await Bot.SendTextMessageAsync(message.Chat.Id, "Не удалось записать значение", replyMarkup: new ReplyKeyboardRemove()); ;
            }
        }

        //установка состояния чата на запись значения миграции
        public static async Task<Message> GetValue(Message message)
        {
            user.setValueCheck = true;
            user.setterValueId = message;
            Console.WriteLine($"setvaluecheck - {user.setValueCheck}");
            return await Bot.SendTextMessageAsync(message.Chat.Id, "Введите значение миграции (№.....)", replyMarkup: new ReplyKeyboardRemove());
        }

        //получение следующего знечения миграции
        static async Task<Message> NextMigration(Message message)
        {
            user.migration++;
            await SaveUsers();
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                       text: $"Следующая миграция - {user.migration}",
                                                       replyMarkup: new ReplyKeyboardRemove());
        }

        //вывод текущео значения миграции 
        static async Task<Message> ShowMigration(Message message)
        {
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                      text: $"Последняя миграция - {user.migration}",
                                                      replyMarkup: new ReplyKeyboardRemove());
        }

        //заглушка на обработчик событий в методе BotOnMessageReceived в switch
        static async Task<Message> Usage(Message message)
        {
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: "");
        }


        //обработка исключений
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
        private static Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}
