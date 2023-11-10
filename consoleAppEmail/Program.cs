using System;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using static Microsoft.Graph.CoreConstants;
using System.Net.Http.Headers;
using Newtonsoft.Json;

class Program
{
    static async Task Main(string[] args)
    {

        var settings = Settings.LoadSettings();

        InitializeGraph(settings);

        // List emails from user's inbox
        await ListInboxAsync();
                 
                
        

        void InitializeGraph(Settings settings)
        {
            GraphHelper.InitializeGraphForUserAuth(settings,
                (info, cancel) =>
                {
                    // Display the device code message to
                    // the user. This tells them
                    // where to go to sign in and provides the
                    // code to use.
                    Console.WriteLine(info.Message);
                    return Task.FromResult(0);
                });
        }

        

        //async Task DisplayAccessTokenAsync()
        //{
        //    try
        //    {
        //        var userToken = await GraphHelper.GetUserTokenAsync();
        //        Console.WriteLine($"User token: {userToken}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error getting user access token: {ex.Message}");
        //    }
        //}

        async Task ListInboxAsync()
        {
            try
            {
                var messagePage = await GraphHelper.GetInboxAsync();

                if (messagePage?.Value == null)
                {
                    Console.WriteLine("No results returned.");
                    return;
                }

                string apiSecret = "b762b148e19845b";

                // Output each message's details
                foreach (var message in messagePage.Value)
                {
                    Console.WriteLine($"Message: {message.Subject ?? "NO SUBJECT"}");
                    Console.WriteLine($"  From: {message.From?.EmailAddress?.Name}");
                    Console.WriteLine($"  Status: {(message.IsRead!.Value ? "Read" : "Unread")}");
                    Console.WriteLine($"  Received: {message.ReceivedDateTime?.ToLocalTime().ToString()}");

                    // Generate a new bearer token for each request
                    string bearerToken = GenerateBearerToken(apiSecret);

                    // Create a JSON object with message details
                    var messageDetails = new
                    {
                        Subject = message.Subject ?? "NO SUBJECT",
                        From = message.From?.EmailAddress?.Name ?? "Unknown",
                        Status = message.IsRead!.Value ? true : false,
                        Body = "Your message body here",
                        // Add other properties as needed
                    };

                    // Convert the messageDetails object to JSON
                    string jsonBody = JsonConvert.SerializeObject(messageDetails);

                    // Send a POST request to the webhook API
                    await SendPostRequestToWebhook(jsonBody, bearerToken);
                }

                // If NextPageRequest is not null, there are more messages
                // available on the server
                // Access the next page like:
                // var nextPageRequest = new MessagesRequestBuilder(messagePage.OdataNextLink, _userClient.RequestAdapter);
                // var nextPage = await nextPageRequest.GetAsync();
                var moreAvailable = !string.IsNullOrEmpty(messagePage.OdataNextLink);

                Console.WriteLine($"\nMore messages available? {moreAvailable}");
                Console.Read();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user's inbox: {ex.Message}");
            }

            string GenerateBearerToken(string apiSecret)
            {
                // Construct the URL with the API secret as a query parameter
                string getTokenUrl = $"https://integrations.kwixee.co.in/api/Integration/GetToken?SecretKey={apiSecret}";

                using (HttpClient client = new HttpClient())
                {
                    // Make a GET request to the URL
                    HttpResponseMessage response = client.GetAsync(getTokenUrl).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        // Read and parse the response content
                        string responseContent = response.Content.ReadAsStringAsync().Result;

                        // Use a JSON parser to extract the access_token from the response
                        dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);
                        string bearerToken = jsonResponse.access_token;

                        Console.WriteLine(bearerToken);
                        return bearerToken;
                    }
                    else
                    {
                        Console.WriteLine($"Error getting bearer token. Status code: {response.StatusCode}");
                        return null;
                    }
                }



            }

            async Task SendPostRequestToWebhook(string jsonBody, string bearerToken)
            {

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                    HttpContent content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync("https://integrations.kwixee.co.in/dev/api/Integration/SaveAppData", content);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("POST request successful.");
                    }
                    else
                    {
                        Console.WriteLine(response);
                        Console.WriteLine("POST request failed.");
                    }
                }
            }

        }


    }

}
