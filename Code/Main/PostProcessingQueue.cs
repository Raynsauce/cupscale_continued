﻿using Cupscale.IO;
using Cupscale.Main;
using Cupscale.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cupscale.Cupscale
{
    class PostProcessingQueue
    {
        public static Queue<string> outputFileQueue = new Queue<string>();
        public static List<string> processedFiles = new List<string>();
        public static List<string> outputFiles = new List<string>();

        public static bool run;
        public static string currentOutPath;

        //public static bool ncnn;

        public enum CopyMode { KeepStructure, CopyToRoot }
        public static CopyMode copyMode;

        public static void Start (string outpath)
        {
            currentOutPath = outpath;
            outputFileQueue.Clear();
            processedFiles.Clear();
            outputFiles.Clear();
            IOUtils.ClearDir(Paths.imgOutNcnnPath);
            run = true;
        }

        public static void Stop ()
        {
            Logger.Log("PostProcessingQueue.Stop()");
            run = false;
        }

        public static async Task Update ()
        {
            while (run || AnyFilesLeft())
            {
                CheckNcnnOutput();
                Logger.Log("Update() Loop runs, run: " + run + ", AnyFilesLeft(): " + AnyFilesLeft());
                /*
                foreach (string file in Directory.GetFiles(Paths.imgOutPath, "*.png.png", SearchOption.AllDirectories))   // Rename to tmp
                {
                    try
                    {
                        string newPath = file.Substring(0, file.Length - 8) + ".tmp";
                        Logger.Log("renaming " + file + " -> " + newPath);
                        File.Move(file, newPath);
                    }
                    catch { }
                }
                */
                string[] outFiles = Directory.GetFiles(Paths.imgOutPath, "*.tmp", SearchOption.AllDirectories);
                Logger.Log("Queue Update() - " + outFiles.Length + " files in out folder");
                foreach (string file in outFiles)
                {
                    if (!outputFileQueue.Contains(file) && !processedFiles.Contains(file) && !outputFiles.Contains(file))
                    {
                        //processedFiles.Add(file);
                        outputFileQueue.Enqueue(file);
                        Logger.Log("[Queue] Enqueued " + Path.GetFileName(file));
                    }
                    else
                    {
                        Logger.Log("Skipped " + file + " - Is In Queue: " + outputFileQueue.Contains(file) + " - Is Processed: " + processedFiles.Contains(file) + " - Is Outfile: " + outputFiles.Contains(file));
                    }
                }
                await Task.Delay(1000);
            }
            Logger.Log("Exited Update()");
        }

        static bool AnyFilesLeft ()
        {
            if (IOUtils.GetAmountOfFiles(Paths.imgOutPath, true) > 0)
                return true;
            if (IOUtils.GetAmountOfFiles(Paths.imgOutNcnnPath, true) > 0)
                return true;
            Logger.Log("No files in Paths.imgOutPath");
            return false;
        }

        public static string lastOutfile;

        public static async Task ProcessQueue ()
        {
            Stopwatch sw = new Stopwatch();
            Logger.Log("ProcessQueue()");
            while (run || AnyFilesLeft())
            {
                if (outputFileQueue.Count > 0)
                {
                    string file = outputFileQueue.Dequeue();
                    Logger.Log("[Queue] Post-Processing " + Path.GetFileName(file));
                    sw.Restart();
                    await Upscale.PostprocessingSingle(file, false);
                    string outFilename = Upscale.FilenamePostprocess(lastOutfile);
                    outputFiles.Add(outFilename);
                    Logger.Log("[Queue] Done Post-Processing " + Path.GetFileName(file) + " in " + sw.ElapsedMilliseconds + "ms");

                    try
                    {
                        if (Upscale.overwriteMode == Upscale.Overwrite.Yes)
                        {
                            string suffixToRemove = "-" + Program.lastModelName.Replace(":", ".").Replace(">>", "+");
                            if (copyMode == CopyMode.KeepStructure)
                            {
                                string combinedPath = currentOutPath + outFilename.Replace(Paths.imgOutPath, "");
                                Directory.CreateDirectory(combinedPath.GetParentDir());
                                File.Copy(outFilename, combinedPath.ReplaceInFilename(suffixToRemove, "", true), true);
                            }
                            if (copyMode == CopyMode.CopyToRoot)
                            {
                                File.Copy(outFilename, Path.Combine(currentOutPath, Path.GetFileName(outFilename).Replace(suffixToRemove, "")), true);
                            }
                            File.Delete(outFilename);
                        }
                        else
                        {
                            if (copyMode == CopyMode.KeepStructure)
                            {
                                string combinedPath = currentOutPath + outFilename.Replace(Paths.imgOutPath, "");
                                Directory.CreateDirectory(combinedPath.GetParentDir());
                                File.Copy(outFilename, combinedPath, true);
                            }
                            if (copyMode == CopyMode.CopyToRoot)
                            {
                                File.Copy(outFilename, Path.Combine(currentOutPath, Path.GetFileName(outFilename)), true);
                            }
                            File.Delete(outFilename);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log("Error trying to copy post-processed file back: " + e.Message + "\n" + e.StackTrace);
                    }
                    
                    BatchUpscaleUI.upscaledImages++;
                }
                await Task.Delay(250);
            }
        }

        static void CheckNcnnOutput()
        {
            foreach (string file in Directory.GetFiles(Paths.imgOutPath, "*.png.png", SearchOption.AllDirectories))   // Rename to tmp
            {
                try
                {
                    string newPath = file.Substring(0, file.Length - 8) + ".tmp";
                    Logger.Log("renaming " + file + " -> " + newPath);
                    string movePath = Path.Combine(Paths.imgOutPath, Path.GetFileName(newPath));
                    Logger.Log("moving " + file + " -> " + movePath);
                    File.Move(file, movePath);
                }
                catch { }
            }
            /*
            try
            {
                IOUtils.RenameExtensions(Paths.imgOutNcnnPath, "png", "tmp", true);
                IOUtils.Copy(Paths.imgOutNcnnPath, Paths.imgOutPath, "*", true);
            }
            catch { }
            */
        }
    }
}
