using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Utilities;
using Microsoft.ML.OnnxRuntime;
using Microsoft.Scripting.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;

/* This exists for me to test if full voice acting will work properly before we get voice actors involved */
namespace JortPob.Common
{
    public class SAM
    {
        public static string Generate(Dialog.DialogRecord dialog, Dialog.DialogInfoRecord info, string line, string hashName, NpcContent npc)
        {
            // Get the exact location this file will be in
            string lineDir = $"{Const.CACHE_PATH}dialog\\{npc.race}\\{npc.sex}\\{dialog.id}\\{hashName}\\";
            string wavPath = $"{lineDir}{hashName}.wav";
            string wemPath = $"{lineDir}{hashName}.wem";

            for (int retry = 0; retry < Const.SAM_MAX_RETRY; retry++)
            {
                try
                {
                    // Create synth
                    using (SpeechSynthesizer synthesizer = new())
                    {
                        // Check if this audio file exists in the cache already // @TODO: ideally we generate a voice cache later but guh w/e filesystem check for now
                        if (System.IO.File.Exists(wemPath)) { return wemPath; }

                        if (npc.sex == NpcContent.Sex.Female) { synthesizer.SelectVoice("Microsoft Zira Desktop"); }
                        else { synthesizer.SelectVoice("Microsoft David Desktop"); }

                        // Make folder if doesn't exist (this is so ugly lmao)
                        if (!System.IO.Directory.Exists(lineDir)) { System.IO.Directory.CreateDirectory(lineDir); }

                        // Write 32bit 44100hz wav file (required format for wem)
                        synthesizer.SetOutputToWaveFile(wavPath, new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                        synthesizer.Speak(line);
                    }

                    // Convert wav to wem
                    // Setup paths, make folders
                    string wwiseConsolePath = $"{Const.WWISE_PATH}WwiseConsole.exe";
                    string xmlName = $"{hashName}.wsources";
                    string xmlPath = $"{lineDir}{xmlName}";
                    string xmlRelative = $"..\\dialog\\{npc.race}\\{npc.sex}\\{dialog.id}\\{hashName}\\{xmlName}";
                    string projectDir = $"{Const.CACHE_PATH}wwise\\";
                    string projectPath = $"{projectDir}wwise.wproj";

                    // Create XML file
                    string xmlRaw = $""""
                                <?xml version='1.0' encoding='UTF-8'?>
                                <ExternalSourcesList SchemaVersion="1" Root="{lineDir}"><Source Path="{hashName}.wav" Conversion="Vorbis Quality High" /></ExternalSourcesList>
                                """";
                    File.WriteAllText(xmlPath, xmlRaw);

                    // Create project if it doesn't exist
                    if (!File.Exists(projectPath))
                    {
                        if (Directory.Exists(projectDir)) { Directory.Delete(projectDir); } // creating a wwise proj requires the folder to not exist
                        ProcessStartInfo startInfo = new(wwiseConsolePath)
                        {
                            WorkingDirectory = Const.CACHE_PATH,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.AddRange(new string[] { "create-new-project", $"\"{projectPath}\"", "--platform", "Windows" });
                        using var process = Process.Start(startInfo);
                        process.WaitForExit();
                    }

                    // Call wwise console to convert wav to wem
                    {
                        ProcessStartInfo startInfo = new(wwiseConsolePath)
                        {
                            RedirectStandardOutput = true,
                            WorkingDirectory = lineDir,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.AddRange(new string[] { "convert-external-source", $"\"{projectPath}\"", "--source-file", xmlRelative, "--output", "Windows", $"\"{lineDir}\"" });
                        using var process = Process.Start(startInfo);
                        process.WaitForExit();
                    }
                }
                catch
                {
                    Lort.Log($"## ERROR ## Failed to generate dialog {wavPath}", Lort.Type.Debug);
                }

                if (File.Exists(wemPath)) { break; } // if the file is created successfully we don't need to retry.
            }

            if (!File.Exists(wemPath))
            {
                throw new System.Exception($"Failed to generated line {wemPath} despite {Const.SAM_MAX_RETRY} retry attempts.");
            }

            // Return wem path
            return wemPath;
        }
    
        public static string GenerateAlt(Dialog.DialogRecord dialog, Dialog.DialogInfoRecord info, string line, string hashName, NpcContent npc)
        {
            // Get the exact location this file will be in
            string lineDir = $"{Const.CACHE_PATH}dialog\\{npc.race}\\{npc.sex}\\{dialog.id}\\{hashName}\\";
            string wavPath = $"{lineDir}{hashName}.wav";
            string wemPath = $"{lineDir}{hashName}.wem";

            var flitePath = $"{Environment.CurrentDirectory}\\Resources\\tts\\flite.exe";
            for (int retry = 0; retry < Const.SAM_MAX_RETRY; retry++)
            {
                try
                {
                    // Check if this audio file exists in the cache already // @TODO: ideally we generate a voice cache later but guh w/e filesystem check for now
                    if (System.IO.File.Exists(wemPath)) { return wemPath; }

                    // Make folder if doesn't exist (this is so ugly lmao)
                    if (!System.IO.Directory.Exists(lineDir)) { System.IO.Directory.CreateDirectory(lineDir); }

                    var l = $"<speak>{line}<break time='500ms'/></speak>";
                    var voice = npc.sex == NpcContent.Sex.Female ? "slt" : "rms";
                    {
                        ProcessStartInfo startInfo = new(flitePath)
                        {
                            Arguments = $"-ssml \"{l}\" -voice {voice} \"{wavPath}\"",
                            RedirectStandardOutput = true,
                            WorkingDirectory = lineDir,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var process = Process.Start(startInfo);
                        process.WaitForExit();
                    }

                    // Convert wav to wem
                    // Setup paths, make folders
                    string wwiseConsolePath = $"{Const.WWISE_PATH}WwiseConsole.exe";
                    string xmlName = $"{hashName}.wsources";
                    string xmlPath = $"{lineDir}{xmlName}";
                    string xmlRelative = $"..\\dialog\\{npc.race}\\{npc.sex}\\{dialog.id}\\{hashName}\\{xmlName}";
                    string projectDir = $"{Const.CACHE_PATH}wwise\\";
                    string projectPath = $"{projectDir}wwise.wproj";

                    // Create XML file
                    string xmlRaw = $""""
                                <?xml version='1.0' encoding='UTF-8'?>
                                <ExternalSourcesList SchemaVersion="1" Root="{lineDir}"><Source Path="{hashName}.wav" Conversion="Vorbis Quality High" /></ExternalSourcesList>
                                """";
                    File.WriteAllText(xmlPath, xmlRaw);

                    // Create project if it doesn't exist
                    if (!File.Exists(projectPath))
                    {
                        if (Directory.Exists(projectDir)) { Directory.Delete(projectDir); } // creating a wwise proj requires the folder to not exist
                        ProcessStartInfo startInfo = new(wwiseConsolePath)
                        {
                            WorkingDirectory = Const.CACHE_PATH,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.AddRange(new string[] { "create-new-project", $"\"{projectPath}\"", "--platform", "Windows" });
                        using var process = Process.Start(startInfo);
                        process.WaitForExit();
                    }

                    // Call wwise console to convert wav to wem
                    {
                        ProcessStartInfo startInfo = new(wwiseConsolePath)
                        {
                            RedirectStandardOutput = true,
                            WorkingDirectory = lineDir,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.AddRange(new string[] { "convert-external-source", $"\"{projectPath}\"", "--source-file", xmlRelative, "--output", "Windows", $"\"{lineDir}\"" });
                        using var process = Process.Start(startInfo);
                        process.WaitForExit();
                    }
                }
                catch
                {
                    Lort.Log($"## ERROR ## Failed to generate dialog {wavPath}", Lort.Type.Debug);
                }

                if (File.Exists(wemPath)) { break; } // if the file is created successfully we don't need to retry.
            }

            if (!File.Exists(wemPath))
            {
                throw new System.Exception($"Failed to generated line {wemPath} despite {Const.SAM_MAX_RETRY} retry attempts.");
            }

            // Return wem path
            return wemPath;
        }
    }
}
