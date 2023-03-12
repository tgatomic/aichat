using Microsoft.JSInterop;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGpt.Data
{

    public class ChatCompletionChunk
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("choices")]
        public Choice[] Choices { get; set; }
    }

    public class Choice
    {
        [JsonPropertyName("delta")]
        public Delta Delta { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    public class Delta
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    class GptMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }
    }

    public class GptInterface
    {
        public event EventHandler<string>? NewChunkReceivedEvt;
        public event EventHandler<string>? NewChatNameChunkReceivedEvt;

        public async Task GetChatCompletionStream(List<Message> messages)
        {
            List<GptMessage> gptMessages = new List<GptMessage>
            {
                new GptMessage()
                {
                    Role = "system",
                    Content = "You are a helpful assistant."
                }
            };


            foreach (Message message in messages)
            {
                if (message.User)
                {
                    gptMessages.Add(new GptMessage()
                    {
                        Role = "user",
                        Content = message.Content
                    });
                } else
                {
                    gptMessages.Add(new GptMessage()
                    {
                        Role = "assistant",
                        Content = message.Content
                    });
                }
            }

            using (var client = new HttpClient())
            {
                var requestData = new
                {
                    model = "gpt-3.5-turbo",
                    messages = gptMessages,
                    stream = true
                };

                string testData = JsonSerializer.Serialize(requestData);

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Add("Authorization", "Bearer ChatGPT-TOKEN");
                request.Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                var stream = await response.Content.ReadAsStreamAsync();
                var streamReader = new StreamReader(stream);


                while (!streamReader.EndOfStream)
                {
                    var line = await streamReader.ReadLineAsync();

                    if (line == "data: [DONE]")
                    {
                        // End of the SSE stream
                        NewChunkReceivedEvt?.Invoke(null, "data: [DONE]");
                        break;
                    }
                    else if (line.StartsWith("data: "))
                    {
                        var json = line.Substring("data: ".Length);
                        var chatCompletionChunk = JsonSerializer.Deserialize<ChatCompletionChunk>(json);

                        string chunk = chatCompletionChunk.Choices[0].Delta.Content;
                        NewChunkReceivedEvt?.Invoke(null, chunk);
                        await Task.Delay(10);
                    }
                }
            }
        }

        public async Task GetChatName(List<Message> messages)
        {
            List<GptMessage> gptMessages = new List<GptMessage>
            {
                new GptMessage()
                {
                    Role = "system",
                    Content = "You are a helpful assistant."
                }
            };

            foreach (Message message in messages)
            {
                if (message.User)
                {
                    gptMessages.Add(new GptMessage()
                    {
                        Role = "user",
                        Content = message.Content
                    });
                }
                else
                {
                    gptMessages.Add(new GptMessage()
                    {
                        Role = "assistant",
                        Content = message.Content
                    });
                }
            }

            gptMessages.Add(new GptMessage()
            {
                Role = "user",
                Content = "Can you give me a short title for this conversation?"
            });


            using (var client = new HttpClient())
            {
                var requestData = new
                {
                    model = "gpt-3.5-turbo",
                    messages = gptMessages,
                    stream = true
                };

                string testData = JsonSerializer.Serialize(requestData);

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Add("Authorization",  "Bearer ChatGPT-TOKEN");
                request.Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                var stream = await response.Content.ReadAsStreamAsync();
                var streamReader = new StreamReader(stream);


                while (!streamReader.EndOfStream)
                {
                    var line = await streamReader.ReadLineAsync();

                    if (line == "data: [DONE]")
                    {
                        // End of the SSE stream
                        NewChatNameChunkReceivedEvt?.Invoke(null, "data: [DONE]");
                        break;
                    }
                    else if (line.StartsWith("data: "))
                    {
                        var json = line.Substring("data: ".Length);
                        var chatCompletionChunk = JsonSerializer.Deserialize<ChatCompletionChunk>(json);

                        string chunk = chatCompletionChunk.Choices[0].Delta.Content;
                        NewChatNameChunkReceivedEvt?.Invoke(null, chunk);
                        await Task.Delay(10);
                    }
                }
            }
        }


    }
}
