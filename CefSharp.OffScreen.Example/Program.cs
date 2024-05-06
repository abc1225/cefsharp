// Copyright © 2014 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CefSharp.DevTools.Page;
using System.Collections.Generic;
using CefSharp.Handler;

namespace CefSharp.OffScreen.Example
{
    public class Program
    {
/*        private const string TestUrlOne = "https://www.google.com/";
        private const string TestUrlTwo = "https://github.com/";
        private const string TestUrlThree = "https://www.google.com/doodles";
        private const string TestUrlFour = "https://microsoft.com/";*/

        private const string TestUrlOne = "http://127.0.0.1:8000/test.php";
        private const string TestUrlTwo = "http://127.0.0.1:8000/test.php";
        private const string TestUrlThree = "https://www.baidu.com";
        private const string TestUrlFour = "https://www.baidu.com/";


        public static int Main(string[] args)
        {
            Console.WriteLine("This example application will load {0}, take a screenshot, and save it to your desktop.", TestUrlOne);
            Console.WriteLine("You may see a lot of Chromium debugging output, please wait...");
            Console.WriteLine();

            //Console app doesn't have a message loop which we need as Cef.Initialize/Cef.Shutdown must be called on the same
            //thread. We use a super simple SynchronizationContext implementation from
            //https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/
            //Continuations will happen on the main thread
            //The Nito.AsyncEx.Context Nuget package has a more advanced implementation
            //https://github.com/StephenCleary/AsyncEx/blob/8a73d0467d40ca41f9f9cf827c7a35702243abb8/doc/AsyncContext.md#console-example-using-asynccontext

            // 文档 https://github.com/cefsharp/CefSharp/wiki/CefSharp%E4%B8%AD%E6%96%87%E5%B8%AE%E5%8A%A9%E6%96%87%E6%A1%A3#1%E5%9F%BA%E7%A1%80%E7%9F%A5%E8%AF%86
            AsyncContext.Run(async delegate
            {
                Cef.EnableWaitForBrowsersToClose();

                var settings = new CefSettings();

                //  设置全局语言
                settings.Locale = "zh-CN";

                // 设置全局Agent
                settings.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/50.0.2661.102 Safari/537.36";

                // 全局允许cookie（默认情况下是允许的，这里只是为了示例）
                settings.CefCommandLineArgs.Add("enable-cookie-testing", "1");


                // 设置全局代理

                // 关闭代理
                // settings.CefCommandLineArgs.Add("no-proxy-server", "1");

                // 添加代理
                //settings.CefCommandLineArgs.Add("proxy-server", "http://127.0.0.1:10809");

                //The location where cache data will be stored on disk. If empty an in-memory cache will be used for some features and a temporary disk cache for others.
                //HTML5 databases such as localStorage will only persist across sessions if a cache path is specified. 
                settings.CachePath = Path.GetFullPath("cache");



                var success = await Cef.InitializeAsync(settings);

                if (!success)
                {
                    return;
                }

                // 设置全局Cookie
                /*                var cookieManager = CefSharp.Cef.GetGlobalCookieManager();
                                var res1 = cookieManager.SetCookie("http://127.0.0.1:8000", new CefSharp.Cookie()
                                {
                                    Domain = "",
                                    Name = "cookie_name",
                                    Value = "cookie_value",
                                    Expires = DateTime.Now.AddDays(10),
                                    //Secure = false,
                                    Path = "/",
                                });
                                Console.WriteLine("cookieManager.SetCookie res1: " + res1);*/



                //var t1 =  MainAsync(TestUrlOne, TestUrlTwo, "cache\\path1", 1.0);
                //Demo showing Zoom Level of 2.0
                //Using seperate request contexts allows the urls from the same domain to have independent zoom levels
                //otherwise they would be the same - default behaviour of Chromium
                //var t2 = MainAsync(TestUrlThree, TestUrlFour, "cache\\path2", 2.0);


                // 设置数据隔离， cachePath  多个CEF进程的cookie独立
                var t1 = MainAsyncOne(TestUrlOne, "cache\\path1", 1.0);
                var t2 = MainAsyncOne(TestUrlTwo, "cache\\path2", 2.0);
                //var t3 = MainAsyncOne(TestUrlTwo, "cache\\path3", 1.0);

                //await Task.WhenAll(t1, t2, t3);
                await Task.WhenAll(t1, t2);
                //await Task.WhenAll(t1);

                Console.WriteLine("Image viewer launched.  Press any key to exit.");

                // Wait for user input
                Console.ReadKey();

                //Wait until the browser has finished closing (which by default happens on a different thread).
                //Cef.EnableWaitForBrowsersToClose(); must be called before Cef.Initialize to enable this feature
                //See https://github.com/cefsharp/CefSharp/issues/3047 for details
                Cef.WaitForBrowsersToClose();

                // Clean up Chromium objects.  You need to call this in your application otherwise
                // you will get a crash when closing.
                Cef.Shutdown();
            });

            //Success
            return 0;
        }

        private static async Task MainAsync(string url, string secondUrl, string cachePath, double zoomLevel)
        {
            var browserSettings = new BrowserSettings
            {
                //Reduce rendering speed to one frame per second so it's easier to take screen shots
                WindowlessFrameRate = 1
            };

            /*            var requestContextSettings = new RequestContextSettings
                        {
                            CachePath = Path.GetFullPath(cachePath)
                        };*/


            // RequestContext can be shared between browser instances and allows for custom settings
            // e.g. CachePath
            // using (var requestContext = new RequestContext(requestContextSettings))

            using (var requestContext = RequestContext
                                        .Configure()
                                        .WithCachePath(Path.GetFullPath("cache"))
                                        .WithProxyServer("http","127.0.0.1", 10809)
                                        .Create())
            using (var browser = new ChromiumWebBrowser(url, browserSettings, requestContext, true ))
            {
                browser.StatusMessage += Browser_StatusMessage;

                // 动态修改代理服务器, 目前不生效
                // SetProxy(browser, true, "http://127.0.0.1:10809");


                browser.RequestHandler = new CustomerRequestHandler(cachePath);


                if (zoomLevel > 1)
                {
                    browser.FrameLoadStart += (s, argsi) =>
                    {
                        var b = (ChromiumWebBrowser)s;
                        if (argsi.Frame.IsMain)
                        {
                            b.SetZoomLevel(zoomLevel);
                        }
                    };
                }
                await browser.WaitForInitialLoadAsync();

                //Check preferences on the CEF UI Thread
                await Cef.UIThreadTaskFactory.StartNew(delegate
                {
                    var preferences = requestContext.GetAllPreferences(true);

                    //Check do not track status
                    var doNotTrack = (bool)preferences["enable_do_not_track"];

                    Debug.WriteLine("DoNotTrack: " + doNotTrack);
                });

                var onUi = Cef.CurrentlyOnThread(CefThreadIds.TID_UI);

                // For Google.com pre-pupulate the search text box
                if (url.Contains("google.com"))
                {
                    await browser.EvaluateScriptAsync("document.querySelector('[name=q]').value = 'CefSharp Was Here!'");
                }

                //Example using SendKeyEvent for input instead of javascript
                //var browserHost = browser.GetBrowserHost();
                //var inputString = "CefSharp Was Here!";
                //foreach(var c in inputString)
                //{
                //	browserHost.SendKeyEvent(new KeyEvent { WindowsKeyCode = c, Type = KeyEventType.Char });
                //}

                ////Give the browser a little time to finish drawing our SendKeyEvent input
                //await Task.Delay(100);

                var contentSize = await browser.GetContentSizeAsync();

                var viewport = new Viewport
                {
                    Height = contentSize.Height,
                    Width = contentSize.Width,
                    Scale = 1.0
                };

                // Wait for the screenshot to be taken,
                // if one exists ignore it, wait for a new one to make sure we have the most up to date
                var bitmap = await browser.CaptureScreenshotAsync(viewport: viewport);

                // Make a file to save it to (e.g. C:\Users\jan\Desktop\CefSharp screenshot.png)
                var screenshotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CefSharp screenshot" + DateTime.Now.Ticks + ".png");

                Console.WriteLine();
                Console.WriteLine("Screenshot ready. Saving to {0}", screenshotPath);

                File.WriteAllBytes(screenshotPath, bitmap);

                Console.WriteLine("Screenshot saved. Launching your default image viewer...");

                // Tell Windows to launch the saved image.
                Process.Start(new ProcessStartInfo(screenshotPath)
                {
                    // UseShellExecute is false by default on .NET Core.
                    UseShellExecute = true
                });

                await browser.LoadUrlAsync(secondUrl);

                // Gets a warpper around the CefBrowserHost instance
                // You can perform a lot of low level browser operations using this interface
                var cefbrowserHost = browser.GetBrowserHost();

                // Cookie 给Broswer设置Cookie
                 bool res = browser.GetCookieManager().SetCookie("http://127.0.0.1:8000", new Cookie
                {
                    Domain = "",
                    Name = "cookie_name",
                    Value = "cookie_value",
                    Expires = DateTime.Now.AddDays(10),
                    Secure = false,
                    Path = "/",
                 }, null);
                Console.WriteLine("await browser.GetCookieManager().SetCookie res: " + res);



                //You can call Invalidate to redraw/refresh the image
                cefbrowserHost.Invalidate(PaintElementType.View);

                contentSize = await browser.GetContentSizeAsync();

                viewport = new Viewport
                {
                    Height = contentSize.Height,
                    Width = contentSize.Width,
                    Scale = 1.0
                };

                // Wait for the screenshot to be taken,
                // if one exists ignore it, wait for a new one to make sure we have the most up to date
                bitmap = await browser.CaptureScreenshotAsync(viewport: viewport);

                screenshotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CefSharp screenshot" + DateTime.Now.Ticks + ".png");

                Console.WriteLine();
                Console.WriteLine("Screenshot ready. Saving to {0}", screenshotPath);

                File.WriteAllBytes(screenshotPath, bitmap);

                Console.WriteLine("Screenshot saved. Launching your default image viewer...");

                // Tell Windows to launch the saved image.
                Process.Start(new ProcessStartInfo(screenshotPath)
                {
                    // UseShellExecute is false by default on .NET Core.
                    UseShellExecute = true
                });
            }
        }

        private static async Task MainAsyncOne(string url, string cachePath, double zoomLevel)
        {
            var browserSettings = new BrowserSettings
            {
                //Reduce rendering speed to one frame per second so it's easier to take screen shots
                WindowlessFrameRate = 1
            };

            /*            var requestContextSettings = new RequestContextSettings
                        {
                            CachePath = Path.GetFullPath(cachePath)
                        };*/


            // RequestContext can be shared between browser instances and allows for custom settings
            // e.g. CachePath
            // using (var requestContext = new RequestContext(requestContextSettings))

            // 设置数据隔离， cachePath  多个CEF进程的cookie独立
            using (var requestContext = RequestContext
                                        .Configure()
                                        .WithCachePath(Path.GetFullPath(cachePath))
                                        .WithProxyServer("http", "127.0.0.1", 10809)
                                        //.WithPreference("User Agent","Customer Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/50.0.2661.102 Safari/537.36")
                                        .Create())
            using (var browser = new ChromiumWebBrowser(url, browserSettings, requestContext, true))
            {
                browser.StatusMessage += Browser_StatusMessage;

                // 动态修改代理服务器, 目前不生效
                // SetProxy(browser, true, "http://127.0.0.1:10809");


                // 给浏览器设置不同的 UA 或 其他Header头信息
                 browser.RequestHandler = new CustomerRequestHandler(cachePath);


                if (zoomLevel > 1)
                {
                    browser.FrameLoadStart += (s, argsi) =>
                    {
                        var b = (ChromiumWebBrowser)s;
                        if (argsi.Frame.IsMain)
                        {
                            b.SetZoomLevel(zoomLevel);
                        }
                    };
                }
                await browser.WaitForInitialLoadAsync();

                //Check preferences on the CEF UI Thread
                await Cef.UIThreadTaskFactory.StartNew(delegate
                {
                    var preferences = requestContext.GetAllPreferences(true);

                    //Check do not track status
                    var doNotTrack = (bool)preferences["enable_do_not_track"];

                    Debug.WriteLine("DoNotTrack: " + doNotTrack);
                });

                var onUi = Cef.CurrentlyOnThread(CefThreadIds.TID_UI);

                // For Google.com pre-pupulate the search text box
                if (url.Contains("google.com"))
                {
                    await browser.EvaluateScriptAsync("document.querySelector('[name=q]').value = 'CefSharp Was Here!'");
                }

                //Example using SendKeyEvent for input instead of javascript
                //var browserHost = browser.GetBrowserHost();
                //var inputString = "CefSharp Was Here!";
                //foreach(var c in inputString)
                //{
                //	browserHost.SendKeyEvent(new KeyEvent { WindowsKeyCode = c, Type = KeyEventType.Char });
                //}

                ////Give the browser a little time to finish drawing our SendKeyEvent input
                //await Task.Delay(100);

                var contentSize = await browser.GetContentSizeAsync();

                var viewport = new Viewport
                {
                    Height = contentSize.Height,
                    Width = contentSize.Width,
                    Scale = 1.0
                };

                // Wait for the screenshot to be taken,
                // if one exists ignore it, wait for a new one to make sure we have the most up to date
                var bitmap = await browser.CaptureScreenshotAsync(viewport: viewport);

                // Make a file to save it to (e.g. C:\Users\jan\Desktop\CefSharp screenshot.png)
                var screenshotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CefSharp screenshot" + DateTime.Now.Ticks + ".png");

                Console.WriteLine();
                Console.WriteLine("Screenshot ready. Saving to {0}", screenshotPath);

                File.WriteAllBytes(screenshotPath, bitmap);

                Console.WriteLine("Screenshot saved. Launching your default image viewer...");

                // Tell Windows to launch the saved image.
                Process.Start(new ProcessStartInfo(screenshotPath)
                {
                    // UseShellExecute is false by default on .NET Core.
                    UseShellExecute = true
                });

                //await browser.LoadUrlAsync(secondUrl);

                // Gets a warpper around the CefBrowserHost instance
                // You can perform a lot of low level browser operations using this interface
                var cefbrowserHost = browser.GetBrowserHost();

                // GetBrowserHost 可获取之后， 再设置Cookie 给Broswer设置Cookie
                bool res = browser.GetCookieManager().SetCookie("http://127.0.0.1:8000", new Cookie
                {
                    Domain = "",
                    Name = "cookie_name",
                    Value = cachePath,
                    Expires = DateTime.Now.AddDays(10),
                    Secure = false,
                    Path = "/",
                }, null);
                Console.WriteLine("await browser.GetCookieManager().SetCookie res: " + res);

            }
        }


        private static void Browser_StatusMessage(object sender, StatusMessageEventArgs e)
        {
            // 处理状态信息，例如加载进度等
           // MessageBox.Show(e.Value);
            Console.WriteLine("Browser_StatusMessage: " + e.Value);
        }


        /**
         * 暂时无用
         */
        private static async void SetProxy(ChromiumWebBrowser cwb, bool useProxy,string Address)
        {
            try
            {
                //判断是否使用代理
                if (useProxy)
                {

/*                    var value = RequestContextExtensions.GetProxyDictionary(scheme, host, port);
                    preferences.Add(new KeyValuePair<string, object>("proxy", value));*/

                    await Cef.UIThreadTaskFactory.StartNew(delegate
                    {
                        var rc = cwb.GetBrowser().GetHost().RequestContext;
                        var proxyConfig = new Dictionary<string, object>();
                        proxyConfig["mode"] = "fixed_servers";
                        proxyConfig["server"] = Address;
                        string error;


                        if (rc.CanSetPreference("proxy"))
                        {
                            Console.WriteLine("是否可以代理服务器结果: true");
                        }
                        else
                        {
                            Console.WriteLine("是否可以代理服务器结果: false");
                        }

                        bool success = rc.SetPreference("proxy", proxyConfig, out error);
                        Console.WriteLine("设置代理服务器结果: " + success + "  error: " + error);
                    });
                }
                else
                {
                    //关闭代理
                    await Cef.UIThreadTaskFactory.StartNew(delegate
                    {
                        var rc = cwb.GetBrowser().GetHost().RequestContext;
                        if (rc != null)
                        {
                            var proxyMode = "direct";
                            string error;
                            rc.SetPreference("proxy.mode", proxyMode, out error);

                            // 清除代理服务器设置  
                            bool success = rc.SetPreference("proxy.server", string.Empty, out error);
                            Console.WriteLine("清除代理服务器结果: " + success + "  error: " + error);
                        }
                    });

                }
            }
            catch (Exception ex)
            {
                //输出错误日志
            }
        }

    }

    /**
     * 设置Header信息
     */
    public class CustomerRequestHandler : RequestHandler
    {
        private String agent;
        public CustomerRequestHandler(String a) {
            agent = a;
        }

        protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
        {
            // 在这里设置Header 新版本该对象已经不允许修改
            //request.SetHeaderByName("", "", true);
            return false;
        }

        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return new CustomResourceRequestHandler(agent);
        }

        // 其他接口实现略...
    }

    /**
 * 设置Header信息
 */
    public class CustomResourceRequestHandler : ResourceRequestHandler
    {
        private String agent;

        public CustomResourceRequestHandler(String a) {
            Console.WriteLine("Create CustomResourceRequestHandler agent: " + a);
            agent = a;
        }


        protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            var headers = request.Headers;
            headers["User-Agent"] = ("My User Agent" + agent);
            request.Headers = headers;

            return CefReturnValue.Continue;
        }
    }


}
