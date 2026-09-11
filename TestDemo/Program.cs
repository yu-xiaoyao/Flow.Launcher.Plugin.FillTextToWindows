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
            // 剪贴板测试：dotnet run --project TestDemo -p:WithClipboardTest=true -- --clipboard-test
            if (args.Contains("--clipboard-test"))
            {
                return ClipboardSnapshotTest.Run();
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
