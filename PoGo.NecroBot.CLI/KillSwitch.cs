using System;
using System.Net;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.Utils;

namespace PoGo.NecroBot.CLI
{
    internal static class KillSwitch
    {
        private static readonly Uri _strKillSwitchUri = new Uri("https://raw.githubusercontent.com/NoxxDev/NecroBot/master/KillSwitch.txt");

        internal static bool IsKillSwitchActive()
        {
            using (WebClient webClient = new WebClient())
            {
                string strResponse = WebClientExtensions.DownloadString(webClient, _strKillSwitchUri);

                if (strResponse == null)
                    return false;

                string[] strSplit = strResponse.Split(';');

                if (strSplit.Length > 1)
                {
                    string strStatus = strSplit[0];
                    string strReason = strSplit[1];

                    if (strStatus.ToLower().Contains("disable"))
                    {
                        Console.WriteLine(strReason + Environment.NewLine);

                        Logger.Write("The bot will now close, please press enter to continue", LogLevel.Error);
                        Console.ReadLine();
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            return false;
        }
    }
}
