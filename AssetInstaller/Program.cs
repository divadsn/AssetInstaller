﻿using AssetInstaller.Utils;
using CommandLine;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Windows.Forms;

namespace AssetInstaller
{
    static class Program
    {
        class Options
        {
            [Value(0, MetaName = "path", HelpText = "Path for installation.")]
            public string Path { get; set; }

            [Option("reinstall", HelpText = "Reinstall flag.")]
            public bool Reinstall { get; set; }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Parser parser = new Parser(with =>
            {
                with.CaseSensitive = false;
            });

            parser.ParseArguments<Options>(args).WithParsed<Options>(opts => RunWithOptions(opts));
        }

        static void RunWithOptions(Options options)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if installer is run inside Trainz installation path
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "bin", "TrainzUtil.exe")))
            {
                MessageBox.Show("Die Installation kann nicht im Trainz-Installationsverzeichnis ausgeführt werden. Bitte verschieben Sie die Datei in einen anderen Ordner und versuchen Sie es erneut.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            // Check if installer is run with reinstall option
            if (options.Reinstall && File.Exists(".lastinstall"))
            {
                DialogResult result = MessageBox.Show("Wollen Sie wirklich mit der Neuinstallation fortfahren?\nDies kann einige Zeit in Anspruch nehmen.", "Warnung!", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (result == DialogResult.Yes)
                {
                    File.Delete(".lastinstall");
                }
            }

            TrainzUtil trainzUtil = new TrainzUtil(options.Path ?? RegistryUtils.FindTrainzInstallation());

            // Check if Trainz installation was found on the system and show folder picker if not
            if (trainzUtil.ProductInstallPath is null || !File.Exists(Path.Combine(trainzUtil.ProductInstallPath, "bin", "TrainzUtil.exe")))
            {
                using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
                {
                    dialog.Title = "Bitte Installationsverzeichnis auswählen, in dem sich Trainz.exe befindet...";
                    dialog.IsFolderPicker = true;

                    CommonFileDialogResult result = dialog.ShowDialog();

                    if (result == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
                    {
                        trainzUtil.ProductInstallPath = dialog.FileName;
                    }
                    else
                    {
                        Environment.Exit(0);
                        return;
                    }
                }
            }

            // Check if installation path contains TrainzUtil.exe
            if (!File.Exists(Path.Combine(trainzUtil.ProductInstallPath, "bin", "TrainzUtil.exe")))
            {
                MessageBox.Show("TrainzUtil.exe konnte nicht im Installationsverzeichnis gefunden werden. Bitte überprüfen Sie den gewählten Pfad und versuchen Sie es erneut.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            // Check if Trainz is not running
            if (Process.GetProcessesByName("trainz").Length != 0 || Process.GetProcessesByName("contentmanager").Length != 0)
            {
                MessageBox.Show("Trainz muss geschlossen werden, um mit der Installation fortfahren zu können. Bitte schließen Sie das Spiel und versuchen Sie es erneut.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            // Check if we have write permissions to installation path
            if (!FileUtils.DirectoryHasPermission(trainzUtil.ProductInstallPath, FileSystemRights.Write))
            {
                RestartWithElevatedPrivileges(new string[] { trainzUtil.ProductInstallPath });
                return;
            }

            // Check if Nvidia GPU is installed on the system
            if (SystemGpuInfo.IsNvidia && !File.Exists(".lastinstall"))
            {
                DialogResult result = MessageBox.Show("Auf diesem System wurde eine Nvidia-Grafikkarte erkannt!\n\nEs kann zu Fehlern bei Texturen kommen, wenn die hardwarebeschleunigte Texturkompression in Trainz während der Installation aktiviert ist. Bitte stellen Sie sicher, dass diese Einstellung im Content Manager ausgeschaltet ist, bevor Sie mit der Installation fortfahren.\n\nSind Sie sicher, dass Sie fortfahren möchten? ", "Warnung!", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (result == DialogResult.No)
                {
                    return;
                }
            }

            Application.Run(new InstallerForm(trainzUtil));
        }

        static void RestartWithElevatedPrivileges(string[] args)
        {
            ProcessStartInfo proc = new ProcessStartInfo();
            proc.WorkingDirectory = Environment.CurrentDirectory;
            proc.FileName = Application.ExecutablePath;
            proc.UseShellExecute = true;
            proc.Verb = "runas";

            foreach (string arg in args)
            {
                proc.Arguments += String.Format("\"{0}\" ", arg);
            }

            try
            {
                Process process = Process.Start(proc);
                process.WaitForExit();
            }
            catch
            {
                MessageBox.Show("Die Installation kann ohne Administratorrechte nicht fortgesetzt werden.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }
    }
}
