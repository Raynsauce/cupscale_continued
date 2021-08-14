﻿using Cupscale.Cupscale;
using Cupscale.IO;
using Cupscale.Main;
using Upscale = Cupscale.Main.Upscale;
using Cupscale.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Paths = Cupscale.IO.Paths;
using Cupscale.Implementations;
using Cupscale.OS;

namespace Cupscale.Implementations
{
    class RealEsrganNcnn : ImplementationBase
    {
        static readonly string exeName = "realesrgan-ncnn-vulkan.exe";

        public static async Task Run(string inpath, string outpath, string modelPath = "")
        {
            if (!CheckIfExeExists(Implementations.realEsrganNcnn, exeName))
                return;

            Program.lastModelName = "RealESRGAN";

            bool showWindow = Config.GetInt("cmdDebugMode") > 0;
            bool stayOpen = Config.GetInt("cmdDebugMode") == 2;

            Program.mainForm.SetProgress(3f, "Loading RealESRGAN...");
            //int scale = NcnnUtils.GetNcnnModelScale(currentNcnnModel);

            string opt = stayOpen ? "/K" : "/C";

            string cmd = $"{opt} cd /D {Path.Combine(Paths.binPath, Implementations.realEsrganNcnn.dir).Wrap()} & {exeName} -i {inpath.Wrap()} -o {outpath.Wrap()}" +
                $" -g {Config.GetInt("gpuId")} -m realesrgan-models -s 4";
            Logger.Log("[CMD] " + cmd);

            Process proc = OSUtils.NewProcess(!showWindow);
            proc.StartInfo.Arguments = cmd;

            if (!showWindow)
            {
                proc.OutputDataReceived += RealEsrganOutputHandler;
                proc.ErrorDataReceived += RealEsrganOutputHandler;
            }

            Program.currentEsrganProcess = proc;
            proc.Start();

            if (!showWindow)
            {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }

            while (!proc.HasExited)
                await Task.Delay(50);

            if (Upscale.currentMode == Upscale.UpscaleMode.Batch)
            {
                await Task.Delay(1000);
                Program.mainForm.SetProgress(100f, "[ESRGAN] Post-Processing...");
                PostProcessingQueue.Stop();
            }

        }

        private static void RealEsrganOutputHandler(object sendingProcess, DataReceivedEventArgs output)
        {
            if (output == null || output.Data == null)
                return;

            string data = output.Data;
            Logger.Log("[NCNN] " + data.Replace("\n", " ").Replace("\r", " "));

            bool showTileProgress = Upscale.currentMode == Upscale.UpscaleMode.Preview || Upscale.currentMode == Upscale.UpscaleMode.Single;

            if (showTileProgress && data.Trim().EndsWith("%"))
            {
                float percent = float.Parse(data.Replace("%", "").Replace(",", ".")) / 100f;
                Program.mainForm.SetProgress(percent, $"Upscaling Tiles ({percent}%)");
            }

            if (data.Contains("failed"))
            {
                Program.KillEsrgan();
                Program.ShowMessage("Error occurred during upscaling: \n\n" + data + "\n\n", "Error");
            }

            if (data.Contains("vkAllocateMemory"))
                Program.ShowMessage("ESRGAN-NCNN ran out of memory. Try reducing the tile size and avoid running programs in the background (especially games) that take up your VRAM.", "Error");
        }
    }
}
