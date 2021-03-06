using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace UCSRestarter
{
    // The main restarter class.
    public class Restarter
    {
        public Restarter(string path)
        {
            RestartedTimes = new List<DateTime>();
            _path = path;
        }

        // Determines if the Restarter is running (returns _started).
        public bool Started => _started;

        // Determines if the process has crashed.
        public bool HasCrashed
        {
            get
            {
                if (GetWerFaultProcess() != null)
                    return true;

                return false;
            }
        }

        // Number of times the application was restarted.  => RestartedTimes.Count
        public int RestartCount { get { return RestartedTimes.Count; } }

        // List of DateTime the application was restarted.
        public List<DateTime> RestartedTimes { get; private set; }

        // DateTime of the next restart time.
        public DateTime NextRestart { get; private set; }

        // DateTime of when the Restarter started.
        public DateTime StartTime { get; private set; }

        // Duration between each restart interval.
        public TimeSpan RestartInterval { get; set; }

        public TimeSpan AverageRunTime
        {
            get
            {
                // Avoid divide by 0 exception.
                if (RestartedTimes.Count == 0)
                    return default(TimeSpan);

                return TimeSpan.FromTicks((DateTime.Now - StartTime).Ticks / RestartedTimes.Count);
            }
        }

        // Thread that does the restarting stuff.
        private Thread _restarterThread;
        // Process of application to restart.
        private Process _process;
        // Path to .exe file.
        private string _path;
        // Determines if the Restarter is running.
        private bool _started;

        // Starts the Restarter.
        public void Start()
        {
            _started = true;
            _process = Process.Start(_path);

            NextRestart = DateTime.Now.Add(RestartInterval);
            StartTime = DateTime.Now;

            _restarterThread = new Thread(HandleRestarting);
            _restarterThread.Name = "Restarter Thread";
            _restarterThread.Start();
        }

        // Stops the Restarter.
        public void Stop()
        {
            if (_started)
            {
                _started = false;
                _restarterThread.Abort();
            }
            else throw new InvalidOperationException("Bro-exception occurred!");
        }

        // Handles the restarting of stuff by checking if it crashes..
        private void HandleRestarting()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        var remaining = (NextRestart - DateTime.Now).ToString(@"hh\:mm\:ss\.f");
                        var avgRunTime = AverageRunTime.ToString(@"hh\:mm\:ss\.f");
                        var title = "UCS Restarter - Remaining: " + remaining + ", Count: " + RestartedTimes.Count + ", Average Run Time: " + avgRunTime;
                        Console.Title = title;

                        // Check if has crashed.
                        var hasCrashed = HasCrashed;
                        if (hasCrashed)
                        {
                            ConsoleUtils.WriteLineError("Detected that UCS has crashed\n\t-> Restarting UCS at " + DateTime.Now);

                            // Kill WerFault.exe to cause UCS process to exit.
                            var werFault = GetWerFaultProcess();
                            werFault.Kill();

                            Restart();
                        }

                        // Check if it has exited.
                        var hasExited = _process.HasExited;
                        if (hasExited)
                        {
                            ConsoleUtils.WriteLineError("Detected that UCS has exited\n\t-> Restarting UCS at " + DateTime.Now);

                            Restart();
                        }

                        // Check if we have NextRestart time has passed.
                        if (DateTime.Now >= NextRestart)
                        {
                            ConsoleUtils.WriteLineInfo(RestartInterval + " has passed.\n\t-> Restarting UCS at " + DateTime.Now);

                            Restart();
                        }

                        // Be sure to sleep because we don't want to kill the CPU.
                        Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        ConsoleUtils.WriteLineError("Exception occurred while running -> \n\t" + ex);
                        Console.ResetColor();
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // We don't care about these types of exceptions.
            }
        }

        private Process GetWerFaultProcess()
        {
            var processes = Process.GetProcessesByName("WerFault");
            for (int i = 0; i < processes.Length; i++)
            {
                var fileDescription = _process.MainModule.FileVersionInfo.FileDescription;
                if (processes[i].MainWindowTitle.Contains(fileDescription))
                    return processes[i];
            }
            return null;
        }

        private void Restart()
        {
            // Make sure it is not dead before killing it because we don't want to kill a dead process.
            if (!_process.HasExited)
                _process.Kill();

            _process = Process.Start(_path);
            RestartedTimes.Add(DateTime.Now);
            NextRestart = DateTime.Now.Add(RestartInterval);
            ConsoleUtils.WriteLineInfo("UCS has restarted successfully at " + DateTime.Now);
        }
    }
}
