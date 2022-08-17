// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Microsoft.Office.Interop.Excel;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Domain.Code.Helpers;
using NetOffice.ExcelApi.Tools;
using Newtonsoft.Json;
using Excel = NetOffice.ExcelApi;

namespace Ghosts.Client.Handlers
{
    public class ExcelHandler : BaseHandler
    {
        public ExcelHandler(Timeline timeline, TimelineHandler handler)
        {
            base.Init(handler);
            Log.Trace("Launching Excel handler");

            try
            {
                if (handler.Loop)
                {
                    Log.Trace("Excel loop");
                    while (true)
                    {
                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).Count();
                            if (processIds > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                            {
                                continue;
                            }
                        }

                        ExecuteEvents(timeline, handler);
                    }
                }
                else
                {
                    Log.Trace("Excel single run");
                    KillApp();
                    ExecuteEvents(timeline, handler);
                    KillApp();
                }
            }
            catch (Exception e)
            {
                Log.Error($"Excel launch handler exception: {e}");
                KillApp();
            }
        }

        private static void KillApp()
        {
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Excel);
        }

        private void ExecuteEvents(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    try
                    {
                        Log.Trace($"Excel event - {timelineEvent}");
                        WorkingHours.Is(handler);

                        if (timelineEvent.DelayBefore > 0)
                        {
                            Thread.Sleep(timelineEvent.DelayBefore);
                        }

                        if (timeline != null)
                        {
                            var processIds = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                            if (processIds.Count > 2 && processIds.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                            {
                                return;
                            }
                        }

                        var writeSleep = ProcessManager.Jitter(100);
                        using (var excelApplication = new Excel.Application
                               {
                                   DisplayAlerts = false,
                                   Visible = true
                               })
                        {

                            // create a utils instance, not need for but helpful to keep the lines of code low
                            var utils = new CommonUtils(excelApplication);

                            Log.Trace("Excel adding workbook");
                            // add a new workbook
                            var workBook = excelApplication.Workbooks.Add();
                            Log.Trace("Excel adding worksheet");
                            var workSheet = (Excel.Worksheet)workBook.Worksheets[1];


                            var list = RandomText.GetDictionary.GetDictionaryList();
                            using (var rt = new RandomText(list.ToArray()))
                            {
                                rt.AddSentence(10);
                                workSheet.Cells[1, 1].Value = rt.Content;
                                workSheet.Cells[1, 1].Dispose();
                            }

                            for (var i = 2; i < 100; i++)
                            {
                                for (var j = 1; j < 100; j++)
                                {
                                    if (_random.Next(0, 20) != 1) // 1 in 20 cells are blank
                                    {
                                        workSheet.Cells[i, j].Value = _random.Next(0, 999999999);
                                        workSheet.Cells[i, j].Dispose();
                                    }
                                }
                            }

                            //for (var i = 0; i < _random.Next(1,30); i++)
                            //{
                            //    var range = GetRandomRange();
                            //    // draw back color and perform the BorderAround method
                            //    workSheet.Range(range).Interior.Color =
                            //        utils.Color.ToDouble(StylingExtensions.GetRandomColor());
                            //    workSheet.Range(range).BorderAround(XlLineStyle.xlContinuous, XlBorderWeight.xlMedium,
                            //        XlColorIndex.xlColorIndexAutomatic);

                            //    range = GetRandomRange();
                            //    // draw back color and border the range explicitly
                            //    workSheet.Range(range).Interior.Color =
                            //        utils.Color.ToDouble(StylingExtensions.GetRandomColor());
                            //    workSheet.Range(range)
                            //        .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                            //        .LineStyle = XlLineStyle.xlDouble;
                            //    workSheet.Range(range)
                            //        .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                            //        .Weight = 4;
                            //    workSheet.Range(range)
                            //        .Borders[(Excel.Enums.XlBordersIndex) XlBordersIndex.xlInsideHorizontal]
                            //        .Color = utils.Color.ToDouble(StylingExtensions.GetRandomColor());
                            //}

                            Thread.Sleep(writeSleep);

                            var rand = RandomFilename.Generate();

                            var defaultSaveDirectory = timelineEvent.CommandArgs[0].ToString();
                            if (defaultSaveDirectory.Contains("%"))
                            {
                                defaultSaveDirectory = Environment.ExpandEnvironmentVariables(defaultSaveDirectory);
                            }

                            try
                            {
                                foreach (var key in timelineEvent.CommandArgs)
                                {
                                    if (key.ToString().StartsWith("save-array:"))
                                    {
                                        var savePathString =
                                            key.ToString().Replace("save-array:", "").Replace("'", "\"");
                                        savePathString =
                                            savePathString.Replace("\\",
                                                "/"); //can't seem to deserialize windows path \
                                        var savePaths = JsonConvert.DeserializeObject<string[]>(savePathString);
                                        defaultSaveDirectory =
                                            savePaths.PickRandom().Replace("/", "\\"); //put windows path back
                                        break;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Trace($"save-array exception: {e}");
                            }

                            if (!Directory.Exists(defaultSaveDirectory))
                            {
                                Directory.CreateDirectory(defaultSaveDirectory);
                            }

                            var path = $"{defaultSaveDirectory}\\{rand}.xlsx";

                            //if directory does not exist, create!
                            Log.Trace($"Checking directory at {path}");
                            var f = new FileInfo(path).Directory;
                            if (f == null)
                            {
                                Log.Trace($"Directory does not exist, creating directory at {f.FullName}");
                                Directory.CreateDirectory(f.FullName);
                            }

                            try
                            {
                                if (File.Exists(path))
                                {
                                    File.Delete(path);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Excel file delete exception: {e}");
                            }

                            Log.Trace($"Excel saving to path - {path}");
                            workBook.SaveAs(path);

                            FileListing.Add(path);
                            Report(handler.HandlerType.ToString(), timelineEvent.Command,
                                timelineEvent.CommandArgs[0].ToString());

                            if (timelineEvent.CommandArgs.Contains("pdf"))
                            {
                                var pdfFileName = timelineEvent.CommandArgs.Contains("pdf-vary-filenames")
                                    ? $"{defaultSaveDirectory}\\{RandomFilename.Generate()}.pdf"
                                    : path.Replace(".xlsx", ".pdf");
                                // Save document into PDF Format
                                workBook.ExportAsFixedFormat(NetOffice.ExcelApi.Enums.XlFixedFormatType.xlTypePDF,
                                    pdfFileName);
                                // end save as pdf
                                Report(handler.HandlerType.ToString(), timelineEvent.Command, "pdf");
                                FileListing.Add(pdfFileName);
                            }

                            if (_random.Next(100) < 50)
                                workBook.Close();


                            workSheet.Dispose();
                            workSheet = null;

                            workBook.Dispose();
                            workBook = null;

                            // close excel and dispose reference
                            excelApplication.Quit();

                            try
                            {
                                excelApplication.Dispose();
                            }
                            catch
                            {
                                // ignore
                            }

                            try
                            {
                                Marshal.ReleaseComObject(excelApplication);
                            }
                            catch
                            {
                                // ignore
                            }

                            try
                            {
                                Marshal.FinalReleaseComObject(excelApplication);
                            }
                            catch
                            {
                                // ignore
                            }
                        }

                        GC.Collect();

                        if (timelineEvent.DelayAfter > 0)
                        {
                            //sleep and leave the app open
                            Log.Trace($"Sleep after for {timelineEvent.DelayAfter}");
                            Thread.Sleep(timelineEvent.DelayAfter - writeSleep);
                        }

                    }
                    catch (Exception e)
                    {
                        Log.Error($"Excel handler exception: {e}");
                    }
                    finally
                    {
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                KillApp();
                Log.Trace("Excel closing...");
            }
        }

        private string GetRandomRange()
        {
            var x = _random.Next(1, 40);
            var y = _random.Next(x, 50);
            var a1 = RandomText.GetRandomCapitalLetter();
            var a2 = RandomText.GetRandomCapitalLetter(a1);

            return $"${a1}{x}:${a2}{y}";
        }
    }
}
