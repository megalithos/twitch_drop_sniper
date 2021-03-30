using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using System.Threading;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Support;
using OpenQA.Selenium.Interactions;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

/*
 * FOR README SEE README.MD
 */

namespace rust_auto_drop
{
    class Program
    {
        #region forHidingConsole
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        #endregion

        #region constants
        const string TWITCH_URL = "https://www.twitch.tv";
        const string DROPS_URL = "https://twitch.facepunch.com/";
        const string INVENTORY_URL = "https://www.twitch.tv/drops/inventory";
        const string START_WATCHING_XPATH = "//*[@id=\"root\"]/div/div[2]/div[2]/main/div[2]/div[3]/div/div/div[2]/div/div[2]/div/div/div/div[5]/div/div[3]/button";
        static readonly string CLAIMED_STREAMERS_LIST_PATH = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/claimed_streamers.txt";
        static readonly string CRASH_DIRECTORY_PATH = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/crash_reports";
        const string CLAIM_BUTTON_FORMAT_XPATH = "//*[@id=\"root\"]/div/div[2]/div/main/div[2]/div[3]/div/div/div/div/div/div/div[2]/div[{0}]/div[2]/div/div[1]/div[1]/div/div/div[1]/div[2]/button";
        const string YOU_ARE_EARNING_TOWARD = "//*[@id=\"root\"]/div/div[2]/div/main/div[2]/div[3]/div/div/div/div/div/div/div[2]/div[*]/div[1]/div[3]/div[2]/div/p/a";
        const string ACTIVE_STREAMERS_ON_FACEPUNCH_PAGE = "/html/body/section[1]/div/div[2]/a[*]";

        // collection of buttons to click (by xpath) that we need in order to set the resolution to lowest possible.
        static readonly List<string> SETTINGS_BUTTON_CLICKS_XPATHS = new List<string>
        {
            "//*[@id=\"root\"]/div/div[2]/div/main/div[2]/div[3]/div/div/div[2]/div/div[2]/div/div/div/div[2]",
            "/html/body/div[1]/div/div[2]/div[2]/main/div[2]/div[3]/div/div/div[2]/div/div[2]/div/div/div/div[5]/div/div[2]/div[2]/div[1]/div[2]/div/button",
            "//*[@id=\"root\"]/div/div[2]/div[2]/main/div[2]/div[3]/div/div/div[2]/div/div[2]/div/div/div/div[5]/div/div[2]/div[2]/div[1]/div[1]/div/div/div/div/div/div/div[3]/button",
            "//*[@id=\"root\"]/div/div[2]/div/main/div[2]/div[3]/div/div/div[2]/div/div[2]/div/div/div/div[7]/div/div[2]/div[2]/div[1]/div[1]/div/div/div/div/div/div/div[8]",
        };
        const int RESET_STREAMS_AFTER_SECONDS = 300;
        #endregion

        static List<string> liveStreamerURLs = new List<string>();
        static IWebDriver driver;
        static string token;
        static Form1 mainForm;
        static bool hiddenMode;
        static IWebDriver streamWatcherDriver;
        static List<string> progressedStreamersThisRound = new List<string>();

        private static int maxStreamsAtOnce;

        [STAThread]
        static void Main()
        {
            IntPtr handle = GetConsoleWindow();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Thread.CurrentThread.IsBackground = true;
            mainForm = new Form1();
            Application.Run(mainForm);
        }

        public static void MainLoop(string tok, bool _hiddenMode, int _maxStreamsAtOnce)
        {
            hiddenMode = _hiddenMode;
            token = tok;

            /* only watch one streamer at once. why you may ask, well it does not matter whether you watch 1 or 5 streamers at the same time.
             also if watching a streamer whose drop you have claimed, you will only lose 5 minutes (rare case) -> still much more useful than hand picking streamers to watch */
            maxStreamsAtOnce = 1;
            mainForm.SetStatus("starting...");

            Console.WriteLine($"hiddenMode:{hiddenMode}, token:{token}, maxStreamsAtOnce:{maxStreamsAtOnce}");

            try
            {
                while (true)
                {
                    mainForm.ClearWatchingStreamersText();
                    driver = StartNewDriver();
                    CheckNotClaimed();
                    CheckAlreadyClaimed();
                    liveStreamerURLs.Clear();
                    FetchStreams();
                    QuitDriver();
                    OpenAllStreams();
                    mainForm.SetStatus("waiting until next cycle...");
                    Thread.Sleep(RESET_STREAMS_AFTER_SECONDS * 1000);
                }
            } catch (Exception e)
            {
                OutputCrashMessage(e.Message);
                mainForm.Exit();
            }
        }

        #region important methods
        /// <summary>
        /// Add those streamers to the claimed list that didn't pop into the
        /// twitch inventory after we watched them. (They didn't pop up because we already before watched claimed their drops!)
        /// 
        /// This method is useful so we don't keep watching streamers whose drops we have already claimed, and the users only watch them
        /// for one cycle, after which they are added to the claim list so they won't be watched again. So user basically won't notice anything
        /// and the program will itself add the streamer to the claimed list.
        /// </summary>
        static void CheckAlreadyClaimed()
        {
            string[] claimed = File.ReadAllLines(CLAIMED_STREAMERS_LIST_PATH);

            foreach (string url in liveStreamerURLs)
            {
                if (!progressedStreamersThisRound.Contains(url) && !claimed.Contains<string>(url))
                {
                    File.AppendAllText(CLAIMED_STREAMERS_LIST_PATH, url + System.Environment.NewLine);
                }
            }
        }

        // indexes start at 2.
        static void CheckNotClaimed()
        {
            mainForm.SetStatus("checking unclaimed drops...");
            driver.Navigate().GoToUrl(INVENTORY_URL);
            driver.Manage().Cookies.AddCookie(new Cookie("auth-token", token));
            driver.Navigate().Refresh();
            Thread.Sleep(5000);
            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> you_are_earning_towards = driver.FindElements(By.XPath(YOU_ARE_EARNING_TOWARD));

            // for every streamer that we have progress bar for a drop -> add the streamer's url to this list
            progressedStreamersThisRound = new List<string>();
            foreach (IWebElement element in you_are_earning_towards)
            {
                progressedStreamersThisRound.Add(element.GetAttribute("href"));
            }
            
            int i = 2;
            foreach (IWebElement element in you_are_earning_towards)
            {
                try // try to claim the drop
                {
                    Console.Write("Trying: " + string.Format(CLAIM_BUTTON_FORMAT_XPATH, i));
                    IWebElement claimButtonElement = driver.FindElement(By.XPath(string.Format(CLAIM_BUTTON_FORMAT_XPATH, i)));
                    claimButtonElement.Click(); // UNCOMMENT THIS LINE ON RELEASE
                    string streamerUrl = progressedStreamersThisRound[i - 2];
                    Console.WriteLine("============================\nCLICK..." + streamerUrl);
                    File.AppendAllText(CLAIMED_STREAMERS_LIST_PATH, streamerUrl + System.Environment.NewLine);
                }
                catch (OpenQA.Selenium.NotFoundException) { Console.WriteLine("Claim button not found on element number: " + i); }
                i++;
            }
        }
        
        static void FetchStreams()
        {
            mainForm.SetStatus("fetching streams...");
            driver.Navigate().GoToUrl(DROPS_URL);
            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> activeStreams = driver.FindElements(By.XPath(ACTIVE_STREAMERS_ON_FACEPUNCH_PAGE));

            string[] claimedStreamers = null;
            if (File.Exists(CLAIMED_STREAMERS_LIST_PATH))
            {
                claimedStreamers = File.ReadAllLines(CLAIMED_STREAMERS_LIST_PATH);
            }
            else
                File.Create(CLAIMED_STREAMERS_LIST_PATH);

            foreach (IWebElement webElement in activeStreams)
            {
                string val = webElement.GetAttribute("class");
                
                // determine if the drop is live from the facepunch site
                if (val == "drop is-live" || val == "drop two-column is-live") // second option if they put 2 drops for one streamer so it will detect it
                {
                    string url = (webElement.GetAttribute("href"));
                    url = url.ToLower();
                    // don't add streamers to the list whose drops we have already claimed
                    if (!claimedStreamers.Contains<string>(url))
                    {
                        liveStreamerURLs.Add(url);
                        Console.WriteLine("ADD: " + url);
                    }
                }
            }
        }

        static void OpenAllStreams()
        {
            if (streamWatcherDriver != null)
            {
                streamWatcherDriver.Quit();
            }

            if (liveStreamerURLs.Count == 0)
            {
                Console.WriteLine("No one is streaming at the moment.");
                if (streamWatcherDriver != null) streamWatcherDriver.Quit();
                return;
            }

            mainForm.SetStatus("opening streams...");
            streamWatcherDriver = StartNewDriver();
            streamWatcherDriver.Navigate().GoToUrl(TWITCH_URL);
            streamWatcherDriver.Manage().Cookies.AddCookie(new Cookie("auth-token", token));
            streamWatcherDriver.Navigate().Refresh();
            streamWatcherDriver.Navigate().GoToUrl(liveStreamerURLs[0]); // go to the first stream on this window
            mainForm.AppendWatchingStreamersText(liveStreamerURLs[0] + "\n");

            WebDriverExtensions.FindElementAndClickIt(streamWatcherDriver, By.XPath(START_WATCHING_XPATH), 5);
            /*for (int i = 0; i < liveStreamerURLs.Count; i++) 
            {
                if (i == 0)
                {
                    
                    continue;
                }

                if (watchingCount == maxStreamsAtOnce)
                    return;

                string url = liveStreamerURLs[i];

                ((IJavaScriptExecutor)streamWatcherDriver).ExecuteScript("window.open()");
                streamWatcherDriver.SwitchTo().Window(streamWatcherDriver.WindowHandles[streamWatcherDriver.WindowHandles.Count - 1]);
                streamWatcherDriver.Navigate().GoToUrl(url);
                mainForm.AppendWatchingStreamersText(url + "\n");
                WebDriverExtensions.FindElementAndClickIt(streamWatcherDriver, By.XPath(START_WATCHING_XPATH), 5);
                watchingCount++;
            }*/
        }

        static IWebDriver StartNewDriver()
        {
            ChromeOptions o = new ChromeOptions();
            o.AddArgument("--mute-audio");

            IWebDriver myDriver;
            if (hiddenMode)
            {
                o.AddArgument("--headless");
                o.AddArgument("--disable-gpu");
                o.AddArgument("disable-infobars"); // disabling infobars
                o.AddArgument("--disable-extensions"); // disabling extensions
                o.AddArgument("--disable-dev-shm-usage"); // overcome limited resource problems
                o.AddArgument("--no-sandbox"); // Bypass OS security model

                ChromeDriverService service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                myDriver = new ChromeDriver(service, o);
            }
            else
            {
                myDriver = new ChromeDriver(o);
            }


            return myDriver;
        }
        #endregion

        public static void QuitDriver()
        {
            if (driver == null) return;
            driver.Quit();
        }

        public static void QuitStreamWatcherDriver()
        {
            if (streamWatcherDriver == null) return;
            streamWatcherDriver.Quit();
        }

        public static void CleanupAllChromedriverProcesses()
        {
            Process[] chromeDriverProcesses = Process.GetProcessesByName("chromedriver");

            foreach (var chromeDriverProcess in chromeDriverProcesses)
            {
                chromeDriverProcess.Kill();
            }
        }

        #region reportingCrashesToFile
        /// <summary>
        /// Create a file with a specified crash message in it.
        /// </summary>
        /// <param name="message"></param>
        public static void OutputCrashMessage(string message)
        {
            if (!Directory.Exists(CRASH_DIRECTORY_PATH))
                Directory.CreateDirectory(CRASH_DIRECTORY_PATH);
            
            string path = CRASH_DIRECTORY_PATH + "/" + "crash_" + RandomString(20) + ".txt";

            File.WriteAllText(path, message);
        }

        private static Random random = new Random();

        public static string RandomString(int length)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            chars = chars.ToLower();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        #endregion
    }

    public static class WebDriverExtensions
    {
        public static IWebElement FindElementAndClickIt(this IWebDriver driver, By by, int timeoutInSeconds)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
                IWebElement element = wait.Until(ExpectedConditions.ElementToBeClickable(by));
                element.Click();
                Console.WriteLine("LOADED:!:!:!:! \n"); 

            }
            catch (Exception e) { Console.WriteLine(e.Message);  }
                
            return null;
        }
    }
}