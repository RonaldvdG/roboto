﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Roboto
{
    /// <summary>
    /// Class for logging data in a standard format. 
    /// </summary>
    public class logging
    {
        public enum loglevel { verbose, low, normal, warn, high, critical }
        private TextWriter textWriter = null;
        private bool initialised = false;
        private bool followOnLine = false;
        private Char bannerChar = "*".ToCharArray()[0];
        private DateTime currentLogFileDate = DateTime.MinValue;
        private DateTime logLastFlushed = DateTime.Now;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="level"></param>
        /// <param name="colour"></param>
        /// <param name="noLineBreak"></param>
        /// <param name="banner"></param>
        /// <param name="pause"></param>
        /// <param name="skipheader"></param>
        /// <param name="skipLevel">Levels of the stack to skip when getting the calling class</param>
        public void log(string text, loglevel level = loglevel.normal, ConsoleColor colour = ConsoleColor.White, bool noLineBreak = false, bool banner = false, bool pause = false, bool skipheader = false, int skipLevel = 1)
        {
            //TODO - add exceptions into the log. 

            //check logfile correct
            if (initialised && Roboto.Settings.enableFileLogging && DateTime.Now > currentLogFileDate.AddHours(Roboto.Settings.rotateLogsEveryXHours))
            {
                initialised = false;
                try
                {
                    log("Rotating Logs", loglevel.warn, ConsoleColor.White, false, true);
                    finalise();
                    initialise();
                }
                catch (Exception e)
                {
                    initialised = false;
                    log("Error rotating logs! File logging disabled. " + e.ToString(), loglevel.critical);
                }
            }

            if (logLastFlushed < DateTime.Now.AddMinutes(-5) )
            {
                textWriter.Flush();
                logLastFlushed = DateTime.Now;
                log("Flushed logfile", loglevel.low);
                
            }


            StackFrame frame = new StackFrame(skipLevel);
            var method = frame.GetMethod();
            string classtype = method.DeclaringType.ToString();
            string methodName = method.Name;

            

            //guess colour
            if (colour == ConsoleColor.White)
            {
                switch(level)
                {
                    case loglevel.verbose:
                        colour = ConsoleColor.Gray;
                        break;
                    case loglevel.low:
                        colour = ConsoleColor.DarkGreen;
                        break;
                    case loglevel.normal:
                        colour = ConsoleColor.Cyan;
                        break;
                    case loglevel.warn:
                        colour = ConsoleColor.Magenta;
                        break;
                    case loglevel.high:
                        colour = ConsoleColor.Yellow;
                        if (initialised)
                        {
                            Roboto.Settings.stats.logStat(new statItem("High Errors", typeof(logging)));
                        }
                        break;
                    case loglevel.critical:
                        colour = ConsoleColor.Red;
                        if (initialised)
                        {
                            Roboto.Settings.stats.logStat(new statItem("Critical Errors", typeof(logging)));
                        }
                        break;
                }
            }

            Console.ForegroundColor = colour;
            
            if (noLineBreak)
            {
                write(text);
                followOnLine = true;
            }
            else
            {
                //clear any trailing lines from write's instead of writelines
                if (followOnLine)
                {
                    writeLine("");
                    followOnLine = false;
                }
                //add our time and module stamps
                string outputString = "";
                if (banner == false && skipheader == false)
                {
                    outputString += DateTime.Now.ToString("dd-MM-yyyy  HH:mm:ss") + " - " 
                        + level.ToString().Substring(0,2).ToUpper() + " - "
                        + (classtype.ToString()  + ":" + methodName).PadRight(45)
                        +  " - ";
                }
                else
                {
                    outputString += "".PadRight(53);
                }

                //add the main text
                outputString +=  text;

                //add a row of *** for a banner
                if (banner) { writeLine("".PadLeft(Console.WindowWidth-1, bannerChar)); }
                //write the main line
                writeLine(outputString);
                //another row of ***
                if (banner) { writeLine("".PadLeft(Console.WindowWidth-1, bannerChar)); }
                //pause if needed
                if (pause)
                {
                    writeLine("Press any key to continue");
                    Console.ReadKey();
                }

            }
            Console.ResetColor();
        }

        internal void finalise()
        {
            log("Closing logfile", loglevel.warn );
            textWriter.Flush();
            textWriter.Close();
            initialised = false;
        }

        private void writeLine(string s)
        {
            s = cleanse(s);
            Console.WriteLine(s);

            if (initialised && textWriter != null)
            {
                textWriter.WriteLine(s);
            }
        }

        private void write(string s)
        {
            //cleanse our text of anything we shouldnt log
            s = cleanse(s);
            
            Console.Write(s);
            if (Roboto.Settings.enableFileLogging && textWriter != null)
            {
                textWriter.Write(s);
            }
        }

        private string cleanse(string s)
        {
            if (Roboto.Settings != null && Roboto.Settings.telegramAPIKey != null)
            {
                s = s.Replace(Roboto.Settings.telegramAPIKey, "<APIKEY>");
            }
            return s;
        }


        public void initialise()
        {
            //Set up any stats
            Roboto.Settings.stats.registerStatType("Critical Errors", typeof(logging), Color.Crimson, stats.displaymode.bar);
            Roboto.Settings.stats.registerStatType("High Errors", typeof(logging), Color.Orange, stats.displaymode.bar);
            
            //todo - remove any logs older than x days.

            if (Roboto.Settings.enableFileLogging)
            {
                //Setup our logging
                currentLogFileDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
                string logfile = settings.foldername + Roboto.Settings.botUserName + " " + DateTime.Now.ToString("yyyy-MM-dd HH") + ".log";
                textWriter = new StreamWriter(logfile, true);
                for (int i = 0; i < 10; i++) { textWriter.WriteLine(); }
                initialised = true;
                log("Enabled logging to file " + logfile, loglevel.warn);

            }
            else
            {
                log("File logging is disabled. Enable in the xml configuration file." );
            }

            

        }

        public void setTitle(string title)
        {
            Console.Title = "Roboto - " + title;
        }
    }
}
