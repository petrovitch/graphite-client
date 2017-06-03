using System;
using System.Diagnostics;
using System.Linq;
using log4net;

namespace Graphite.System
{
    internal class CounterListener : IDisposable
    {
	    private static readonly ILog logger = LogManager.GetLogger(typeof(CounterListener));
	    readonly string _instance;
	    private PerformanceCounter counter;
        
        private bool disposed;

        public CounterListener(string category, string instance, string counter)
        {
	        _instance = instance;
	        try
            {
	            this.counter = new PerformanceCounter(category, counter, ResolveInstanceName(category, instance));
				
                this.counter.Disposed += (sender, e) => this.disposed = true;

                // First call to NextValue returns always 0 -> perforn it without taking value.
                this.counter.NextValue();
            }
            catch (InvalidOperationException exception)
            {
				throw new InvalidOperationException(
                    exception.Message + string.Format(" (Category: '{0}', Counter: '{1}', Instance: '{2}')", category, counter, instance),
                    exception);
            }
        }

	    /// <summary>
        /// Reads the next value from the performance counter.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">The object or underlying performance counter is already disposed.</exception>
        /// <exception cref="InvalidOperationException">Connection to the underlying counter was closed.</exception>
        public float? ReportValue()
        {
            if (this.disposed)
            {
	            this.RenewCounter();
	            return null;
            }

            try
            {
                // Report current value.
                return this.counter.NextValue();
            }
            catch (InvalidOperationException)
            {
                // Connection to the underlying counter was closed.

                this.Dispose(true);
				
                this.RenewCounter();

                return null;
            }
        }

	    public void Dispose()
        {
            this.Dispose(true);

            GC.SuppressFinalize(this);
        }

	    protected virtual void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                if (this.counter != null)
                {
                    this.counter.Dispose();
                }

                this.disposed = true;
            }
        }

	    protected virtual void RenewCounter()
        {

			try {
				this.counter = new PerformanceCounter(this.counter.CategoryName,
                this.counter.CounterName,
                ResolveInstanceName(this.counter.CategoryName, _instance));

				this.counter.Disposed += (sender, e) => this.disposed = true;

				this.disposed = false;

                // First call to NextValue returns always 0 -> perforn it without taking value.
                this.counter.NextValue();
            }
            catch (InvalidOperationException)
            {
                // nop
            }
        }

	    static string ResolveInstanceName(string category, string instance)
	    {
			// Sometimes a counter like ".Net Data Provider for SQLServer" will put the pid in the instance name. 
			// Not much we can do other than fetch by the first pid matching the process name.
			if (category == ".Net Data Provider for SqlServer" && !instance.EndsWith("]")) {
				return GetSqlProviderInstanceNameByProcessName(category, instance);
			}

			// Look for an instance name that exactly matches the specified instance
			var allInstanceNames = new PerformanceCounterCategory(category).GetInstanceNames();
		    var matched = allInstanceNames.FirstOrDefault(i => i.Equals(instance, StringComparison.CurrentCultureIgnoreCase));
		    if (matched != null)
		    {
			    return matched;
		    }

			// Check for a specified pipe delimited list of options and find the first match or just pick the first instance.
			return instance.Split('|')
				.Select(i => i.Trim())
				.Select(i => allInstanceNames.FirstOrDefault(a => a.Equals(i, StringComparison.InvariantCultureIgnoreCase)))
				.FirstOrDefault() ?? allInstanceNames.FirstOrDefault();
	    }
		
	    static string GetSqlProviderInstanceNameByProcessName(string category, string instance)
	    {
		    var processes = Process.GetProcessesByName(instance);
		    if (!processes.Any())
		    {
			    logger.WarnFormat("Could not find any processes by the name {0}", instance);
			    return null;
		    }

		    var allInstanceNames = new PerformanceCounterCategory(category).GetInstanceNames();
		    var instanceNames = from instanceName in allInstanceNames
			    from process in processes
			    where instanceName.EndsWith(string.Format("[{0}]", processes.First().Id))
			    select instanceName;

		    var matchedInstanceName = instanceNames.FirstOrDefault();

		    if (matchedInstanceName == null)
		    {
			    logger.WarnFormat(
				    "Could not find any counter instances that match the process name {0}.\r\n Processes={1}\r\nInstances={2}", instance,
				    string.Join(Environment.NewLine, processes.Select(p => p.ProcessName)),
				    string.Join(Environment.NewLine, allInstanceNames));
		    }
		    return matchedInstanceName;
	    }
    }
}
