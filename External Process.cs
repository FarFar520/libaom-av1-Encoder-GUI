using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace 破片压缩器 {
    internal class External_Process {
        public int pid = int.MaxValue;
        public Process process;
        public List<string> listError = new List<string>( );
        List<string> listOutput = new List<string>( );

        public string StandardOutput = string.Empty, StandardError = string.Empty;
        public string get_ffmpeg_Pace => ffmpeg_Pace;

        string ffmpeg_Pace = string.Empty;
        string ffmpeg_Encoding = string.Empty;
        bool newFrame = false;

        int index_frame = -1;
        public uint encodingFrames = 0;
        public double encFps = 0.0f;

        public Stopwatch stopwatch = new Stopwatch( );
        //public DateTime time编码开始 = DateTime.Now, time出帧 = DateTime.Now;
        public TimeSpan span输入时长 = TimeSpan.Zero;//, span耗时 = TimeSpan.Zero;

        public FileInfo fi源, fi编码;

        public DirectoryInfo di编码成功, di输出文件夹;

        public bool b已结束 = false, b安全退出 = false, b编码后删除切片 = false, b补齐时间戳 = false, b单线程 = true;

        public StringBuilder sb输出数据流 = new StringBuilder( );//全局变量有调用，直接初始化。

        Regex regex时长 = new Regex(@"Duration:\s*((?:\d{2}:){2,}\d{2}(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);//视频时长
        Regex regex帧时长 = new Regex(@"time=\s*((?:\d{2}:){2,}\d{2}(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Regex regexFrame = new Regex(@"frame=\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex regexSize = new Regex(@"size=\s*(\d+(?:\.\d+)?)KiB", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex regexBitrate = new Regex(@"bitrate=\s*(\d+(?:\.\d+)?)kbits/s", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex regexTime = new Regex(@"time=\s*(\d{2}:\d{2}:\d{2}(?:\.\d{2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        /// <summary>
        /// 调用外部可执行程序类初始化，对同文件夹读写
        /// </summary>
        /// <param name="exe文件">可执行程序，也支持系统环境变量配置的程序名</param>
        /// <param name="str命令行">拼装完整的命令行</param>
        /// <param name="fi输入文件">供文件归档函数使用的输入文件</param>
        /// <returns>工作目录 = 输入&输出路径，命令行中可使用【输入文件名】</returns>
        public External_Process(string exe文件, string str命令行, FileInfo fi输入文件) {
            this.fi源 = fi输入文件;
            di输出文件夹 = fi输入文件.Directory;

            listOutput.Add(DateTime.Now.ToString( ));
            listOutput.Add($"{exe文件} {str命令行}");

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
            //process.StartInfo.encoding
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
            this.fi源 = fi输入文件;
            di输出文件夹 = new DirectoryInfo(str输出文件夹);

            listOutput.Add(DateTime.Now.ToString( ));
            listOutput.Add($"{exe文件} {str命令行}");

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
            }
        }

        public bool HasFrame(out uint f) {
            f = encodingFrames;
            string sf = regexFrame.Match(ffmpeg_Pace).Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(sf) && uint.TryParse(sf, out f)) {
                encodingFrames = f; return true;
            } else return f > 0;
        }
        public double getFPS( ) {
            if (newFrame) {
                newFrame = false;
                //time出帧 = DateTime.Now;
                int i = index_frame;
                int iframes = 0;
                //double sec = time出帧.Subtract(time编码开始).TotalSeconds;
                double sec = stopwatch.ElapsedMilliseconds / 1000;
                if (sec < 0) sec = 1;
                for (; i < ffmpeg_Pace.Length; i++) { //开头可能有空格，先找到数字开头。
                    char c = ffmpeg_Pace[i];
                    if (ffmpeg_Pace[i] > '0' && ffmpeg_Pace[i] <= '9') {
                        iframes = ffmpeg_Pace[i] - 48;
                        break;
                    }
                }
                for (i++; i < ffmpeg_Pace.Length; i++) {
                    if (ffmpeg_Pace[i] >= '0' && ffmpeg_Pace[i] <= '9') {
                        iframes = iframes * 10 + ffmpeg_Pace[i] - 48;
                    } else
                        break;//非数字结尾退出
                }
                if (iframes > 0) {
                    encodingFrames = (uint)iframes;
                    encFps = iframes / sec;
                } else if (regexFrame.IsMatch(ffmpeg_Encoding)) {
                    encodingFrames = uint.Parse(regexFrame.Match(ffmpeg_Encoding).Groups[1].Value);
                    encFps = encodingFrames / sec;
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


        public bool Get_StanderOutput(out List<string> StanderOutput) {
            StanderOutput = new List<string>( );
            try {
                process.Start( );
                while (!process.StandardOutput.EndOfStream) {
                    string line = process.StandardOutput.ReadLine( );
                    StanderOutput.Add(line);
                    sb输出数据流.AppendLine(line);
                }
            } catch { return false; }
            return StanderOutput.Count > 0;
        }

        public bool Get_StandardError(out List<string> StandardError) {
            StandardError = new List<string>( );
            try {
                process.Start( );
                while (!process.StandardError.EndOfStream)
                    StandardError.Add(process.StandardError.ReadLine( ).Trim( ));
            } catch { return false; }
            return StandardError.Count > 0;
        }
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

        public bool async(out Thread thread_GetStandardError, out Thread thread_GetStandardOutput) {
            thread_GetStandardError = new Thread(read_StandardError);
            thread_GetStandardOutput = new Thread(read_StandardOutput);

            try { process.Start( ); } catch { return false; }

            thread_GetStandardError.IsBackground = true;
            thread_GetStandardError.Start( );

            thread_GetStandardOutput.IsBackground = true;
            thread_GetStandardOutput.Start( );

            return !process.HasExited;
        }//开线程读取，不会阻塞外部程序，返回线程，判断线程状态，代表完整读取输出信息。

        public bool async_FFmpeg编码( ) {
            bool run = false;
            try {
                process.Start( );
                pid = process.Id;
                process.PriorityClass = ProcessPriorityClass.Idle;
                run = true;
            } catch (Exception err) { listError.Add(err.Message); }

            if (run) {
                Thread thread_GetStandardError = new Thread(ffmpeg_读编码消息直到结束);
                thread_GetStandardError.IsBackground = true;
                thread_GetStandardError.Start( );
                return true;//返回真代表加入等待队列。

            } else {
                try { File.WriteAllText($"{fi源.DirectoryName}\\FFmpegAsync异常.{fi源.Name}.log", listError[0]); } catch { }
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
                        ffmpeg_Pace = StandardError;
                        newFrame = true;
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
            while (!process.StandardOutput.EndOfStream) {
                string line = process.StandardOutput.ReadLine( ).TrimStart( );
                if (!string.IsNullOrEmpty(line) && !line.StartsWith("Progress")) {
                    builder.AppendLine(line);
                    if (line.StartsWith("Error")) {
                        listError.Add(line);
                    } else
                        listOutput.Add(line);
                }
            }

            if (!process.StandardError.EndOfStream) {
                string error = process.StandardError.ReadToEnd( );
                builder.AppendLine("有错误发生").AppendLine(process.StandardOutput.ReadToEnd( ));
            }
            process.WaitForExit( );

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
            }
        }

        void ffmpeg_读编码消息直到结束( ) {//一条子线程          
            //time编码开始 = DateTime.Now;
            stopwatch.Start( );
            while (!process.StandardError.EndOfStream) {
                StandardError = process.StandardError.ReadLine( );
                if (!string.IsNullOrEmpty(StandardError)) {
                    if (regexFrame.IsMatch(StandardError)) {
                        break;
                    } else {
                        listError.Add(StandardError);
                        if (span输入时长 == TimeSpan.Zero && regex时长.IsMatch(StandardError)) {
                            TimeSpan.TryParse(regex时长.Match(StandardError).Groups[1].Value, out span输入时长);
                        }
                    }
                }
            }
            if (!process.HasExited) {
                //while (!process.StandardError.EndOfStream) {
                //    StandardError = process.StandardError.ReadLine( ).TrimStart( );
                //    if (!string.IsNullOrEmpty(StandardError)) {
                //        if (StandardError.StartsWith("frame=")) {
                //            ffmpeg_Pace = StandardError;
                //            newFrame = true;
                //            break;
                //        } else if (StandardError.StartsWith("size=")) {
                //            continue;
                //        } else {
                //            listError.Add(StandardError);
                //        }
                //    }
                //}
                while (!process.StandardError.EndOfStream) {
                    ffmpeg_Encoding = process.StandardError.ReadLine( );
                    int iframe = StandardError.IndexOf("frame=") + 6;
                    if (iframe >= 6) {
                        index_frame = iframe;
                        ffmpeg_Pace = ffmpeg_Encoding;
                        newFrame = true;
                    } else
                        listError.Add(ffmpeg_Encoding);
                }
                process.WaitForExit( );
            }

            Fx文件处理( );
        }

        void read_StandardOutput( ) {
            while (!process.StandardOutput.EndOfStream) {

                StandardOutput = process.StandardOutput.ReadLine( );
                listOutput.Add(StandardOutput);
                sb输出数据流.AppendLine(StandardOutput);

            }
        }
        void read_StandardError( ) {
            while (!process.StandardError.EndOfStream) {
                StandardError = process.StandardError.ReadLine( );
                listError.Add(StandardError);
            }
        }

        void Fx文件处理( ) {
            //span耗时 = DateTime.Now.Subtract(time编码开始);
            b安全退出 = process.ExitCode == 0;
            转码队列.process主动移除结束(this);

            StringBuilder builder = new StringBuilder( );
            for (int i = 0; i < listOutput.Count; i++) builder.AppendLine(listOutput[i]);
            builder.AppendLine( );
            for (int i = 0; i < listError.Count; i++) builder.AppendLine(listError[i]);
            builder.AppendLine( );
            if (!string.IsNullOrEmpty(ffmpeg_Pace)) builder.AppendLine(ffmpeg_Pace);
            builder.AppendLine( );

            if (!b安全退出) {
                read_StandardOutput( );
                try { fi编码.Delete( ); } catch { }
                builder.Append(DateTime.Now).Append(" 异常退出，代码：").Append(process.ExitCode);
                try { File.WriteAllText($"{fi源.DirectoryName}\\FFmpegAsync异常.{fi源.Name}@{DateTime.Now:yy-MM-dd HH.mm.ss}.log", builder.ToString( )); } catch { }
            } else {
                Fx补齐时间码( );
                //builder.AppendFormat("{0:yyyy-MM-dd HH:mm:ss} 均速{1:F4}fps 耗时 {2} ({3:F0})秒", DateTime.Now, getFPS( ), span耗时, span耗时.TotalSeconds);
                builder.AppendFormat("{0:yyyy-MM-dd HH:mm:ss} 均速{1:F4}fps 耗时 {2} ({3})秒", DateTime.Now, getFPS( ), stopwatch.Elapsed, stopwatch.ElapsedMilliseconds / 1000);
                if (File.Exists(fi编码.FullName)) {
                    string name = fi源.Name.Substring(0, fi源.Name.Length - fi源.Extension.Length);
                    if (!di编码成功.Exists) try { di编码成功.Create( ); } catch { return; }
                    string str转码完成文件 = $"{di编码成功.FullName}\\{name}{fi编码.Extension}";
                    if (File.Exists(str转码完成文件)) try { File.Delete(str转码完成文件); } catch { }
                    try { fi编码.MoveTo(str转码完成文件); } catch { return; }
                    try { File.WriteAllText($"{di编码成功.FullName}\\{name}_转码完成.log", builder.ToString( )); } catch { }
                    if (b编码后删除切片) try { fi源.Delete( ); } catch { }
                }

                Form破片压缩.autoReset合并.Set( );//转码后文件移动到成功文件夹，触发一次合并查询。
            }

            b已结束 = true;
            process.Dispose( );
        }

        void Fx补齐时间码( ) {
            if (b补齐时间戳 && TimeSpan.TryParse(regex帧时长.Match(ffmpeg_Pace).Groups[1].Value, out TimeSpan ts编码时长)) {
                if (span输入时长 > ts编码时长) {
                    string timeCodeFile = $"{fi编码.DirectoryName}\\{fi编码.Name}_timestamp.txt";
                    if (subProcess(EXE.mkvextract, $"timestamps_v2 {fi编码.Name} 0:{fi编码.Name}_timestamp.txt", fi编码.DirectoryName, out string Output, out string Error)) {
                        string[] tcLine = null;
                        try { tcLine = File.ReadAllLines(timeCodeFile); } catch { return; }
                        if (tcLine.Length > 2) {
                            string endSeconds = span输入时长.TotalMilliseconds.ToString( );
                            string outName = fi编码.Name.Substring(0, fi编码.Name.LastIndexOf('.'));
                            for (int len = tcLine.Length - 1; len > 0; len--) {
                                StringBuilder sbTC = new StringBuilder( );
                                for (int i = 0; i < len; i++) {
                                    sbTC.AppendLine(tcLine[i]);
                                }
                                sbTC.AppendLine(endSeconds);
                                try { File.WriteAllText(timeCodeFile, sbTC.ToString( )); } catch {
                                    try { File.Delete(timeCodeFile); } catch { }
                                    return;
                                }
                                string timestampMKV = $"{outName}-timestamp{len}.mkv";
                                if (subProcess(EXE.mkvmerge, $"--output \"{timestampMKV}\" --timestamps \"0:{timeCodeFile}\" \"{fi编码.Name}\"", fi编码.DirectoryName, "Progress: 100%")) {
                                    if (subProcess(EXE.ffprobe, $"\"{timestampMKV}\"", fi编码.DirectoryName, out List<string> listError)) {
                                        foreach (string err in listError) {
                                            if (err.Length > 21 && err.StartsWith("Duration:")) {
                                                string span = err.Substring(9, err.IndexOf(',', 9) - 9);
                                                if (TimeSpan.TryParse(span, out ts编码时长)) {
                                                    if (span输入时长 <= ts编码时长) {
                                                        try { fi编码.Delete( ); } catch { }
                                                        fi编码 = new FileInfo($"{fi编码.DirectoryName}\\{timestampMKV}");

                                                        if (span输入时长 != ts编码时长) {
                                                            string str切片时长有变 = $"{fi源.DirectoryName}\\切片时长有变";
                                                            if (!Directory.Exists(str切片时长有变))
                                                                try { Directory.CreateDirectory(str切片时长有变); } catch { }
                                                            try { fi源.CopyTo($"{str切片时长有变}\\{fi源.Name}"); } catch { }
                                                            try { fi编码.CopyTo($"{str切片时长有变}\\{fi编码.Name}"); } catch { }
                                                            try {
                                                                FileInfo fiTC = new FileInfo(timeCodeFile);
                                                                fiTC.CopyTo($"{str切片时长有变}\\{fiTC.Name}");
                                                            } catch { }
                                                        }
                                                        try { File.Delete(timeCodeFile); } catch { }
                                                        return;
                                                    } else {
                                                        try { File.Delete(timestampMKV); } catch { }
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    } else {
                                        try { File.Delete(timestampMKV); } catch { }
                                    }

                                } else {
                                    try { File.Delete(timestampMKV); } catch { }
                                }
                                try { File.Delete(timeCodeFile); } catch { }
                            }//每次少一行。
                        }
                        try { File.Delete(timeCodeFile); } catch { }
                    }
                }
            }
        }

        bool subProcess(string FileName, string Arguments, string WorkingDirectory, string FinalTxt) {
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
                try { p.Start( ); } catch { }
                while (!p.StandardOutput.EndOfStream || !p.StandardError.EndOfStream) {
                    if (!p.StandardOutput.EndOfStream && p.StandardOutput.ReadLine( ).Contains(FinalTxt)) {
                        return true;
                    }

                    if (!p.StandardError.EndOfStream && p.StandardError.ReadLine( ).Contains(FinalTxt)) {
                        return true;
                    }
                }
            }
            return false;
        }

        bool subProcess(string FileName, string Arguments, string WorkingDirectory, out string Output, out string Error) {
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
                try { p.Start( ); } catch { }

                Error = p.StandardError.ReadToEnd( );
                Output = p.StandardOutput.ReadToEnd( );

                p.WaitForExit( );
                Success = p.ExitCode == 0;
            }
            return Success;
        }

        bool subProcess(string FileName, string Arguments, string WorkingDirectory, out List<string> listError) {
            bool Success = false;
            listError = new List<string>( );
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
                try { p.Start( ); } catch { }
                while (!p.StandardError.EndOfStream) {
                    string e = p.StandardError.ReadLine( ).TrimStart( );
                    if (!string.IsNullOrWhiteSpace(e)) {
                        listError.Add(e);
                    }
                }
                p.WaitForExit( );
                Success = p.ExitCode == 0;
            }

            return Success;
        }

    }
}
