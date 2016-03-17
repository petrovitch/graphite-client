using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;

namespace Graphite.System
{
    internal class AppPoolListener
    {
		private static readonly ILog logger = LogManager.GetLogger(typeof(AppPoolListener));
		private readonly string appPoolName;
        private readonly string category;
        private readonly string counter;

        private string counterName;

        private CounterListener counterListener;
	    static readonly string NetDataProviderForSqlserver = ".Net Data Provider for SqlServer";

	    public AppPoolListener(string appPoolName, string category, string counter)
        {
            this.appPoolName = appPoolName;
            this.category = category;
            this.counter = counter;

            this.LoadCounterName();
        }
        
        public bool LoadCounterName()
        {
            string newName = category == NetDataProviderForSqlserver ? this.GetDataProviderCounterName((this.appPoolName)) : this.GetCounterName(this.appPoolName);

            if (!string.IsNullOrEmpty(newName) && this.counterName != newName)
            {
                if (this.counterListener != null)
                {
                    this.counterListener.Dispose();

                    this.counterListener = null;
                }

				this.counterName = newName;
				
                return true;
            }

	        return false;
        }

        public float? ReportValue()
        {
            // AppPool not found -> is not started.
            if (string.IsNullOrEmpty(this.counterName) && !LoadCounterName())
                return null;

            if (this.counterListener == null)
            {
                try
                {
                    this.counterListener = new CounterListener(category, this.counterName, counter);
                }
                catch (InvalidOperationException e)
                {
					logger.Error(e);
				}
            }

            if (this.counterListener == null)
                return null;

            try
            {


                return this.counterListener.ReportValue(); ;
            }
            catch (InvalidOperationException e)
            {
				logger.Error(e);
				// counter not available.
				this.counterListener = null;

                return null;
            }
        }



        private string GetCounterName(string appPool)
        {
            string result;

            this.Execute("list WP", out result, 1000);

            var match = Regex.Match(
                result, 
                "WP \"(?<id>[0-9]+)\" \\(applicationPool:" + Regex.Escape(appPool) + "\\)", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            int processId;

            if (match.Success && match.Groups["id"].Success && int.TryParse(match.Groups["id"].Value, out processId))
            {
                return this.ProcessNameById("w3wp", processId);
            }

            return null;
        }

		private string GetDataProviderCounterName(string appPool)
		{
			string result;

			this.Execute("list WP", out result, 1000);

			var match = Regex.Match(
				result,
				"WP \"(?<id>[0-9]+)\" \\(applicationPool:" + Regex.Escape(appPool) + "\\)",
				RegexOptions.IgnoreCase | RegexOptions.Singleline);

			int processId;

			if (match.Success && match.Groups["id"].Success && int.TryParse(match.Groups["id"].Value, out processId)) {
				var instanceNames = new PerformanceCounterCategory(NetDataProviderForSqlserver).GetInstanceNames();
				
				return instanceNames.FirstOrDefault(n => n.EndsWith(string.Format("[{0}]", processId)));
			}

			return null;
		}

		private string ProcessNameById(string prefix, int processId)
        {
            var localCategory = new PerformanceCounterCategory("Process");

            string[] instances = localCategory.GetInstanceNames()
                .Where(p => p.StartsWith(prefix))
                .ToArray();

            foreach (string instance in instances)
            {
                using (var localCounter = new PerformanceCounter("Process", "ID Process", instance, true))
                {
                    long val = localCounter.RawValue;

                    if (val == processId)
                    {
                        return instance;
                    }
                }
            }

            return null;
        }

        private bool Execute(string arguments, out string result, int maxMilliseconds = 30000)
        {
            string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(systemPath, "inetsrv\\appcmd.exe"),
                Arguments = arguments,

                RedirectStandardOutput = true,

                UseShellExecute = false,
                CreateNoWindow = true,
            };

	        try
	        {
		        var standardOut = new StringBuilder();

		        Process p = Process.Start(startInfo);

		        p.OutputDataReceived += (s, d) => standardOut.AppendLine(d.Data);
		        p.BeginOutputReadLine();

		        bool success = p.WaitForExit(maxMilliseconds);
		        p.CancelOutputRead();

		        if (!success)
		        {
			        try
			        {
				        p.Kill();
			        }
			        catch (Win32Exception e)
			        {
						logger.Error(e);
						// unable to kill the process
					}
			        catch (InvalidOperationException e)
			        {
						logger.Error(e);
						// process already stopped
					}
		        }

		        result = standardOut.ToString();

		        return success;
	        }
	        catch (Exception e)
	        {
				logger.Error(e);
				result = string.Empty;
		        return false;
	        }
        }
	}
}
