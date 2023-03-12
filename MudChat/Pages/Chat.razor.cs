using Microsoft.AspNetCore.Components;
using ChatGpt.Data;
using Microsoft.JSInterop;
using System.Reflection.Metadata;
using MudBlazor;
using Microsoft.AspNetCore.Components.Web;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using AI.Dev.OpenAI.GPT;
using System.Collections.Generic;
using System;
using static MudBlazor.CategoryTypes;
using Newtonsoft.Json;
using static System.Net.WebRequestMethods;
using System.Net.NetworkInformation;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Reflection.PortableExecutable;
using static OpenAI.GPT3.ObjectModels.SharedModels.IOpenAiModels;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Formatting;
using Markdig.Syntax;
using System.Runtime.CompilerServices;


namespace ChatGpt.Pages
{
    public class ConversationWrapper
    {
        public string User { get; set; }
        public List<Conversation> Conversations { get; set; }
    }
    public partial class Chat
    {
        public List<Conversation> ConversationHistory = new List<Conversation>();

        private MudTextField<string>? inputField;
        private List<Message> messages = new();
        private GptInterface gptInterface = new GptInterface();

        public string TokensUsed { get; set; } = "Tokens used: 0";
        [Parameter] public string CurrentMessage { get; set; }
        [Parameter] public string CreatedAt { get; set; }
        [Parameter] public string ContactId { get; set; }

        private string currentChatId = "";
        private bool stillThinking = true;
        private bool firstMessage = true;

        string chatName = "";
        bool firstChatNameChunk = true;
        bool titleGenerated = false;

        private async Task ThinkingDots()
        {
            int dots = 0;
            while (stillThinking)
            {
                messages[messages.Count - 1].Content += '.';
                dots += 1;
                if (dots > 5)
                {
                    dots = 0;
                    messages[messages.Count - 1].Content = "Thinking";
                }

                StateHasChanged();
                await Task.Delay(1000);
            }
        }

        async Task<int> GetNumberOfTokens()
        {
            int tokens = 0;
            foreach (Message msg in messages)
            {
                tokens += GPT3Tokenizer.Encode(msg.Content).Count;
            }

            return tokens;
        }

        async Task HandleTokenLimit()
        {
            int tokens = await GetNumberOfTokens();
            TokensUsed = $"Tokens used: {await GetNumberOfTokens()}";

            if (tokens > 4090)
            {
                while(tokens > 4090)
                {
                    messages.RemoveAt(0);
                    tokens = await GetNumberOfTokens();
                }
            }
        }

        async Task DisplayTokensUsed()
        {
            TokensUsed = $"Tokens used: {await GetNumberOfTokens()}";
        }

        [JSInvokable]
        private async Task SubmitAsync()
        {
            Message question = new()
            {
                Content = CurrentMessage,
                CreatedAt = DateTime.Now,
                User = true
            };
            messages.Add(question);
            CurrentMessage = "";

            Message responseMessage = new Message()
            {
                Content = "Thinking",
                CreatedAt = DateTime.Now,
                User = false
            };
            messages.Add(responseMessage);

            StateHasChanged();
            await jsRuntime.InvokeAsync<string>("window.ScrollToBottom", "chatContainer");

            // Ensure we're not above ChatGpt token limit
            await HandleTokenLimit();

            if (titleGenerated == false && messages.Count > 5)
            {
                firstChatNameChunk = true;
                await gptInterface.GetChatName(messages);
            }

            StateHasChanged();
            await jsRuntime.InvokeAsync<string>("window.ScrollToBottom", "chatContainer");

            stillThinking = true;
            firstMessage = true;

            _ = Task.Factory.StartNew(() => { _ = ThinkingDots(); });

            await gptInterface.GetChatCompletionStream(messages);
        }

        [JSInvokable]
        private async void NewChunkReceived(object? sender, string chunk)
        {
            if (!String.IsNullOrEmpty(chunk) && chunk != "data: [DONE]")
            {
                if (firstMessage)
                {
                    firstMessage = false;
                    messages[messages.Count - 1].Content = "";
                    stillThinking = false;
                }

                Message tmpMessage = messages[messages.Count - 1];
                messages.Remove(tmpMessage);
                StateHasChanged();

                tmpMessage.Content += chunk;
                messages.Add(tmpMessage);

                StateHasChanged();
                await jsRuntime.InvokeAsync<string>("window.ScrollToBottom", "chatContainer");
            }
            else if (chunk == "data: [DONE]")
            {
                Console.WriteLine(messages[messages.Count - 1].Content);

                Conversation? chatHistory = ConversationHistory.Where(x => x.Id == currentChatId).FirstOrDefault();
                int index = ConversationHistory.IndexOf(chatHistory);
                ConversationHistory[index].Messages = messages;

                StateHasChanged();
                await jsRuntime.InvokeAsync<string>("window.ScrollToBottom", "chatContainer");

                SaveHistory();
            }
        }

        private async void NewChatTitleChunkReceived(object? sender, string chunk)
        {
            if (chunk != "" && chunk != "data: [DONE]")
            {
                Conversation? chatHistory = ConversationHistory.Where(x => x.Id == currentChatId).FirstOrDefault();
                int index = ConversationHistory.IndexOf(chatHistory);

                if (firstChatNameChunk) {
                    ConversationHistory[index].Title = "";
                    firstChatNameChunk = false;

                    if (chunk == "\"")
                    {
                        return;
                    }
                }

                ConversationHistory[index].Title += chunk;
                chatName += chunk;

                StateHasChanged();
            } 
            else if (chunk == "data: [DONE]")
            {
                Conversation? chatHistory = ConversationHistory.Where(x => x.Id == currentChatId).FirstOrDefault();
                int index = ConversationHistory.IndexOf(chatHistory);
                ConversationHistory[index].Title.Remove(ConversationHistory[index].Title.Length - 1, 0);
                ConversationHistory[index].Title.Remove(0, 1);
                ConversationHistory[index].GeneratedTitle = true;
                titleGenerated = true;

                StateHasChanged();
                await SaveHistory();
            }
        }

        bool shiftPressed = false;

        private async void HandleKeyUp(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && shiftPressed == false)
            {
                CurrentMessage = inputField.Value;
                SubmitAsync();
            } else if (e.Key == "Shift") {
                shiftPressed = false;
            }
        }

        private async void HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Shift")
            {
                shiftPressed = true;
            }
        }

        private string GenerateId()
        {
            // https://stackoverflow.com/questions/11313205/generate-a-unique-id
            StringBuilder builder = new();
            Enumerable
               .Range(65, 26)
                .Select(e => ((char)e).ToString())
                .Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
                .Concat(Enumerable.Range(0, 10).Select(e => e.ToString()))
                .OrderBy(e => Guid.NewGuid())
                .Take(11)
                .ToList().ForEach(e => builder.Append(e));
            return builder.ToString();
        }

        private async Task NewChatAsync()
        {
            ConversationHistory.ForEach(x => x.Selected = false);

            Conversation newUser = new()
            {
                Title = "New Chat",
                Id = GenerateId(),
                Selected = true,
                CreatedAt = DateTime.Now,
                Messages = new(),
                GeneratedTitle = false
            };

            ConversationHistory.Add(newUser);

            await LoadUserChat(newUser.Id);
        }

        public async Task LoadHistoryAsync()
        {
            var user = "example_user";
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"http://localhost:64020/history/{user}")
            };
            request.Headers.Add("Accept", "application/json");

            var client = ClientFactory.CreateClient();

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var content = await response.Content.ReadAsAsync<string>();

                    var savedContent = JsonConvert.DeserializeObject<ConversationWrapper>(content);
                    ConversationHistory = savedContent.Conversations;
                } catch
                {
                    Console.WriteLine("User file not found!");
                    ConversationHistory = new();
                }
            }
        }

        private async Task SaveHistory()
        {
            ConversationWrapper wrapper = new ConversationWrapper();
            wrapper.User = currentChatId;
            wrapper.Conversations = ConversationHistory;

            string jsonPayload = JsonConvert.SerializeObject(wrapper);
            var user = "example_user"; 

            var client = ClientFactory.CreateClient();

            var uri = new Uri($"http://localhost:64020/history/{user}");
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "Content-Type");
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Request failed with status code: " + response.StatusCode);
            }
        }

        [JSInvokable]
        protected override async Task OnInitializedAsync()
        {
            await LoadHistoryAsync();

            if (ConversationHistory.Count == 0)
            {
                await NewChatAsync();
            }

            gptInterface.NewChunkReceivedEvt += NewChunkReceived;
            gptInterface.NewChatNameChunkReceivedEvt += NewChatTitleChunkReceived;

            await LoadUserChat(ConversationHistory[0].Id);
        }

        async Task LoadUserChat(string chatId)
        {
            currentChatId = chatId;
            ConversationHistory.ForEach(x => x.Selected = false);

            Conversation? chatHistory = ConversationHistory.Where(x => x.Id == chatId).FirstOrDefault();
            if(chatHistory != null)
            {
                int index = ConversationHistory.IndexOf(chatHistory);
                ConversationHistory[index].Selected = true;
                titleGenerated = ConversationHistory[index].GeneratedTitle;

                if (chatHistory == null) {
                    throw new Exception($"Couldn't find chatid {chatId}");    
                }
            
                messages = chatHistory.Messages;

                _navigationManager.NavigateTo($"chat/{chatId}");

                // Gets the token-count for current coversation for displaying on page
                DisplayTokensUsed();

                StateHasChanged();
                jsRuntime.InvokeAsync<string>("window.ScrollToBottom", "chatContainer");
            } 
            else
            {
                Console.WriteLine($"Couldn't find chat with id {chatId}");
            }
        }

        async Task DeleteConversation(string userId)
        {
            var itemToRemove = ConversationHistory.Single(r => r.Id == userId);
            ConversationHistory.Remove(itemToRemove);
            StateHasChanged();

            if (ConversationHistory.Count > 0)
            {
                await LoadUserChat(ConversationHistory[0].Id);
            } else
            {
                // Generate new empty chat
                await NewChatAsync();
            }

            await SaveHistory();
        }
    }
}
