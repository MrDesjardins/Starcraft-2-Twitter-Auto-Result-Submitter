using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Twitterizer;

namespace Starcraft2.ReplayParser.TestApplication
{
    internal class Program
    {
        private static void Main()
        {
            new Program();
        }

        private string GetConsumerKey()
        {
            return "tgAtjzWZApFnUWENHiFYIg";//ConfigurationManager.AppSettings["ConsumerKey"];
        }

        private string GetConsumerSecret()
        {
            return "BOcMboJQGKWVqmCVqXlIkuAad1ISahOQVCh6bLs1f3A";//ConfigurationManager.AppSettings["ConsumerSecret"];
        }


        public Program()
        {
            CheckTwitterAccess();
            string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string replayLocation = (string)ConfigurationManager.AppSettings["YourReplayFolder"];
            //replayLocation = Path.Combine(appPath, "testReplay.1.3.4.SC2Replay");
            if(string.IsNullOrEmpty(replayLocation))
            {
                replayLocation =  Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"StarCraft II\Accounts\");
            }

            FileSystemWatcher incomingReplay = new FileSystemWatcher();
            incomingReplay.Path = replayLocation;
            incomingReplay.NotifyFilter = NotifyFilters.LastAccess |
                                    NotifyFilters.LastWrite |
                                    NotifyFilters.FileName |
                                    NotifyFilters.DirectoryName;

            incomingReplay.Filter = "*.SC2Replay";
            incomingReplay.IncludeSubdirectories = true;
            incomingReplay.Changed += new FileSystemEventHandler(incomingReplay_Changed);

            incomingReplay.EnableRaisingEvents = true;


            Console.WriteLine("Ready to go. You can now play or press any key to quit.");
            Console.ReadLine();
        }

      
        /// <summary>
        /// Check if Twitter has access. If not, request wizard start.
        /// </summary>
        private void CheckTwitterAccess()
        {
            string accessToken = ConfigurationManager.AppSettings["AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                RequestAccess();
            }
        }

        /// <summary>
        /// This record the last Tweet to make sure it does not spam.
        /// </summary>
        private DateTime lastTweet = DateTime.Now.AddMinutes(-10);

        private void incomingReplay_Changed(object sender, FileSystemEventArgs e)
        {
            if ( ((TimeSpan)(DateTime.Now - lastTweet)).TotalMinutes>=3)//No spam, 3 per minutes max
            {
                TwitReplay(e.FullPath);
                lastTweet = DateTime.Now;
            }
        }

        /// <summary>
        /// Twit the replay status
        /// </summary>
        /// <param name="filePath"></param>
        private void TwitReplay(string filePath)
        {
            ConfigurationManager.RefreshSection("appSettings");//In case user changes some configuration
            Replay replay = Replay.Parse(filePath);
            if (replay.TeamSize == "1v1")
            {
                string myName = ConfigurationManager.AppSettings["BattleNetUserName"];
                string displayString = ConfigurationManager.AppSettings["DisplayString"];
                Player myPlayer = replay.Players.Where(p => p.Name == myName).FirstOrDefault();
                if (myPlayer == null)
                {
                    Console.WriteLine("Your name is not found in the replay, it's not possible to analyze the game. Here are the players name:");
                    foreach (var p in replay.Players)
                    {
                        Console.WriteLine(p.Name);
                    }
                    Console.WriteLine("Change you name in the .config file and play again.");
                    return;
                }
                Player opponent = replay.Players.Where(p => p.Name != myName).FirstOrDefault();

                string message = string.Format(displayString, (myPlayer.IsWinner ? "win" : "loss"), opponent.Name, replay.Map, myPlayer.Name);

                //When the program start. Check if the user is loggued or if access.
                string accessToken = ConfigurationManager.AppSettings["AccessToken"];
                string accessTokenSecret = ConfigurationManager.AppSettings["AccessTokenSecret"];
   
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(accessTokenSecret))
                {
                    RequestAccess();
                   
                    accessToken = ConfigurationManager.AppSettings["AccessToken"];
                    accessTokenSecret = ConfigurationManager.AppSettings["AccessTokenSecret"];
                }

                var tokens = new OAuthTokens
                                 {
                                     AccessToken = accessToken,
                                     AccessTokenSecret = accessTokenSecret,
                                     ConsumerKey = GetConsumerKey(),
                                     ConsumerSecret = GetConsumerSecret()
                                 };

                TwitterResponse<TwitterStatus> tweetResponse = TwitterStatus.Update(tokens, message);
                
                if (tweetResponse.Result == RequestResult.Success)
                {
                    Console.WriteLine("{0}: {1}", DateTime.Now, message);// Tweet posted successfully!
                }
                else if (tweetResponse.Result == RequestResult.Unauthorized)
                {
                    RequestAccess();
                }

            }
        }

        private void RequestAccess()
        {

            Console.WriteLine("You need to grant permission to this application to Tweet to you Twitter account. \n\nThis is required only the first time. A webpage will launch and you will need to get a PIN and write down here.");

            string consumerKey = GetConsumerKey();
            string consumerSecret = GetConsumerSecret();

            OAuthTokenResponse tokenResponse = OAuthUtility.GetRequestToken(consumerKey, consumerSecret, "oob");
            Uri uriForActivation = OAuthUtility.BuildAuthorizationUri(tokenResponse.Token);

            Process.Start(uriForActivation.ToString());//Popup the browser
            Console.Write("Enter the pin id: ");
            string pinNumber = Console.ReadLine();
            try
            {
                OAuthTokenResponse answer = OAuthUtility.GetAccessToken(consumerKey
                                                , consumerSecret
                                                , tokenResponse.Token
                                                , pinNumber);

                Modify("AccessToken", answer.Token);
                Modify("AccessTokenSecret", answer.TokenSecret);
            }
            catch (TwitterizerException te)
            {
                Console.WriteLine("** Error : " + te.Message);
            }
        }


        private static void Modify(string key, string value)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings[key].Value = value;
            configuration.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

    
    }
}