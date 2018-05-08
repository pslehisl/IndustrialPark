﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IndustrialPark
{
    static class Program
    {
        public static MainForm mainForm;

        public static ViewConfig viewConfig;

        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            mainForm = new MainForm();
            viewConfig = new ViewConfig();

            Application.Run(mainForm);
        }
    }
}
