﻿namespace PokemonBattleJournal
{
    public partial class App : Application
    {
        public App()
        {
            //Register Syncfusion license
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("MzY3NjYwMkAzMjM4MmUzMDJlMzBuWkMvdUxhYkNPWERMQndYazNyU1gzWnVoN29Zb2dxU1AxQTk2K0k3aXNFPQ==");
            InitializeComponent();
            
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
            // It can also be defined as an Object Initializer
            //return new Window() { Page = new AppShell() };
        }
    }
}