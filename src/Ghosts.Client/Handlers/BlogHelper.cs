﻿using Ghosts.Client.Infrastructure;
using Ghosts.Client.Infrastructure.Browser;
using Ghosts.Domain.Code;
using Ghosts.Domain;
using Newtonsoft.Json;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;
using System.Reflection;
using NLog;

namespace Ghosts.Client.Handlers
{

    /// <summary>
    /// Handles Blog actions for BaseBrowserHandler
    /// </summary>
    public abstract class BlogHelper
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        internal static readonly Random _random = new Random();
        private int _deletionProbability = -1;
        private int _uploadProbability = -1;
        private int _downloadProbability = -1;
        private int _replyProbability = -1;
        private Credentials _credentials = null;
        private string _state = "initial";
        public string site { get; set; } = null;
        public string header { get; set; } = null;
        public string username { get; set; } = null;
        public string password { get; set; } = null;
        string _version = null;

        public BaseBrowserHandler baseHandler = null;
        public BlogContentManager contentManager = null;
        public IWebDriver Driver = null;

        
        public void Init(BaseBrowserHandler parent, IWebDriver currentDriver)
        {
            baseHandler = parent;
            contentManager = new BlogContentManager();
            Driver = currentDriver;
        }

        private bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                Log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        
        
        public virtual bool DoInitialLogin(TimelineHandler handler, string user, string pw)
        {
            Log.Trace($"Blog:: Unsupported action 'DoInitialLogin' in Blog version {_version} ");
            return false;
        }

        public virtual bool DoBrowse(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'Browse' in Blog version {_version} ");
            return false;
        }

        public virtual bool DoDelete(TimelineHandler handler)
        {
            Log.Trace($"Blog:: Unsupported action 'Delete' in Blog version {_version} ");
            return false;
        }

        public virtual bool DoUpload(TimelineHandler handler, string subject, string body)
        {
            Log.Trace($"Blog:: Unsupported action 'upload' in Blog version {_version} ");
            return true;
        }

        
        private string GetNextAction()
        {
            int choice = _random.Next(0, 101);
            string blogAction = null;
            int endRange;
            int startRange = 0;

            if (_deletionProbability > 0)
            {
                endRange = _deletionProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "delete";
                else startRange = endRange + 1;
            }

            if (blogAction == null && _uploadProbability > 0)
            {
                endRange = startRange + _uploadProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "upload";
                else startRange = endRange + 1;
            }

            if (blogAction == null && _downloadProbability > 0)
            {
                endRange = startRange + _downloadProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "download";
                else startRange = endRange + 1;

            }
            if (blogAction == null && _replyProbability > 0)
            {
                endRange = startRange + _replyProbability;
                if (choice >= startRange && choice <= endRange) blogAction = "reply";
                else startRange = endRange + 1;

            }
            return blogAction;

        }

        /// <summary>
        /// This supports only one blog site because it remembers context between runs. Different handlers should be used for different sites
        /// On the first execution, login is done to the site, then successive runs keep the login.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="timelineEvent"></param>
        public void Execute(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            string credFname;
            string credentialKey = null;
            
            Actions actions;

            switch (_state)
            {


                case "initial":
                    //these are only parsed once, global for the handler as handler can only have one entry.
                    _version = handler.HandlerArgs["blog-version"].ToString();  //guaranteed to have this option, already checked in base handler
                    
                  
                    if (_deletionProbability < 0 && handler.HandlerArgs.ContainsKey("blog-deletion-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-deletion-probability"].ToString(), out _deletionProbability);
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-deletion-probability"].ToString(), _deletionProbability))
                        {
                            _deletionProbability = 0;
                        }
                    }
                    if (_uploadProbability < 0 && handler.HandlerArgs.ContainsKey("blog-upload-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-upload-probability"].ToString(), out _uploadProbability);
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-upload-probability"].ToString(), _uploadProbability))
                        {
                            _uploadProbability = 0;
                        }
                    }
                    if (_downloadProbability < 0 && handler.HandlerArgs.ContainsKey("blog-browse-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-browse-probability"].ToString(), out (_downloadProbability));
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-browse-probability"].ToString(), _downloadProbability))
                        {
                            _downloadProbability = 0;
                        }
                    }

                    if (_replyProbability < 0 && handler.HandlerArgs.ContainsKey("blog-reply-probability"))
                    {
                        int.TryParse(handler.HandlerArgs["blog-reply-probability"].ToString(), out (_replyProbability));
                        if (!CheckProbabilityVar(handler.HandlerArgs["blog-reply-probability"].ToString(), _replyProbability))
                        {
                            _replyProbability = 0;
                        }
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability + _replyProbability) > 100)
                    {
                        Log.Trace($"Blog:: The sum of the browse/upload/deletion/reply blog probabilities is > 100 , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    if ((_deletionProbability + _uploadProbability + _downloadProbability + _replyProbability) == 0)
                    {
                        Log.Trace($"Blog:: The sum of the download/upload/deletion/reply blog probabilities == 0 , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    credFname = handler.HandlerArgs["blog-credentials-file"].ToString();

                    if (handler.HandlerArgs.ContainsKey("blog-credentials-file"))
                    {

                        try
                        {
                            _credentials = JsonConvert.DeserializeObject<Credentials>(System.IO.File.ReadAllText(credFname));
                        }
                        catch (System.Exception e)
                        {
                            Log.Trace($"Blog:: Error parsing blog credentials file {credFname} , blog browser action will not be executed.");
                            baseHandler.blogAbort = true;
                            Log.Error(e);
                            return;
                        }
                    }

                    //now parse the command args
                    //parse the command args


                    char[] charSeparators = new char[] { ':' };
                    foreach (var cmd in timelineEvent.CommandArgs)
                    {
                        //each argument string is key:value, parse this
                        var argString = cmd.ToString();
                        if (!string.IsNullOrEmpty(argString))
                        {
                            var words = argString.Split(charSeparators, 2, StringSplitOptions.None);
                            if (words.Length == 2)
                            {
                                if (words[0] == "site") site = words[1];
                                else if (words[0] == "credentialKey") credentialKey = words[1];
                            }
                        }
                    }

                    if (site == null)
                    {
                        Log.Trace($"Blog:: The command args must specify a 'site:<value>' , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    //check if site starts with http:// or https:// 
                    site = site.ToLower();
                    
                    Regex rx = new Regex("^http://.*", RegexOptions.Compiled);
                    var match = rx.Matches(site);
                    if (match.Count > 0) header = "http://";
                    if (header == null)
                    {
                        rx = new Regex("^https://.*", RegexOptions.Compiled);
                        match = rx.Matches(site);
                        if (match.Count > 0) header = "https://";
                    }
                    if (header != null)
                    {
                        site = site.Replace(header, "");
                    }
                    else
                    {
                        header = "http://";  //default header
                    }




                    if (credentialKey == null)
                    {
                        Log.Trace($"Blog:: The command args must specify a 'credentialKey:<value>' , blog browser action will not be executed.");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    username = _credentials.GetUsername(credentialKey);
                    password = _credentials.GetPassword(credentialKey);

                    if (username == null || password == null)
                    {
                        Log.Trace($"Blog:: The credential key {credentialKey} does not return a valid credential from file {credFname}, blog browser action will not be executed");
                        baseHandler.blogAbort = true;
                        return;
                    }

                    //have username, password - do the initial login
                    if (!DoInitialLogin(handler,username,password)) {
                        baseHandler.blogAbort = true;
                        return;
                    }

                    //at this point we are logged in, ready for action
                    _state = "execute";
                    break;

                case "execute":

                    //determine what to do
                    string blogAction = GetNextAction();

                    
                    if (blogAction == null)
                    {
                        //nothing to do this cycle
                        Log.Trace($"Blog:: Action is skipped for this cycle.");
                        return;
                    }

                    if (blogAction == "download")
                    {
                        if (!DoBrowse(handler))
                        {
                            baseHandler.blogAbort = true;
                            return;
                        }
                    }

                    if (blogAction == "delete")
                    {
                        if (!DoDelete(handler))
                        {
                            baseHandler.blogAbort = true;
                            return;
                        }
                    }

                    if (blogAction == "upload")
                    {
                        //get new content
                        contentManager.BlogContentNext();
                        if (contentManager.Subject == null || contentManager.Body == null)
                        {
                            Log.Trace($"Blog:: Content unavailable, check Blog content file, upload skipped.");
                        } else if (!DoUpload(handler, contentManager.Subject, contentManager.Body))
                        {
                            baseHandler.blogAbort = true;
                            return;
                        }
                    }
                    if (blogAction == "delete")
                    {
                        //select a file to delete
                        try
                        {
                            var targetElements = Driver.FindElements(By.CssSelector("td[class='ms-cellStyleNonEditable ms-vb-itmcbx ms-vb-imgFirstCell']"));
                            if (targetElements.Count > 0)
                            {
                                int docNum = _random.Next(0, targetElements.Count);
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElements[docNum]).Click().Perform();

                                var checkboxElement = targetElements[docNum].FindElement(By.XPath(".//div[@role='checkbox']"));
                                string fname = checkboxElement.GetAttribute("title");

                                Thread.Sleep(1000);
                                //delete it
                                //somewhat weird, had to locate this element by the tooltip
                                var targetElement = Driver.FindElement(By.CssSelector("a[aria-describedby='Ribbon.Documents.Manage.Delete_ToolTip'"));
                                actions = new Actions(Driver);
                                //deal with the popup
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                Driver.SwitchTo().Alert().Accept();
                                Log.Trace($"Blog:: Deleted file {fname} from site {site}.");
                                Thread.Sleep(1000);
                            }
                            else
                            {
                                Log.Trace($"Blog:: No documents to delete from {site}.");
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"Blog:: Error performing sharepoint download from site {site}.");
                            Log.Error(e);
                        }
                    }
                    break;




            }

        }





    }
}
