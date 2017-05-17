using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using EmotionalNewsBot.Models;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;

namespace LuisBot.Dialogs
{
    //Finall CODE! 
    [Serializable]
    [LuisModel("90248cbc-723c-425c-9789-8e6c6e5ecfa5", "b92a6603cabe45989d8eda44a817d01b")]
    public class RootDialog : LuisDialog<object>
    {
        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string response = $"Sorry, I did not understand '{result.Query}'. Type 'help' if you need assistance.";
            await context.PostAsync(response);
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("News")]
        public async Task Search(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            var message = await activity;
            var reply = context.MakeMessage();
            EntityRecommendation newsEntity, sentimentEntity;

            if (result.TryFindEntity("NewsTopic", out newsEntity))
            {
                var findPositive = result.TryFindEntity("PositiveSentiment", out sentimentEntity);
                var findNegative = result.TryFindEntity("NegativeSentiment", out sentimentEntity);

                await context.PostAsync((findPositive ? "positive " : (findNegative ? "negative " : "")) +
                                         "news about '" + newsEntity.Entity + "' coming right up \U0001F680!");

                BingNews bingNews = await getBingNews(newsEntity.Entity);

                if (bingNews == null || bingNews.totalEstimatedMatches == 0)
                {
                    reply.Text = "Sorry, couldn't find any news about '" + newsEntity.Entity + "' \U0001F61E.";
                }
                else
                {
                    reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    reply.Attachments = new List<Attachment>();
                }

                for (int i = 0; i < 10 && i < (bingNews?.totalEstimatedMatches ?? 0); i++)
                {
                    var article = bingNews.value[i];
                    var sentimentScore = await getSentimentScore(article.description);

                    if (findPositive && sentimentScore < 0.6)
                    {
                        continue;
                    }
                    else if (findNegative && sentimentScore > 0.4)
                    {
                        continue;
                    }

                    HeroCard attachment = new HeroCard()
                        {
                            Title = article.name.Length > 60 ? article.name.Substring(0, 57) + "..." : article.name,
                            Subtitle = getSentimentLabel(sentimentScore),
                            Text = article.provider[0].name + ", " + article.datePublished.ToString("d") + " - " + article.description,
                            Images = new List<CardImage>() { new CardImage(article.image?.thumbnail?.contentUrl + "&w=400&h=400") },
                            Buttons = new List<CardAction>() { new CardAction(
                                                                 ActionTypes.OpenUrl, 
                                                                 title: "View on Web", 
                                                                 value: ReplaceFirst(article.url, "http", "https")) }
                        };
                    reply.Attachments.Add(attachment.ToAttachment());           
                }       
            }
            else
            {
                reply.Text = $"I understand you want to search for news, but I couldn't understand the topic you're looking for \U0001F633. ";
                reply.Text += $"Rephrase your question or re-train your LUIS model!";
            }
            await context.PostAsync(reply);
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            string response = $"I can help you find the freshest news articles \U0001F4F0 around the world, ";
            response += $"and give you a hint about each article's sentiment based on its description. ";
            response += $"Here are a few things you can try: \n\n-\n";
            response += $"* find good news about microsoft\r"; 
            response += $"* show me bad news about global warming\r";
            response += $"* positive press about the elections\n-\n";
            response += $"Tip: if I don't find exactly what you're looking for, try re-training my LUIS model";
            await context.PostAsync(response);               
            context.Wait(this.MessageReceived);
        }

        private async Task<BingNews> getBingNews(string query)
        {
            BingNews bingNews;
            string bingUri = "https://api.cognitive.microsoft.com/bing/v5.0/news/search/?count=50&q=" + query;
            string rawResponse;

            HttpClient httpClient = new HttpClient() {
                DefaultRequestHeaders = {
                    {"Ocp-Apim-Subscription-Key", "d379cb484f2c47baacea28384536c530"},
                    {"Accept", "application/json"}
            }};

            try
            {
                rawResponse = await httpClient.GetStringAsync(bingUri);
                bingNews = JsonConvert.DeserializeObject<BingNews>(rawResponse);
            }
            catch (Exception e)
            {
                return null;
            }
            return bingNews;
        }

        private async Task<double> getSentimentScore(string documentText)
        {
            string queryUri = "https://westus.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment";

            HttpClient client = new HttpClient() {
                DefaultRequestHeaders = { {"Ocp-Apim-Subscription-Key", "9d18c0ef2db140148a29b931a1049ad4"},
                                          {"Accept", "application/json"}
            }};

            var textInput = new BatchInput {
                documents = new List<DocumentInput> {
                    new DocumentInput {
                        id = 1,
                        text = documentText,
            }}};

            var jsonInput = JsonConvert.SerializeObject(textInput);
            HttpResponseMessage postMessage; 
            BatchResult response;

            try
            {
                postMessage = await client.PostAsync(queryUri, new StringContent(jsonInput, Encoding.UTF8, "application/json"));
                response = JsonConvert.DeserializeObject<BatchResult>(await postMessage.Content.ReadAsStringAsync());
            }
            catch (Exception e)
            {
                return 0.0;
            }
            return response?.documents[0]?.score ?? 0.0;
        }

        private string getSentimentLabel(double sentimentScore)
        {
            string message;

            if (sentimentScore <= 0.1)
                message = $"Extremely Negative :@";
            else if (sentimentScore <= 0.2)
                message = $"Very Negative (facepalm)";
            else if (sentimentScore < 0.4)
                message = $"Negative :(";
            else if (sentimentScore <= 0.6)
                message = $"Neutral :^)";
            else if (sentimentScore <= 0.8)
                message = $"Positive :P";
            else if (sentimentScore < 0.9)
                message = $"Very Positive :D";
            else
                message = $"Extremely Positive (heart)";

            message += " (" + (int)(sentimentScore * 100) + "%)";
            return message;
        }

        private string ReplaceFirst(string text, string search, string replace)
        {
            if (text.StartsWith("https"))
                return text;

            int pos = text.IndexOf(search);
            if (pos < 0)
                return text;

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }
}