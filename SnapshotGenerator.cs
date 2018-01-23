using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/* 
 BEFORE THE START:
 * (1) Change the phantomjs driver path in the 'ExportToFile' method.
 * (2) Optional: change the output file name which is stored in variable 'fileBase' in 'ExportToFile' method.
  
 
 PROGRAM DESCRIPTION:
 */ 


namespace EyeTrackerConsole
{

    public class SnapshotGenerator
    {
        // PUBLIC VARIABLES:
        public string Url;
        public PointF[] Points { get; set; }

        // DirectoryInfo class exposes instance methods for creating, moving, and enumerating through 
        // directories and subdirectories. 
        DirectoryInfo BaseFolder { get; set; }

        public bool DrawPoints { get; set; }


        // SnapshotGenerator CLASS CONSTRUCTOR.
        public SnapshotGenerator(string url, IEnumerable<PointF> points, DirectoryInfo baseFolder)
        {
            this.Url = url;
            this.Points = points.ToArray(); // copies the elements of the 'points' list to a new array. 
            this.BaseFolder = baseFolder;
        }

        // METHODS:
        // Creates 'SnapshotGenerator' object from a JSON file and its path.
        public static SnapshotGenerator Create(string file, DirectoryInfo baseFolder)
        {
            // Outputs messages if the 'file' doesn't exists.
            Debug.Assert(File.Exists(file)); 

            // Deserializes the JSON file 'file' to a SnapshotGenerator object.
            SnapshotGenerator result = JsonConvert.DeserializeObject<SnapshotGenerator>(File.ReadAllText(file));

            // Setting the 'BaseFolder' property of the above SnapshotGenerator object to the current path.
            result.BaseFolder = baseFolder;
            return result;
        }


        public FileInfo ExportToFile()
        {
            try
            {
                // The beep is used for testing purpose. It can be deleted withod any loss.
                Task.Run(() => { Console.Beep(1000, 100); });
                Thread.Sleep(200);

                // Creates the path of the output file.
                string fileBase = String.Format("{0:yyyy-MM-ddTHHmmss}", DateTime.Now);

                // FullName property gets the full path of the directory.
                // Path.Combine method comvines two strings into a path.
                // Creating two instances of the FileInfo class from a PNG and JSON files (which represent the same file).
                FileInfo fi = new FileInfo(Path.Combine(BaseFolder.FullName, String.Format("{0}.png", fileBase)));
                FileInfo fi2 = new FileInfo(Path.Combine(BaseFolder.FullName, String.Format("{0}.json", fileBase)));

                // Serializes the SnapshotGenerator object to a JSON string using indented formatting.
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);

                // Creates a new file with a path 'fi2.FullName', writes the 'json' string to the file, and 
                // then closes the file. If the file already exists its content is overwritten.
                File.WriteAllText(fi2.FullName, json);

                // CHANGE THE PHANTOMJS DRIVER PATH HERE.
                using (var driverService = PhantomJSDriverService.CreateDefaultService("D:\\IJS\\EyeTracker\\phantomjs-2.0.0-windows\\bin"))
                {
                    // Hides the PhantomJSDriver command prompt window.
                    driverService.HideCommandPromptWindow = true;

                    // Creates new headless driver which runs invisibly in the background and is used to take sceenshots.
                    using (IWebDriver driver = new PhantomJSDriver(driverService))
                    {
                        // Maximizes the background driver window and navigates it to the specified url.
                        driver.Manage().Window.Maximize();
                        driver.Navigate().GoToUrl(Url);

                        // Waits until the page is fully loaded and only then takes a screenshot of the whole document.
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30.00));
                        wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));

                        // Takes a screenshot.
                        ITakesScreenshot screenshotDriver = driver as ITakesScreenshot;
                        Screenshot screenshot = screenshotDriver.GetScreenshot();

                        // Drawing procedure.
                        using (var ms = new MemoryStream(screenshot.AsByteArray))
                        {
                            using (Bitmap bmp = new Bitmap(ms))
                            {
                                using (Graphics g = Graphics.FromImage(bmp))
                                {
                                    try
                                    {
                                        // Draw points and the intepolating curve on the bitmap.

                                        //GraphicsPath gp = new GraphicsPath(this.Points.Select(i => new Point(i.X, i.Y)).ToArray());

                                        g.DrawCurve(Pens.Red, this.Points);

                                        // Executes if we want to draw points as well.
                                        if (DrawPoints)
                                        {
                                            foreach (PointF f in this.Points)
                                            {
                                                g.DrawRectangle(new Pen(new SolidBrush(Color.Red), 2), f.X, f.Y, 3, 3);
                                            }
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine(ex.ToString());
                                    }
                                }
                                // Outputs message if the file already exists because the 'fi' should yet not exist at this point.
                                Debug.Assert(!fi.Exists);

                                // Saves the bitmap to the file with the above created path.
                                bmp.Save(fi.FullName, ImageFormat.Png);

                                // Refreshes the state of the 'fi' object and outputs messange if it doesn't exists.
                                fi.Refresh();
                                Debug.Assert(fi.Exists);

                                // The beep is used for testing purpose. It can be deleted without any loss.
                                Task.Run(() => { Console.Beep(300, 100); });
                                Thread.Sleep(200);

                                return fi;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Task.Run(() => { Console.Beep(5000, 100); }); // Again used for testig purpose.
                throw;
            }
        }
    }
}
