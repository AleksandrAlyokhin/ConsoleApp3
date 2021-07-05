using Newtonsoft.Json;
using ProgramSettings;
using System;
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
        class test
        {
            public long migration { get; set; }
            public test(long tmp)
            {
                migration = tmp;
            }
        }

        private static TelegramBotClient Bot;
        static test tmp;
        const  string path = "gg.json";
        public static long mig = 0;
        public static bool setValueCheck = false;
        public static Message setterValueId;


        public static async Task Main()
        {
            tmp = JsonConvert.DeserializeObject<test>(System.IO.File.ReadAllText(path));
            mig = tmp.migration;
            Bot = new TelegramBotClient(Configuration.BotToken);
            var me = await Bot.GetMeAsync();
            var cts = new CancellationTokenSource();

            Bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync), cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            cts.Cancel();
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(update,update.Message, cancellationToken),
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

        private static async Task BotOnMessageReceived(Update update, Message message, CancellationToken cancellationToken)
        {            
            Console.WriteLine($"Receive message type: {message.Type}");
            Message sentMessage = message;
            if (message.Type == MessageType.Text && message.Text.StartsWith('/'))
            {
                var action = (message.Text.Split('@').First()) switch
                {
                    "/showvalue" => ShowMigration(message),
                    "/nextvalue" => NextMigration(message),
                    "/setvalue" => GetValue(message),
                    _ => Usage(message)
                };
                sentMessage = await action;
            }
            else if (setValueCheck && message.Text.StartsWith('№'))
            {
                var admin = Bot.GetChatMemberAsync(update.Message.Chat.Id, update.Message.From.Id, cancellationToken).Result;
                if (setterValueId.From == message.From && (admin.Status != ChatMemberStatus.Administrator || admin.Status != ChatMemberStatus.Creator))
                {
                    Console.WriteLine($"setvaluecheck - {setValueCheck}");
                    Console.WriteLine("зашел с сообщением - " + message.Text + " " + message.From + " " + admin.Status);
                    if (long.TryParse(message.Text.TrimStart('№'), out long temp))
                    {
                        mig = temp;
                        await Bot.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Последняя миграция - {mig}",
                                                     replyMarkup: new ReplyKeyboardRemove());
                        string json = JsonConvert.SerializeObject(new test(mig));
                        using (StreamWriter sw = new StreamWriter(path, false, System.Text.Encoding.Default))
                        {
                            sw.WriteLine(json);
                        }
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(message.From.Id, "Не удалось записать значение", replyMarkup: new ReplyKeyboardRemove());
                    }                   
                }
                setValueCheck = false;
                return;

            }
            else
            {
                setValueCheck = false;
            }
            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");            
        }

        public static async Task<Message> GetValue(Message message)
        {
            setValueCheck = true;
            setterValueId = message;
            Console.WriteLine($"setvaluecheck - {setValueCheck}");
            return await Bot.SendTextMessageAsync(message.From.Id, "Введите значение миграции (№.....)", replyMarkup: new ReplyKeyboardRemove());
        }

        static async Task<Message> NextMigration(Message message)
        {
            mig++;
            string json = JsonConvert.SerializeObject(new test(mig));
            using (StreamWriter sw = new StreamWriter(path, false, System.Text.Encoding.Default))
            {
                sw.WriteLine(json);
            }
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                       text: $"Следующая миграция - {mig}",
                                                       replyMarkup: new ReplyKeyboardRemove());
        }

        static async Task<Message> ShowMigration(Message message)
        {
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                      text: $"Последняя миграция - {mig}",
                                                      replyMarkup: new ReplyKeyboardRemove());
        }

        static async Task<Message> Usage(Message message)
        {
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: "");
        }

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


