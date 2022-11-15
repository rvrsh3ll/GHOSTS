﻿using Ghosts.Domain;
using Ghosts.Domain.Code;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools.V101.Overlay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Actions = OpenQA.Selenium.Interactions.Actions;

namespace Ghosts.Client.Handlers
{

    public class BlogHelperDrupal : BlogHelper
    {

        public BlogHelperDrupal(BaseBrowserHandler callingHandler, IWebDriver callingDriver)
        {
            base.Init(callingHandler, callingDriver);

        }

        /// <summary>
        /// Login into the Drupal site
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <param name="header"></param>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <returns></returns>
        public override bool DoInitialLogin(TimelineHandler handler, string user, string pw)
        {
            //have the username, password

            RequestConfiguration config;
            Actions actions;


            //for drupal, cannot pass user/pw in the url

            string target = header + site + "/";
            //navigate to the base site first

            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHandler.MakeRequest(config);
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to parse site {target}, url may be malformed. Blog browser action will not be executed.");
                Log.Error(e);
                return false;
            }
            //now login 
            try
            {
                var targetElement = Driver.FindElement(By.CssSelector("input#edit-name.form-text.required"));
                targetElement.SendKeys(user);
                Thread.Sleep(500);
                targetElement = Driver.FindElement(By.CssSelector("input#edit-pass.form-text.required"));
                targetElement.SendKeys(pw);
                Thread.Sleep(500);
                targetElement = Driver.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                actions = new Actions(Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                Thread.Sleep(500);
                //check if login was successful
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to login into site {target}, check username/password. Blog browser action will not be executed.");
                Log.Error(e);
                return false;
            }

            //check login success
            try
            {
                var targetElement = Driver.FindElement(By.CssSelector("div[class='messages error']"));
                //if reach here, login was unsuccessful
                Log.Trace($"Blog:: Unable to login into site {target}, check username/password. Blog browser action will not be executed.");
                return false;
            }
            catch
            {
                //ignore
            }

            return true;
        }


        /// <summary>
        /// Upload a blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public override bool DoUpload(TimelineHandler handler, string subject, string body)
        {

            RequestConfiguration config;
            Actions actions;

            string target = header + site + "/node/add/blog";
            //navigate to the add content page
            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHandler.MakeRequest(config);
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to navigate to {target} while adding content.");
                Log.Error(e);
                return true;  //dont abort handler  because of this
            }

            //add subject,body
            try
            {
                var targetElement = Driver.FindElement(By.CssSelector("input#edit-title.form-text.required"));
                targetElement.SendKeys(subject);
                targetElement = Driver.FindElement(By.CssSelector("textarea#edit-body-und-0-value"));
                targetElement.SendKeys(body);
                Thread.Sleep(1000);
                targetElement = Driver.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                actions = new Actions(Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                Log.Trace($"Blog:: Added post to site {site}.");

            }
            catch (Exception e)
            {
                Log.Trace($"Blog:: Error while posting content to site {site}.");
                Log.Error(e);
            }


            return true;
        }

        /// <summary>
        /// Delete an existing blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public override bool DoDelete(TimelineHandler handler)
        {
            Actions actions;
            //first, browse to an existing entry
            if (DoBrowse(handler))
            {
                try
                {
                    //successfully browsed to an entry and in view mode
                    var targetElement = Driver.FindElement(By.CssSelector("ul.tabs.primary"));
                    var tabLinks = targetElement.FindElements(By.XPath(".//a"));
                    if (tabLinks.Count > 0)
                    {

                        foreach (var link in tabLinks)
                        {
                            var hrefValue = link.GetAttribute("href");
                            if (hrefValue.Contains("edit"))
                            {
                                //click the Edit tab
                                actions = new Actions(Driver);
                                actions.MoveToElement(link).Click().Perform();
                                Thread.Sleep(2000);
                                //edit is in an overlay, that contains an iframe
                                var overlay = Driver.FindElement(By.Id("overlay-container"));
                                var iframe = overlay.FindElement(By.CssSelector("iframe.overlay-element.overlay-active"));
                                Driver.SwitchTo().Frame(iframe);
                                targetElement = Driver.FindElement(By.CssSelector("input#edit-delete.form-submit"));
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                //another overlay pops up
                                overlay = Driver.FindElement(By.Id("overlay"));
                                //this overlay does not have an iframe
                                targetElement = overlay.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                                //press delete button
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                Log.Trace($"Blog:: Deleted post from site {site}.");


                            }

                        }
                    }
                }
                catch
                {
                    return true;
                }

            }
            return true;
        }


        /// <summary>
        /// Browse to an existing blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public override bool DoBrowse(TimelineHandler handler)
        {
            Actions actions;
            RequestConfiguration config;
            string target = header + site + "/blog";
            //navigate to the blog page
            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHandler.MakeRequest(config);
                Thread.Sleep(1000);
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to navigate to {target} while browsing.");
                Log.Error(e);
                return true;  //dont abort handler  because of this
            }

            //first pick a page if there are multiple pages
            try
            {
                var targetElements = Driver.FindElements(By.CssSelector("li.pager-item"));
                if (targetElements.Count > 0)
                {
                    int pageNum = _random.Next(0, targetElements.Count + 1);
                    if (pageNum != targetElements.Count)
                    {
                        //pick a different page
                        actions = new Actions(Driver);
                        actions.MoveToElement(targetElements[pageNum]).Click().Perform();
                        Thread.Sleep(1000);

                    }
                }
            }
            catch
            {

            }

            try
            {
                var targetElements = Driver.FindElements(By.CssSelector("li.node-readmore"));


                if (targetElements.Count > 0)
                {
                    int docNum = _random.Next(0, targetElements.Count);
                    actions = new Actions(Driver);
                    actions.MoveToElement(targetElements[docNum]).Click().Perform();
                    Log.Trace($"Blog:: Browsed post on site {site}.");
                    return true;
                }
            }
            catch
            {

            }
            Log.Trace($"Blog:: No articles to browse on site {site}.");
            return true;
        }
    }


}
