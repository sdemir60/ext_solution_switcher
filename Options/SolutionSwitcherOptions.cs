using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Drawing;

namespace SolutionSwitcher.Options
{
    public class SolutionSwitcherOptions : DialogPage
    {
        [Category("Index")]
        [DisplayName("Root Directory")]
        [Description("Tarama yapılacak kök dizin (recursive). .sln dosyaları bulunur, projeler çıkarılır.")]
        public string RootDirectory { get; set; } = "";

        [Category("Behavior")]
        [DisplayName("Open In New Window")]
        [Description("Solution’ı yeni VS penceresinde aç.")]
        public bool OpenInNewWindow { get; set; } = false;

        [Category("Appearance")]
        [DisplayName("Accent Color (#F59F00)")]
        public string AccentHex { get; set; } = "#F59F00";

        [Category("Appearance")]
        [DisplayName("Accent Light Background")]
        [Description("Vurgu arkaplanı için açık ton (örn. #FFF3D6)")]
        public string AccentLightBackground { get; set; } = "#FFF3D6";

        [Category("Index")]
        [DisplayName("Rescan on Startup")]
        public bool RescanOnStartup { get; set; } = true;

        [Category("Index")]
        [DisplayName("Max Parallelism")]
        public int MaxParallelism { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
    }
}