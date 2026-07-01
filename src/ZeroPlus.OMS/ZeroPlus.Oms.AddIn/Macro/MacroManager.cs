using ExcelDna.Integration;
using NLog;
using System;

namespace ZeroPlus.Oms.AddIn.Macro
{
    internal class MacroManager
    {

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public MacroManager()
        {
            if (OmsCore.DominatorClient != null)
            {
                OmsCore.DominatorClient.MacroTriggerRequestEvent += DominatorClient_MacroTriggerRequestEvent;
            }
        }

        private void DominatorClient_MacroTriggerRequestEvent(string macro, object[] args, DateTime timestamp)
        {
            TriggerMacro(macro, args, timestamp.ToUniversalTime().Ticks);
        }

        public static void TriggerMacro(string macroName, object[] args, long timestamp)
        {
            try
            {
                if ((args == null) || (args.Length == 0))
                {
                    ExcelAsyncUtil.QueueMacro(macroName);
                }
                else
                {
                    ExcelAsyncUtil.QueueAsMacro(delegate
                    {
                        dynamic xlApp = ExcelDnaUtil.Application;
                        xlApp.Run(macroName, args);
                    });
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(TriggerMacro));
            }
        }
    }
}
