using EyeXFramework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Tobii.EyeX.Framework;
/*
 BEFORE THE START:
 * (1) Set the path to the screenshot folder in the static field variable 'root' of the class Program and in 
 *     function Test1. 
 * (2) Set the desired driver in the static field variable 'driver' of the class Program (line 358) and in 
 *     function Capture (line 73). Options are: ChromeDriver, FirefoxDriver, InternetExplorerDriver, ... 
 *     (see http://www.seleniumhq.org/projects/webdriver/). When new WebDriver is created add the path to the driver 
 *     in method 'Capture' (see line 74).
 * (3) Set the desired starting URL in method Capture().
 * (4) Needed references: EyeXFramework, Microsoft.CSharp, System.Core, System.Data, System.Data.DataSetExtensions, 
 *     System.Drawing, System.Xml, System.Xml.Linq, Tobii.EyeX.Client.Net20, Newtonsoft.Json, WebDriver and 
 *     WebDriver.Support. Last three can be obtained by using Nudget packages 
 *     (see https://visualstudiogallery.msdn.microsoft.com/4ec1526c-4a8c-4a84-b702-b21a8f5293ca). 
 *     Once Nudget packages are installed one can access them by right clicking on the project 'References' and then 
 *     chosing option 'Manage Nudget packages...'. Use online check.
 * (5) Used package in the solution: PhantomJS.2.0.0.
  
 
 PROGRAM DESCRIPTION: 
 * Method Capture is used to initialize Eyetracker, timer and Selenium WebDriver. After that timer elapsed method
 * 'CheckUrl' invokes drawing to the screenshot if url has changed. All drawing procedure is implemented separately
 * in the class 'SnapshotGenerator' and documented there.
*/

namespace EyeTrackerConsole
{
    class Program
    {
        // Static variables values are shared among all instances of a particular class (value remains the same for all objects).

        // Path to the screenshot folder. CHANGE IT HERE IF NEEDED.
        static DirectoryInfo root = new DirectoryInfo("D:\\IJS\\EyeTracker\\TestPhotos");

        // Visible driver.
        static ChromeDriver driver;

        // List of points which are obtaind from Tobii EyeX device.
        static List<PointF> points = new List<PointF>();

        static EyeXHost _eyeXhost;


        // The entry point of a C# console application - the first method that is invoked.
        static void Main(string[] args)
        {
            Capture();
            Test3();
        }


        public static void Capture()
        {
            try
            {
                // Initializes Eyetracker.
                EyeXHost eyeXHost = new EyeXHost();
                eyeXHost.Start();
                FixationDataStream lightlyFilteredGazeDataStream = eyeXHost.CreateFixationDataStream(FixationDataMode.Slow);
                lightlyFilteredGazeDataStream.Next += lightlyFilteredGazeDataStream_Next;
                _eyeXhost = eyeXHost;
                
                // Timer which checks URL every 0.1s
                Timer t = new Timer();
                t.Interval = 100;
                t.Elapsed += CheckUrl;

                // Starting URL. CHANGE IT HERE IF NEEDED.
                string url = "http://panache.fr/";
                //string url = "http://www.fmf.uni-lj.si/si/";
                //string url = "http://stackoverflow.com/questions/21555394/how-to-create-bitmap-from-byte-array";

                /* Creates new visible ChromeDriver, maximizes the browser window, navigates to the starting URL 
                 * and starts the "check url" timer. CHANGE THE PATH TO THE DRIVER HERE IF NEEDED.
                 */
                driver = new ChromeDriver("D:\\IJS\\EyeTracker");
                driver.Manage().Window.Maximize();
                driver.Navigate().GoToUrl(url);
                t.Start();
                lastUrl = url;

                Console.ReadKey();
            }
            finally
            {
                // Dispose the driver if the above code fails.
                if (driver != null)
                    driver.Dispose();
            }
        }

        // Static variables used for checking url in method 'CheckUrl'.
        private static string lastUrl;

        // Static field used in case user closes the window. See method 'DisposeAll' and 'DrawRemainings'.
        private static string _currentUrl;

        // Used due to multithreading reasons in 'CheckUrl'.
        public static bool inLoop = false;
        public static object _syncObject = new object();

        // Draws the obtained points to the appropriate screenshot using SnapshotGenerator class if the URL has changed.
        public static void CheckUrl(object sender, ElapsedEventArgs e)
        {
            // Implemented due to the multithreading. If another thread tries to enter locked code it will wait, 
            // block, until the object is released.
            lock(_syncObject)
            {
                if (inLoop) return; // Waits if one thread is already in this code block.
                inLoop = true;  // Continiues with the code and prevents another thread to access it.
            }

            try
            {
                string currentUrl = driver.Url; // Gets current driver url.
                string drawUrl = lastUrl;
                if (drawUrl != currentUrl || driver == null) // Only one of the arguments has to be true.
                {
                    if (drawUrl != null)
                    {
                        // Copies 'points' list to a new array and clears the original list after.
                        var pp = new List<PointF>(points).ToArray();
                        points.Clear();

                        // Creates new thread and draws the obtained points to the appropriate screenshot in the background.
                        // See 'SnapshotGenerator' class for documentation.
                        Task.Run(() => {
                            SnapshotGenerator sg = new SnapshotGenerator(drawUrl, pp, root);
                            sg.DrawPoints = true;
                            sg.ExportToFile();
                        });
                        // Updates the current url in case of unexpected closure of the driver window.
                        _currentUrl = currentUrl;
                    }
                    // Updates last url for later point drawing.
                    lastUrl = currentUrl;

                }
            }

            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
            // Allows access to another thread.
            inLoop = false;
        }


        // Adds every point obtained from the EyeTracker to the list 'points' regardful of the page's offsets. 
        static void lightlyFilteredGazeDataStream_Next(object sender, FixationEventArgs e)
        {
            if (!(double.IsNaN(e.X) && double.IsNaN(e.Y)))
            {
                // "Convert" the driver to be able to use javascript.
                IJavaScriptExecutor jsc = driver as IJavaScriptExecutor;
               
                    if (driver != null)
                    {
                        try
                        {
                            // Gets offset values using javascript.
                            int offsetY = Convert.ToInt32(jsc.ExecuteScript("return window.scrollY"));
                            int offsetX = Convert.ToInt32(jsc.ExecuteScript("return window.scrollX"));

                            // Adds offset values to each point obtained from Eyetracker.
                            points.Add(new PointF(Convert.ToSingle(e.X + offsetX), Convert.ToSingle(e.Y + offsetY)));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                            // If case of unexpected closure of the browser window.
                            DisposeAll();
                        }
                    }               
            }
        }

        public static bool terminateOnce = false;

        /* If unexpected closure of the window occurs we dispose the driver and draw the remaining points on the screenshot 
         * of the current url; We need boolean variable 'terminateOnce' because the Fixation Data Stream is
         * still running which means that the method 'lightlyFilteredGazeDataStream_Next' is called every
         * n seconds but we only wish to daw the remaining points once.
         */
        public static void DisposeAll()
        {
            if (!terminateOnce) 
            {
                terminateOnce = true;
                driver.Dispose();
                Task drawPoints = Task.Run(() => DrawRemainings());
                drawPoints.Wait();
                _eyeXhost.Dispose();
                Environment.Exit(0);
                
            }
        }


        /* Used in case of unexpected closure of the window. It draws all the remaining points to the last url,
         * which is held in private field variable '_currentUrl'.
         */
        public static void DrawRemainings()
        {
            try
            {
                // Copies remaining points to a new array and clears the original list after.
                var pp = new List<PointF>(points).ToArray();
                points.Clear();

                // Creates new thread and draws the obtained points to the appropriate screenshot in the background.
                // See 'SnapshotGenerator' class for further documentation.
                Task.Run(() =>
                {
                    SnapshotGenerator sg = new SnapshotGenerator(_currentUrl, pp, root);
                    sg.DrawPoints = true;
                    sg.ExportToFile();
                });

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }


        // TEST FUNCTIONS (can be deleted without any loss)

        public static void Test1()
        {
            DirectoryInfo root = new DirectoryInfo("D:\\IJS\\EyeTracker\\TestPhotos");
            string url = "https://www.google.com";
            url = "http://stackoverflow.com/questions/21555394/how-to-create-bitmap-from-byte-array";
            List<PointF> points = new List<PointF>();
            points.Add(new Point(0, 0));
            points.Add(new Point(100, 100));
            points.Add(new Point(0, 100));

            SnapshotGenerator sg = new SnapshotGenerator(url, points, root);
            sg.ExportToFile();
        }


        public static void Test2()
        {
            string file = Path.Combine(root.FullName, "2015-08-20T194609.json");
            SnapshotGenerator sg = SnapshotGenerator.Create(file, root);
            sg.DrawPoints = true;
            sg.ExportToFile();
        }

        public static void Test3()
        {
            try
            {
                string url = "http://stackoverflow.com/questions/21555394/how-to-create-bitmap-from-byte-array";

                // Creates new visible ChromeDriver, maximizes the browser window, navigates to the starting URL and starts the "check url" timer.
                driver = new ChromeDriver("D:\\IJS\\EyeTracker");
                driver.Manage().Window.Maximize();
                driver.Navigate().GoToUrl(url);

                Task.Run(() => { Console.Beep(1000, 100); });
                System.Threading.Thread.Sleep(10000);
                Task.Run(() => { Console.Beep(500, 1000); });

                try
                {
                    Debug.WriteLine(driver.Url);
                }

                catch (System.InvalidOperationException)
                {
                    driver.Quit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
