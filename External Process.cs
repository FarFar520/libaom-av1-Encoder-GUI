using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace 破片压缩器 {
    internal class External_Process {
        public int pid = int.MaxValue;
        public Process process;
        public List<string> listError = new List<string>( );
        List<string> listOutput = new List<string>( );

        public string StandardOutput = string.Empty, StandardError = string.Empty;
        public string get_ffmpeg_Pace => ffmpeg_Pace + str剩余;

        string ffmpeg_Pace = string.Empty;
        string ffmpeg_Encoding = string.Empty;
        bool newFrame = false;

        int index_frame = -1;
        public uint encodingFrames = 0;
        public double encFps = 0.0f;

        public Stopwatch stopwatch = new Stopwatch( );
        public TimeSpan span输入时长 = TimeSpan.Zero;

        public FileInfo fi源, fi编码;

        public DirectoryInfo di编码成功, di输出文件夹;

        public string str成功文件名 = string.Empty;

        public bool b已结束 = false, b安全退出 = false, b补齐时间戳 = false, b单线程 = true, b无缓转码 = false;

        public StringBuilder sb输出数据流 = new StringBuilder( );//全局变量有调用，直接初始化。

        public static Regex regexFrame = new Regex(@"frame=\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Regex regexSize = new Regex(@"size=\s*(\d+(?:\.\d+)?)KiB", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex regexBitrate = new Regex(@"bitrate=\s*(\d+(?:\.\d+)?)kbits/s", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex regexTime = new Regex(@"time=\s*(\d+[\d:\.]+\d+)\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public VTimeBase.Span偏移 span偏移;

        /// <summary>
        /// 调用外部可执行程序类初始化，对同文件夹读写
        /// </summary>
        /// <param name="exe文件">可执行程序，也支持系统环境变量配置的程序名</param>
        /// <param name="str命令行">拼装完整的命令行</param>
        /// <param name="fi输入文件">供文件归档函数使用的输入文件</param>
        /// <returns>工作目录 = 输入&输出路径，命令行中可使用【输入文件名】</returns>
        public External_Process(string exe文件, string str命令行, FileInfo fi输入文件) {
            fi源 = fi输入文件;
            di输出文件夹 = fi输入文件.Directory;
            this.str成功文件名 = fi输入文件.Name;

            listError.Add(DateTime.Now.ToString( ));
            listError.Add($"{exe文件} {str命令行}");

            process = new Process( );
            process.StartInfo.FileName = exe文件;
            process.StartInfo.Arguments = str命令行;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WorkingDirectory = fi输入文件.DirectoryName;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        }

        /// <summary>
        /// 调用外部可执行程序类初始化，读取文件夹与写入文件夹可以不同
        /// </summary>
        /// <param name="exe文件">可执行程序，也支持系统环境变量配置的程序名</param>
        /// <param name="str命令行">拼装完整的命令行</param>
        /// <param name="str输出文件夹">外部可执行程序的工作目录，输出文件的相对路径</param>
        /// <param name="fi输入文件">供文件归档函数使用的输入文件</param>
        /// <returns>与输入文件不同路径→文件写入，命令行中需要使用【输入文件绝对路径】</returns>
        public External_Process(string exe文件, string str命令行, string str输出文件夹, FileInfo fi输入文件) {
            fi源 = fi输入文件;
            this.str成功文件名 = fi输入文件.Name;
            di输出文件夹 = new DirectoryInfo(str输出文件夹);

            listError.Add(DateTime.Now.ToString( ));
            listError.Add($"{exe文件} {str命令行}");

            process = new Process( );
            process.StartInfo.FileName = exe文件;
            process.StartInfo.Arguments = str命令行;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WorkingDirectory = str输出文件夹;//输出文件夹做为工作目录
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        }


        /// <summary>
        /// 切片编码初始化，调用外部可执行程序类.
        /// </summary>
        /// <param name="exe文件">可执行程序，也支持系统环境变量配置的程序名</param>
        /// <param name="str命令行">拼装完整的命令行</param>
        /// <param name="b单线程">是否绑定单核心</param>
        /// <param name="str成功文件名">去除编码信息的序列文件名</param>
        /// <param name="fi输入文件">供文件归档函数使用的输入文件</param>
        /// <param name="fi输出文件">正在编码的文件</param>
        /// <param name="di编码成功">成功文件存储目录</param>
        /// <returns>等待启动的编码任务</returns>
        public External_Process(string exe文件, string str命令行, bool b单线程, string str成功文件名, FileInfo fi输入文件, DirectoryInfo di编码成功) {
            fi源 = fi输入文件;

            this.b单线程 = b单线程;

            this.di编码成功 = di编码成功;
            this.str成功文件名 = str成功文件名;

            di输出文件夹 = fi输入文件.Directory;

            listError.Add(DateTime.Now.ToString( ));
            listError.Add($"{exe文件} {str命令行}");

            process = new Process( );
            process.StartInfo.FileName = exe文件;
            process.StartInfo.Arguments = str命令行;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WorkingDirectory = di输出文件夹.FullName;//输出文件夹做为工作目录
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        }

        /// <summary>
        /// 无缓转码初始化，调用外部可执行程序类.
        /// </summary>
        /// <param name="span偏移">时间戳数据</param>
        /// <param name="exe文件">可执行程序，也支持系统环境变量配置的程序名</param>
        /// <param name="str命令行">拼装完整的命令行</param>
        /// <param name="b单线程">是否绑定单核心</param>
        /// <param name="di输出文件夹">外部可执行程序的工作目录，输出文件的相对路径</param>
        /// <param name="fi输入文件">供文件归档函数使用的输入文件</param>
        /// <param name="di编码成功">成功文件存储目录</param>
        /// <param name="str成功文件名">去除编码信息的序列文件名</param>
        /// <returns>与输入文件不同路径→文件写入，命令行中需要使用【输入文件绝对路径】</returns>
        public External_Process(VTimeBase.Span偏移 span偏移, string exe文件, string str命令行, bool b单线程, FileInfo fi输入文件, DirectoryInfo di输出文件夹, DirectoryInfo di编码成功) {

            this.span偏移 = span偏移; b无缓转码 = true;

            fi源 = fi输入文件;

            this.b单线程 = b单线程;

            this.di编码成功 = di编码成功;
            this.di输出文件夹 = di输出文件夹;
            this.str成功文件名 = span偏移.i分段号.ToString( );

            listError.Add(DateTime.Now.ToString( ));
            listError.Add($"{exe文件} {str命令行}");

            process = new Process( );
            process.StartInfo.FileName = exe文件;
            process.StartInfo.Arguments = str命令行;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WorkingDirectory = fi输入文件.Directory.FullName;//字幕放到输入目录的时候，方便读取
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;

            ffmpeg_Pace = "按指定时间分段压缩，分段正在解码中…";
        }

        public void CountSpan_BitRate(ref TimeSpan sum_Span, ref double sum_KBit) {
            string time = regexTime.Match(ffmpeg_Pace).Groups[1].Value;
            if (TimeSpan.TryParse(time, out TimeSpan span)) {
                if (double.TryParse(regexBitrate.Match(ffmpeg_Pace).Groups[1].Value, out double kbits_sec)) {
                    if (kbits_sec > 9) {
                        sum_Span += span;
                        sum_KBit += kbits_sec * span.TotalSeconds;
                    }
                } else if (double.TryParse(regexSize.Match(ffmpeg_Pace).Groups[1].Value, out double KiB)) {
                    sum_Span += span;
                    sum_KBit += KiB * 8;
                }
            } else {
                double sec = Subtitle.match日时分秒_to_秒(time);
                sum_Span += TimeSpan.FromSeconds(sec);
                if (double.TryParse(regexBitrate.Match(ffmpeg_Pace).Groups[1].Value, out double kbits_sec)) {
                    if (kbits_sec > 9) {
                        sum_KBit += kbits_sec * span.TotalSeconds;
                    }
                } else if (double.TryParse(regexSize.Match(ffmpeg_Pace).Groups[1].Value, out double KiB)) {
                    sum_KBit += KiB * 8;
                }
            }
        }

        public bool HasFrame(out uint f) {
            f = encodingFrames;
            if (uint.TryParse(regexFrame.Match(ffmpeg_Pace).Groups[1].Value, out f)) {
                if (f > 0) {
                    encodingFrames = f;
                    if (stopwatch.ElapsedMilliseconds > 0)
                        encFps = encodingFrames * 1000.0f / stopwatch.ElapsedMilliseconds;
                    return true;
                } else
                    return false;
            } else return f > 0;
        }

        double pecent = 0;
        TimeSpan span剩余 = TimeSpan.Zero;
        string str剩余 = string.Empty;
        public double getFPS( ) {
            if (newFrame && stopwatch.ElapsedMilliseconds > 0) {
                newFrame = false;
                double sec = stopwatch.ElapsedMilliseconds / 1000;
                for (int i = index_frame; i < ffmpeg_Pace.Length; i++) {
                    if (ffmpeg_Pace[i] >= '0' && ffmpeg_Pace[i] <= '9') { //开头可能有空格，先找到数字开头。
                        long iframes = ffmpeg_Pace[i] - 48;
                        for (++i; i < ffmpeg_Pace.Length; i++) {
                            if (ffmpeg_Pace[i] >= '0' && ffmpeg_Pace[i] <= '9') {
                                iframes = iframes * 10 + ffmpeg_Pace[i] - 48;
                            } else {
                                encodingFrames = (uint)iframes;
                                int tend = ffmpeg_Pace.Length - 16;
                                for (++i; i < tend; i++) {
                                    if (ffmpeg_Pace[i] == 't') {
                                        if (ffmpeg_Pace[i + 1] == 'i' && ffmpeg_Pace[i + 2] == 'm' & ffmpeg_Pace[i + 3] == 'e' & ffmpeg_Pace[i + 4] == '=') {
                                            if (ffmpeg_Pace[i + 5] >= '0' && ffmpeg_Pace[i + 5] <= '9') {
                                                float enc_sec = 0;

                                                char hh_1 = ffmpeg_Pace[i + 5], hh_2 = ffmpeg_Pace[i + 6];
                                                char mm_1 = ffmpeg_Pace[i + 8], mm_2 = ffmpeg_Pace[i + 9];
                                                char ss_1 = ffmpeg_Pace[i + 11], ss_2 = ffmpeg_Pace[i + 12];
                                                char ms_1 = ffmpeg_Pace[i + 14], ms_2 = ffmpeg_Pace[i + 15];

                                                if (hh_2 >= '0' && hh_2 <= '9') enc_sec += (hh_1 - 48) * 36000 + (hh_2 - 48) * 3600;
                                                if (mm_1 >= '0' && mm_1 <= '9' && mm_2 >= '0' && mm_2 <= '9') enc_sec += (mm_1 - 48) * 600 + (mm_2 - 48) * 60;
                                                if (ss_1 >= '0' && ss_1 <= '9' && ss_2 >= '0' && ss_2 <= '9') enc_sec += (ss_1 - 48) * 10 + (ss_2 - 48);
                                                if (ms_1 >= '0' && ms_1 <= '9' && ms_2 >= '0' && ms_2 <= '9') enc_sec += (ms_1 - 48) / 10.0f + (ms_2 - 48) / 100.0f;

                                                pecent = enc_sec / span输入时长.TotalSeconds;
                                                if (pecent > 0.618f) {
                                                    str剩余 = "⏱️";
                                                    span剩余 = TimeSpan.FromSeconds(sec / enc_sec * (span输入时长.TotalSeconds - enc_sec));
                                                    if (span剩余.Days > 365) {
                                                        str剩余 += string.Format("{0:F0}年", span剩余.Days / 365);
                                                        int days = span剩余.Days % 365;
                                                        if (days > 31) {
                                                            str剩余 += string.Format("{0:F0}月", days / 31);
                                                            if (days % 31 > 0)
                                                                str剩余 += string.Format("{0}天", days % 31);
                                                        }
                                                    } else if (span剩余.Days > 31) {
                                                        str剩余 += string.Format("{0:F0}月", span剩余.Days / 31);
                                                        if (span剩余.Days % 31 > 0) str剩余 += string.Format("{0}天", span剩余.Days % 31);

                                                    } else if (span剩余.Days > 0) str剩余 += string.Format("{0}天", span剩余.Days);

                                                    if (span剩余.Hours > 0) str剩余 += string.Format("{0}小时", span剩余.Hours);

                                                    if (span剩余.Days == 0 && span剩余.Hours < 10 && span剩余.Minutes > 0)
                                                        str剩余 += string.Format("{0}分", span剩余.Minutes);

                                                    if (span剩余.Hours == 0)
                                                        str剩余 += string.Format("{0}秒", span剩余.Seconds);
                                                } else
                                                    str剩余 = pecent.ToString("P2");
                                            }
                                            break;
                                        }
                                    }
                                }


                                return encFps = iframes / sec;
                            }
                        }
                    }
                }
                if (uint.TryParse(regexFrame.Match(ffmpeg_Pace).Groups[1].Value, out encodingFrames)) {
                    return encFps = encodingFrames / sec;
                }
            }
            return encFps;
        }
        public void fx绑定编码进程到CPU单核心(int core) {
            if (转码队列.arr_单核指针.Length > 2 && process != null) {//转码队列.arr_单核指针 在调用函数前有为空判断
                try {
                    Process p = Process.GetProcessById(process.Id);
                    ProcessThreadCollection ths = p.Threads;
                    p.ProcessorAffinity = 转码队列.arr_单核指针[core];
                    for (int i = 0; i < ths.Count; i++)
                        ths[i].ProcessorAffinity = 转码队列.arr_单核指针[core];
                } catch (Exception err) { listError.Add(err.Message); }
            }
        }
        public bool sync( ) {
            process.OutputDataReceived += new DataReceivedEventHandler(OutputData);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorData);
            try {
                process.Start( );
                process.BeginOutputReadLine( );
                process.BeginErrorReadLine( );//异步读取缓冲无法和直接读取共同工作
            } catch {
                return false;
            }
            process.WaitForExit( );
            return process.ExitCode == 0;
        }//重定向读取错误输出和标准输出，函数可以阻塞原有进程；
        public bool sync(out List<string> OutputDataReceived, out List<string> ErrorDataReceived) {
            OutputDataReceived = listOutput;
            ErrorDataReceived = listError;
            process.OutputDataReceived += new DataReceivedEventHandler(OutputData);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorData);
            try {
                process.Start( );
                process.BeginOutputReadLine( );
                process.BeginErrorReadLine( );//异步读取缓冲无法和直接读取共同工作
            } catch {
                return false;
            }
            process.WaitForExit( );
            return process.ExitCode == 0;
        }//重定向读取错误输出和标准输出，函数可以阻塞原有进程；
        public bool async_FFmpeg编码( ) {
            bool run = false;
            try {
                process.Start( );
                pid = process.Id;
                process.PriorityClass = ProcessPriorityClass.Idle;
                run = true;
            } catch (Exception err) { listError.Add(err.Message); }

            if (run) {
                stopwatch.Start( );//计时开始。
                Thread thread_GetStandardError = new Thread(ffmpeg_读编码消息直到结束);
                thread_GetStandardError.Priority = ThreadPriority.AboveNormal;
                thread_GetStandardError.IsBackground = true;
                thread_GetStandardError.Start( );
                return true;//返回真代表加入等待队列。
            } else {
                string log = string.Empty;
                foreach (var t in listError)
                    log += t + "\r\n";

                try { File.WriteAllText($"{di输出文件夹.FullName}\\FFmpegAsync异常.{str成功文件名}.log", log); } catch { }
                return false;
            }
        }
        public bool async_FFmpeg(out Process pFFmpeg) {
            bool run = false;
            try {
                process.Start( );
                pid = process.Id;
                pFFmpeg = process;
                run = true;
            } catch (Exception err) {
                listError.Add(err.Message);
                pFFmpeg = null;
            }
            if (run) {
                stopwatch.Start( );
                Task.Run(( ) => {
                    while (!process.StandardOutput.EndOfStream) {
                        listOutput.Add(process.StandardOutput.ReadLine( ));
                    }
                });
                Task.Run(( ) => {
                    while (!process.StandardError.EndOfStream) {
                        string StandardError = process.StandardError.ReadLine( );
                        if (StandardError.StartsWith("frame=")) {
                            if (uint.TryParse(regexFrame.Match(StandardError).Groups[1].Value, out uint temp)) {
                                encodingFrames = temp;
                                encFps = 1000.0f * encodingFrames / stopwatch.ElapsedMilliseconds;
                            }
                        } else {
                            listError.Add(StandardError);
                        }
                    }
                });

                return true;
            } else {

                return false;
            }
        }
        public bool sync_FFmpegInfo(out List<string> arrLogs) {
            arrLogs = null;
            sb输出数据流 = new StringBuilder( );
            try {
                process.Start( );
                pid = process.Id;
            } catch {
                return false;
            }
            stopwatch.Start( );
            Task.Run(( ) => {
                try {
                    while (!process.StandardOutput.EndOfStream) {
                        try { listOutput.Add(process.StandardOutput.ReadLine( )); } catch { }
                    }
                } catch { }
            });
            while (!process.StandardError.EndOfStream) {
                StandardError = process.StandardError.ReadLine( ).TrimStart( );
                if (!string.IsNullOrEmpty(StandardError)) {
                    int iframe = StandardError.IndexOf("frame=") + 6;
                    if (iframe >= 6) {
                        index_frame = iframe;
                        ffmpeg_Pace = StandardError;
                        newFrame = true;
                    } else {
                        listError.Add(StandardError);
                        sb输出数据流.AppendLine(StandardError);
                    }

                }
            }
            process.WaitForExit( );
            arrLogs = listError;
            b安全退出 = process.ExitCode == 0;

            process.Dispose( );
            return b安全退出;
        }
        public bool sync_FFmpegInfo保存消息(string logFileName, out string[] arrLogs, ref StringBuilder builder) {
            sb输出数据流 = builder;
            builder.AppendLine( );
            arrLogs = null;
            try {
                process.Start( );
                pid = process.Id;
            } catch (Exception err) {
                builder.AppendLine(err.Message);
                return false;
            }
            while (!process.StandardError.EndOfStream) {
                StandardError = process.StandardError.ReadLine( ).TrimStart( );
                if (!string.IsNullOrEmpty(StandardError)) {
                    int iframe = StandardError.IndexOf("frame=") + 6;
                    if (iframe >= 6) {
                        index_frame = iframe;
                        ffmpeg_Pace = StandardError; newFrame = true;
                    } else {
                        listError.Add(StandardError);
                        builder.AppendLine(StandardError);
                    }
                }
            }
            if (!process.StandardOutput.EndOfStream)
                builder.AppendLine("包含输出消息").AppendLine(process.StandardOutput.ReadToEnd( ));

            process.WaitForExit( );
            arrLogs = listError.ToArray( );
            b安全退出 = process.ExitCode == 0;

            string fullPath;
            if (b安全退出) {
                fullPath = $"{fi源.DirectoryName}\\{logFileName}";
                try { File.WriteAllText(fullPath, builder.ToString( )); } catch { }
            } else if (Form破片压缩.b保存异常日志) {
                fullPath = $"{fi源.DirectoryName}\\FFMpeg异常.{logFileName}";
                builder.AppendFormat("\r\n异常退出代码：{0}", process.ExitCode);
                try { File.WriteAllText(fullPath, builder.ToString( )); } catch { }
            }
            process.Dispose( );
            return b安全退出;
        }

        public bool sync_FFProbeInfo保存消息(string logFileName, out string[] arrLogs, ref StringBuilder builder) {
            sb输出数据流 = builder;
            builder.AppendLine( );
            arrLogs = null;
            //async(ref builder);//开线程读取速度跟不上ffprobe退出速度，重定向会逻辑阻塞，增加了进程等待时间。
            try {
                process.Start( );
                pid = process.Id;
            } catch (Exception err) {
                builder.AppendLine(err.Message);
                return false;
            }
            while (!process.StandardError.EndOfStream || !process.StandardOutput.EndOfStream) {
                if (!process.StandardError.EndOfStream) {
                    StandardError = process.StandardError.ReadLine( ).TrimStart( );
                    if (!string.IsNullOrEmpty(StandardError)) {
                        listError.Add(StandardError);
                    }
                }
                if (!process.StandardOutput.EndOfStream) {
                    StandardOutput = process.StandardOutput.ReadLine( ).TrimStart( );
                    if (!string.IsNullOrEmpty(StandardOutput)) {
                        listOutput.Add(StandardOutput);
                    }
                }
            }

            for (int i = 0; i < listError.Count; i++) builder.AppendLine(listError[i]);//自检信息
            builder.AppendLine( );//为了排版整齐，头部信息输出和标准输出分开汇流
            for (int i = 0; i < listOutput.Count; i++) builder.AppendLine(listOutput[i]);//如果有JSON等，会输出到标准流。

            arrLogs = listError.ToArray( );
            b安全退出 = process.ExitCode == 0;

            string fullPath;
            if (b安全退出) {
                fullPath = $"{di输出文件夹.FullName}\\{logFileName}";
                try { File.WriteAllText(fullPath, builder.ToString( )); } catch { }
            } else if (Form破片压缩.b保存异常日志) {
                fullPath = $"{di输出文件夹.FullName}\\FFPrpeg异常.{logFileName}";
                builder.AppendFormat("\r\n异常退出代码：{0}", process.ExitCode);
                try { File.WriteAllText(fullPath, builder.ToString( )); } catch { }
            }
            process.Dispose( );
            return b安全退出;
        }

        public bool sync_MKVmerge保存消息(string str日志目录, string str日志文件名, out string[] arrLogs, ref StringBuilder builder) {
            sb输出数据流 = builder;
            builder.AppendLine( );
            arrLogs = null;
            try {
                process.StartInfo.Arguments += " --flush-on-close";//解决 发生mkvmerge进程退出，文件未完全写入磁盘的情况。
                process.Start( );
                pid = process.Id;
            } catch (Exception err) {
                builder.AppendLine(err.Message);
                return false;
            }
            Task.Run(( ) => { while (!process.StandardError.EndOfStream) listError.Add(process.StandardError.ReadLine( )); });

            List<string> listWarning = new List<string>( ), listMergeErr = new List<string>( );
            while (!process.StandardOutput.EndOfStream) {
                string line = process.StandardOutput.ReadLine( ).TrimStart( );
                builder.AppendLine(line);
                if (!string.IsNullOrEmpty(line) && !line.StartsWith("Progress")) {
                    if (line.StartsWith("Warning")) {
                        listWarning.Add(line);
                    } else if (line.StartsWith("Error")) {
                        listMergeErr.Add(line);
                    } else
                        listOutput.Add(line);
                }
            }
            process.WaitForExit( );

            if (listError.Count > 2) {
                builder.AppendLine("\r\n程序错误：");
                for (int i = 2; i < listError.Count; i++) builder.AppendLine(listError[i]);
            }

            if (listMergeErr.Count > 0) {
                builder.AppendLine("\r\n有错误：");
                for (int i = 0; i < listMergeErr.Count; i++) builder.AppendLine(listMergeErr[i]);
            }

            if (listWarning.Count > 0) {
                builder.AppendLine("\r\n有警告：");
                for (int i = 0; i < listWarning.Count; i++) builder.AppendLine(listWarning[i]);
            }

            builder.AppendLine( );
            for (int i = 0; i < listOutput.Count; i++) builder.AppendLine(listOutput[i]);

            arrLogs = listOutput.ToArray( );
            b安全退出 = process.ExitCode == 0;

            string fullPath;
            if (b安全退出) {
                fullPath = $"{str日志目录}\\{str日志文件名}";
                try { File.WriteAllText(fullPath, builder.ToString( )); } catch { }

            } else if (Form破片压缩.b保存异常日志) {
                fullPath = $"{str日志目录}\\MKVmerge异常.{str日志文件名}";
                builder.AppendFormat("\r\n异常退出代码：{0}", process.ExitCode);
                try { File.WriteAllText(fullPath, builder.ToString( )); } catch { }
            }
            process.Dispose( );
            return b安全退出;
        }

        void OutputData(object sendProcess, DataReceivedEventArgs output) {
            if (output.Data != null) {
                listOutput.Add(output.Data);
                sb输出数据流.AppendLine(output.Data);
            }
        }
        void ErrorData(object sendProcess, DataReceivedEventArgs output) {//标准输出、错误输出似乎共用缓冲区，只读其中一个，输出缓冲区可能会满，卡死
            if (output.Data != null) {
                listError.Add(output.Data);//异步中写逻辑会阻塞外部程序。使用线程休眠，可以让外部程序暂停。
                StandardError = output.Data;
                sb输出数据流.AppendLine(output.Data);
            }
        }
        void read_StandardOutput( ) {
            while (!process.StandardOutput.EndOfStream) {
                StandardOutput = process.StandardOutput.ReadLine( );
                if (!string.IsNullOrEmpty(StandardOutput)) {
                    listOutput.Add(StandardOutput);
                    sb输出数据流.AppendLine(StandardOutput);
                }
            }
        }

        void ffmpeg_读编码消息直到结束( ) {//一条子线程          
            //time编码开始 = DateTime.Now;
            stopwatch.Start( );
            StringBuilder builder日志 = new StringBuilder( );
            for (int i = 0; i < listError.Count; i++) builder日志.AppendLine(listError[i]);
            builder日志.AppendLine( );

            while (!process.StandardError.EndOfStream) {
                StandardError = process.StandardError.ReadLine( );
                if (!string.IsNullOrEmpty(StandardError)) {
                    if (StandardError.IndexOf("frame=") >= 0) {
                        ffmpeg_Pace = StandardError;
                        Form破片压缩.autoReset刷新输出.Set( );
                        builder日志.Insert(0, 硬件.str摘要);
                        builder日志.AppendLine(DateTime.Now + " 开始编码" + pid);
                        builder日志.AppendLine("------------------------------------------");
                        if (b无缓转码) {
                            span输入时长 = TimeSpan.FromSeconds(span偏移.f持续秒);
                            break;
                        }
                        if (get_ffprobe读取视频时长(fi源.FullName, out double sec)) {
                            span输入时长 = TimeSpan.FromSeconds(sec);
                            builder日志.Append("[FORMAT]\r\nduration=").Append(sec).Append("\r\n[/FORMAT]\r\n------------------------------------------");
                        }
                        break;
                    } else {
                        builder日志.AppendLine(StandardError);
                    }
                }
            }

            while (!process.StandardError.EndOfStream) {
                ffmpeg_Encoding = process.StandardError.ReadLine( );
                if (ffmpeg_Encoding.Length > 6 && ffmpeg_Encoding[0] == 'f' && ffmpeg_Encoding[1] == 'r' && ffmpeg_Encoding[2] == 'a' && ffmpeg_Encoding[3] == 'm' && ffmpeg_Encoding[4] == 'e' && ffmpeg_Encoding[5] == '=') {
                    //正常输出概率最高的情况用字符匹配加快效率
                    ffmpeg_Pace = ffmpeg_Encoding.TrimEnd( ) + '\t';
                    index_frame = 6;
                    newFrame = true;
                } else {
                    int iframe = ffmpeg_Encoding.IndexOf("frame=") + 6;
                    if (iframe >= 6) {//避免偶尔改变输出编码进度开头 frame=
                        ffmpeg_Pace = ffmpeg_Encoding.TrimEnd( ) + '\t';
                        index_frame = iframe;
                        newFrame = true;
                    } else {
                        StandardError = ffmpeg_Encoding;
                        builder日志.AppendLine(ffmpeg_Encoding);
                    }
                }
            }
            process.WaitForExit( );
            b安全退出 = process.ExitCode == 0;

            if (!string.IsNullOrEmpty(ffmpeg_Pace)) builder日志.AppendLine(ffmpeg_Pace);
            builder日志.AppendLine( );
            if (!b安全退出) {
                read_StandardOutput( );
                try { fi编码.Delete( ); } catch { }
                builder日志.Append(DateTime.Now).Append(" 异常退出，代码：").Append(process.ExitCode);
                try { File.WriteAllText($"{fi编码.DirectoryName}\\FFmpegAsync异常.{str成功文件名}@{DateTime.Now:yy-MM-dd HH.mm.ss}.errlog", builder日志.ToString( )); } catch { }

                Thread.Sleep(999); 转码队列.process移除结束(this);//发生异常停顿一秒再继续下一个。

            } else {
                转码队列.process移除结束(this);
                builder日志.AppendFormat("{0:yyyy-MM-dd HH:mm:ss} 均速{1:F4}fps 耗时 {2} ({3})秒", DateTime.Now, getFPS( ), stopwatch.Elapsed, stopwatch.ElapsedMilliseconds / 1000);
                if (File.Exists(fi编码.FullName)) {
                    if (!di编码成功.Exists) try { di编码成功.Create( ); } catch { return; }

                    if (HasFrame(out _)) {
                        try { File.WriteAllText($"{di编码成功.FullName}\\{str成功文件名}_转码完成.log", builder日志.ToString( )); } catch { }
                    } else {//分割点有误、编码器不输出等情况，输出0帧，但是正常退出。
                        try { File.WriteAllText($"{di编码成功.FullName}\\{str成功文件名}_无帧转码完成.errlog", builder日志.ToString( )); } catch { }
                    }
                    string str转码完成文件 = $"{di编码成功.FullName}\\{str成功文件名}{fi编码.Extension}";
                    if (File.Exists(str转码完成文件)) try { File.Delete(str转码完成文件); } catch { }
                    try { fi编码.MoveTo(str转码完成文件); } catch { return; }

                    if (!b无缓转码) {
                        if (Settings.b编码后删除切片) {
                            try { fi源.Delete( ); } catch { }
                        } else {
                            string str已编码源 = (fi源.DirectoryName.EndsWith("协同编码") ? fi源.Directory.Parent.FullName : fi源.Directory.FullName) + "\\已编码源";
                            try {
                                Directory.CreateDirectory(str已编码源);
                                fi源.MoveTo(str已编码源 + "\\" + fi源.Name);
                            } catch {
                                try {
                                    fi源.MoveTo($"{str已编码源}\\{str成功文件名}{DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss.fff")}{fi源.Extension}");
                                } catch {
                                    try { fi源.MoveTo($"{fi源.DirectoryName}\\{str成功文件名}{DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss.fff")}{fi源.Extension}"); } catch {

                                    }
                                }
                            }
                        }
                    }
                    string str提取时间码命令行 = string.Format("timestamps_v2 {0} 0:{1}_timestamp.txt", fi编码.Name, str成功文件名);
                    subProcess(EXE.mkvextract, str提取时间码命令行, fi编码.DirectoryName, out string Output, out string Error);
                }

                Form破片压缩.autoReset刷新输出.Set( );
                Form破片压缩.autoReset合并.Set( );//转码后文件移动到成功文件夹，触发一次合并查询。
            }

            b已结束 = true;
            process.Dispose( );
        }

        public static bool get_ffprobe读取视频时长(string filePath, out double sec) {
            sec = 0;
            using (Process p = new Process( )) {
                p.StartInfo.FileName = EXE.ffprobe;
                p.StartInfo.Arguments = "-show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -v error " + filePath;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardError = false;
                p.StartInfo.RedirectStandardOutput = true;
                try { p.Start( ); } catch { return false; }
                if (double.TryParse(p.StandardOutput.ReadToEnd( ), out sec)) {
                    return true;
                }
                return false;
            }
        }
        public static bool subProcess(string FileName, string Arguments, string WorkingDirectory, out string Output, out string Error) {
            bool Success = false;
            using (Process p = new Process( )) {
                p.StartInfo.FileName = FileName;
                p.StartInfo.Arguments = Arguments;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WorkingDirectory = WorkingDirectory;
                p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                try { p.Start( ); } catch { Error = string.Empty; Output = string.Empty; return false; }

                Error = p.StandardError.ReadToEnd( );
                Output = p.StandardOutput.ReadToEnd( );

                p.WaitForExit( );
                Success = p.ExitCode == 0;
            }
            return Success;
        }
    }
}
