namespace TestDemo
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
#if WITH_CLIPBOARD_TEST
            // 都要先把插件工程引进来：
            //   dotnet run --project TestDemo -p:WithClipboardTest=true -- --clipboard-test
            //   dotnet run --project TestDemo -p:WithClipboardTest=true -- --keyboard-test
            if (args.Contains("--clipboard-test"))
            {
                return ClipboardSnapshotTest.Run();
            }

            if (args.Contains("--keyboard-test"))
            {
                return KeyboardLayoutTest.Run();
            }

            if (args.Contains("--recorder-preview"))
            {
                return ShortcutRecorderPreview.Run(args);
            }
#endif

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            return 0;
        }
    }
}
