using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static 破片压缩器.Subtitle;
using static 破片压缩器.VTimeBase;

namespace 破片压缩器 {
    internal class Video_Roadmap {

        public static Regex regex逗号 = new Regex(@"\s*[,，]+\s*");
        public static Regex regex秒长 = new Regex(@"\[FORMAT\]\s+duration=(\d+\.\d+)\s+\[/FORMAT\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static Regex regex切片号 = new Regex(@"'(\d+)\.mkv'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regexPTS_Time = new Regex(@"pts_time:(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        int fontsize = 19;

        string gop {
            get {
                if (_bGOP传参)
                    return $" -g {Math.Ceiling(info.f输出帧率 * Settings.sec_gop)}";
                else return string.Empty;
            }
        }
        string gop_max {
            get {
                if (_bGOP传参)
                    return $" -g " + info.outSumFrames;
                else return string.Empty;
            }
        }

        string get_分段模式文字滤镜(Span偏移 span偏移) {
            StringBuilder builder = new StringBuilder(lavfi全局值);

            if (_b切片序号水印) {
                builder.Replace("{切片序号水印}", $"drawtext=text='{info.str视频名无后缀} - {span偏移.i分段号}({span偏移.f转场}~{span偏移.f结束 - info.f输入每帧秒})'{str水印字体参数}:fontsize={fontsize}:fontcolor=white@0.618:x=(w-text_w):y=0");
            }

            if (_b硬字幕) {
                string lavfi硬字幕;
                if (aSS != null) {
                    FileInfo fi_分段字幕 = new FileInfo(string.Format("{0}\\{1}-{2}{3}", di分段字幕.FullName, span偏移.f关键帧, span偏移.f结束, aSS.Extension));
                    if (fi_分段字幕.Exists) {
                        lavfi硬字幕 = $"subtitles='{di分段字幕.Name}\\\\{fi_分段字幕.Name}'";
                    } else lavfi硬字幕 = string.Empty;
                } else if (sRT != null) {
                    FileInfo fi_分段字幕 = new FileInfo(string.Format("{0}\\{1}-{2}.srt", di分段字幕.FullName, span偏移.f关键帧, span偏移.f结束));
                    if (fi_分段字幕.Exists) {
                        lavfi硬字幕 = $"subtitles='{di分段字幕.Name}\\\\{fi_分段字幕.Name}{Settings.str文本硬字幕样式}'";
                    } else lavfi硬字幕 = string.Empty;
                } else lavfi硬字幕 = string.Empty;

                if (string.IsNullOrEmpty(lavfi硬字幕)) {
                    builder.Replace("{硬字幕},", "");
                    builder.Replace(",{硬字幕}", "");
                    builder.Replace("{硬字幕}", "");
                } else
                    builder.Replace("{硬字幕}", lavfi硬字幕);
            }

            if (builder.Length > 0) {
                builder.Insert(0, " -lavfi \"");
                builder.Append('"');
            }

            builder.Append(" -fps_mode ").Append(_b转可变帧率 ? "vfr" : "passthrough");

            return builder.ToString( );
        }

        string get_切片模式文字滤镜(string num) {
            StringBuilder builder = new StringBuilder(lavfi全局值);

            if (_b硬字幕) {
                string lavfi硬字幕;
                if (aSS != null) {
                    if (File.Exists(string.Format("{0}\\{1}{2}", di切片.FullName, num, aSS.Extension))) {
                        lavfi硬字幕 = $"subtitles={num}{aSS.Extension}";
                    } else lavfi硬字幕 = string.Empty;
                } else if (sRT != null) {
                    if (File.Exists(string.Format("{0}\\{1}.srt", di切片.FullName, num))) {
                        lavfi硬字幕 = $"subtitles='{num}.srt{Settings.str文本硬字幕样式}'";
                    } else lavfi硬字幕 = string.Empty;
                } else lavfi硬字幕 = string.Empty;

                if (string.IsNullOrEmpty(lavfi硬字幕)) {
                    builder.Replace("{硬字幕},", "");
                    builder.Replace(",{硬字幕}", "");
                    builder.Replace("{硬字幕}", "");
                } else
                    builder.Replace("{硬字幕}", lavfi硬字幕);
            }

            if (_b切片序号水印) {
                builder.Replace("{切片序号水印}", $"drawtext=text='{info.str视频名无后缀} - {num}'{str水印字体参数}:fontsize={fontsize}:fontcolor=white@0.618:x=(w-text_w):y=0");
            }


            if (builder.Length > 0) {
                builder.Insert(0, " -lavfi \"");
                builder.Append('"');
            }

            builder.Append(" -fps_mode ").Append(_b转可变帧率 ? "vfr" : "passthrough");

            return builder.ToString( );
        }

        string get_lavfi( ) {
            List<string> list = new List<string>( );

            //滤镜顺序1.自动反交错。二选一
            //bwdif=mode:parity:deint
            //mode0=send_frame将输入视频的每一帧直接作为输出帧（不进行场合并） mode1=send_field将每个输入帧拆分为独立的场（奇数行和偶数行分别输出）
            //parity‌ - 1=auto自动检测场序（默认值）
            //deint‌0 = all对所有帧进行去隔行处理（默认） deint1=interlaced仅对标记为隔行的帧进行处理
            if (info.b隔行扫描) list.Add("bwdif=1:-1:1");// bwdif=1:-1:1 按场反交错，上下场各生成一帧
            //if (info.b隔行扫描) list.Add("bwdif=0:-1:1");//bwdif=0:-1:1 按帧反交错，上下场组合为一帧。

            ///滤镜顺序2.
            if (info.b剪裁滤镜) list.Add(info.str剪裁滤镜);

            ///滤镜顺序3.
            if (!string.IsNullOrEmpty(str自定义滤镜值)) {
                string[] arr_lavfi = regex逗号.Split(str自定义滤镜值);
                if (arr_lavfi.Length > 0) {
                    if (!string.IsNullOrEmpty(arr_lavfi[0])) list.Add(arr_lavfi[0]);

                    for (int i = 1; i < arr_lavfi.Length; i++)
                        if (!string.IsNullOrEmpty(arr_lavfi[0])) list.Add(arr_lavfi[i]);
                }
            }
            //滤镜顺序4.硬字幕滤镜
            if (_b硬字幕) {
                list.Add("{硬字幕}");
            }
            //滤镜顺序5.缩放
            if (info.b缩放滤镜) list.Add(info.str缩放滤镜);

            //滤镜顺序6.去重复
            if (bVFR) list.Add("mpdecimate");//最好置于顺序末.去掉重复帧

            //滤镜顺序7.序号水印滤镜
            if (_b切片序号水印) list.Add("{切片序号水印}");

            list.Distinct( );

            StringBuilder builder = new StringBuilder( );
            if (list.Count > 0) {
                lavfi全局值 = list[0];

                if (list[0] != "{硬字幕}" && list[0] != "{切片序号水印}")
                    builder.Append("").Append(list[0]);

                for (int i = 1; i < list.Count; i++) {
                    lavfi全局值 += ',' + list[i];

                    if (list[i] != "{硬字幕}" && list[i] != "{切片序号水印}")
                        builder.Append(',').Append(list[i]);
                }
                if (builder.Length > 0) {
                    builder.Insert(0, " -lavfi \"");
                    builder.Append('"');
                }
            }

            builder.Append(" -fps_mode ").Append(_b转可变帧率 ? "vfr" : "passthrough");
            /*
            -vsync parameter (global)
-fps_mode[:stream_specifier] parameter (output,per-stream)
Set video sync method / framerate mode. vsync is applied to all output video streams but can be overridden for a stream by setting fps_mode. vsync is deprecated and will be removed in the future.

For compatibility reasons some of the values for vsync can be specified as numbers (shown in parentheses in the following table).

passthrough (0)
Each frame is passed with its timestamp from the demuxer to the muxer.

cfr (1)
Frames will be duplicated and dropped to achieve exactly the requested constant frame rate.

vfr (2)
Frames are passed through with their timestamp or dropped so as to prevent 2 frames from having the same timestamp.

auto (-1)
Chooses between cfr and vfr depending on muxer capabilities. This is the default method.
            */
            return builder.ToString( );
        }

        string get_encAudio( ) {
            StringBuilder builder = new StringBuilder( );
            if (info.list音频轨.Count > 0) {
                if (_b音轨同时切片) {
                    builder.Append(" -map 0:a?");
                    if (_b_opus) {
                        str音频摘要 = ".opus";
                        if (info.list音频轨.Count == 1 && Settings.i声道 > 0) {
                            if (Settings.i声道 == 2 && info.list信息流[info.list音频轨[0]].Contains("stereo")) {
                            } else
                                builder.Append(" -ac ").Append(Settings.i声道);

                            str音频摘要 += $"{Settings.i声道}.0";
                        }
                        builder.Append(" -c:a libopus -vbr on -compression_level 10").Append(info.OUT.ar);//-vbr 1~10
                        builder.Append(" -b:a ").Append(Settings.i音频码率).Append("k");

                        str音频摘要 += $".{Settings.i音频码率}k";
                    } else {
                        builder.Append(" -c:a copy");
                        str音频摘要 = info.get音轨code;
                        if (str音频摘要 != ".opus") str最终格式 = ".mkv";
                    }

                    if (info.list字幕轨.Count > 0)
                        builder.Append(" -map 0:s -c:s copy");

                } else {
                    str音频摘要 = info.get音轨code;//沿用整轨音频格式。
                    if (!(_b_opus || str音频摘要 == ".opus")) str最终格式 = ".mkv";
                    builder.Append(" -an -sn");//屏蔽切片中音轨、字轨。使用外部整轨。
                }
            } else {
                str音频摘要 = ".noAu";
            }
            return builder.ToString( );
        }

        object obj切片 = new object( );

        public VideoInfo info;
        public VTimeBase vTimeBase;
        public FileInfo fi输入视频;

        public string str输入路径, str切片路径, lower完整路径_输入视频;

        public static string ffmpeg = "ffmpeg", ffprobe = "ffprobe", mkvmerge = "mkvmerge", mkvextract = "mkvextract";

        public static string str软件标签 = $"-metadata encoding_tool=\"{Application.ProductName} {Application.ProductVersion}\"";

        public double sec视频时长 = 0;

        public DirectoryInfo di编码成功, di切片;

        List<float> list_typeI_pts_time = new List<float>( );

        bool bVFR = true;
        bool _b有切片记录 = false, _b音轨同时切片 = false, _b_opus = false, _bGOP传参, _b无缓转码 = false, _b硬字幕 = false, _b分段字幕 = false, _b切片字幕 = false, _b切片序号水印 = false, _b转可变帧率 = false, _b使用全局滤镜 = true;

        FileInfo fiMKA = null, fiOPUS = null, fi视频头信息 = null, fi拆分日志 = null, fi合并日志 = null;

        List<FileInfo> list_fi外挂字幕 = new List<FileInfo>( ), list_切片体积降序 = new List<FileInfo>( );

        string path无缓转码csv = string.Empty;
        string lib视频编码器, lavfi全局值, str自定义滤镜值, str滤镜lavfi, str编码摘要, str音频命令, str音频摘要;
        string str连接视频名, str转码后MKV名, strMKA名, str完整路径MKA, str水印字体路径, str水印字体参数;

        string str输出格式 = ".mkv", str最终格式 = ".webm";

        public string str输出文件名 => str转码后MKV名;
        public string strMKA文件名 => strMKA名;
        public string strMKA路径 => str完整路径MKA;

        public bool b有切片记录 => _b有切片记录;
        public bool b无缓转码 => _b无缓转码;

        Thread th音频转码;

        public FileSystemWatcher watcher编码成功文件夹 = null;
        public static bool b查找可执行文件(out string log, out string txt) {
            log = string.Empty;
            txt = string.Empty;

            bool has_ffmpeg = EXE.find最新版ffmpeg(out ffmpeg);
            bool has_ffprobe = EXE.find最新版ffprobe(out ffprobe);
            bool has_mkvmerge = EXE.find最新版mkvmerge(out mkvmerge);
            bool has_mkvextract = EXE.find最新版mkvextract(out mkvextract);

            if (!has_ffprobe || !has_ffmpeg) {
                if (!has_ffmpeg) log += "“ffmpeg.exe”、";
                if (!has_ffprobe) log += "“ffprobe.exe”、";
                txt += "\r\nffmpeg下载链接：\r\nhttps://www.gyan.dev/ffmpeg/builds/ffmpeg-git-full.7z";
            }

            if (!has_mkvmerge || !has_mkvextract) {
                if (!has_mkvmerge) log += "“mkvmerge.exe”、";
                if (!has_mkvextract) log += "“mkvextract.exe”、";

                txt += "\r\nmkvmerge下载链接：\r\nhttps://mkvtoolnix.download/windows/releases/95.0/mkvtoolnix-32-bit-95.0.7z";
            }

            if (log.Length > 0) {
                log = log.Substring(0, log.Length - 1);
                return false;
            } else
                return true;
        }

        public Video_Roadmap(FileInfo fileInfo, string str正在转码文件夹, bool b无缓转码) {

            External_Process.get_ffprobe读取视频时长(fileInfo.FullName, out sec视频时长);

            fi输入视频 = fileInfo;
            info = new VideoInfo(fileInfo, sec视频时长);

            Task.Run(( ) => fn测试单核解码帧率( ));
            _b_opus = Settings.opus;
            _b音轨同时切片 = Settings.b音轨同时切片转码;
            _bGOP传参 = Settings.lib已设置.GOP跃帧 == null && Settings.lib已设置.GOP跃帧 == null;

            _b无缓转码 = b无缓转码;
            _b转可变帧率 = Settings.b转可变帧率;

            if (Settings.b右上角文件名_切片序列号水印) {
                _b切片序号水印 = true; _b使用全局滤镜 = false;
            }
            str最终格式 = Settings.lib已设置.code == "av1" ? ".webm" : ".mkv";

            if (Settings.b自定义滤镜)
                str自定义滤镜值 = Settings.str自定义滤镜;

            info.OUT.b抽重复帧 = bVFR = _b转可变帧率;

            str输入路径 = fileInfo.Directory.FullName;
            lower完整路径_输入视频 = fileInfo.FullName.ToLower( );
            str切片路径 = $"{str正在转码文件夹.TrimEnd('\\')}\\切片_{fi输入视频.Name}";

            if (!转码队列.dic_切片路径_剩余.ContainsKey(str切片路径))
                转码队列.dic_切片路径_剩余.Add(str切片路径, 0);

            di切片 = new DirectoryInfo(str切片路径);
            path无缓转码csv = di切片.FullName + "\\无缓转码.csv";

            _b硬字幕 = Settings.b硬字幕;

            try { File.Delete(str切片路径 + "\\任务配置.ini"); } catch { }

            if (b无缓转码) {
                vTimeBase = new VTimeBase(info, di切片);
            }
            if (di切片.Exists) {
                if (!b无缓转码) {
                    string str切片记录 = $"{str切片路径}\\视频切片_{fi输入视频.Name}.log";
                    if (File.Exists(str切片记录)) {//有日志表示切片成功。
                        查找并按体积降序切片( );
                        if (list_切片体积降序.Count < 1) {
                            string[] arr_dir = Directory.GetDirectories(str切片路径, "转码完成*");
                            for (int i = 0; i < arr_dir.Length; i++) {
                                string[] arr_file = Directory.GetFiles(arr_dir[i]);
                                for (int j = 0; j < arr_file.Length; j++) {
                                    if (arr_file[j].EndsWith(".webm") || arr_file[j].EndsWith(".mkv")) {
                                        _b有切片记录 = true;
                                        return;
                                    }
                                }
                            }
                            try { File.Delete(str切片记录); } catch { }
                        } else
                            _b有切片记录 = true;
                    }
                }
            } else {
                try { Directory.CreateDirectory(str切片路径); } catch { return; }
                if (b无缓转码) try { File.WriteAllText(path无缓转码csv, "正在查找时间戳……"); } catch { }
            }

        }

        int i统计剩余切片 = 0;
        DateTime time上次查找剩余切片 = DateTime.Now.AddDays(-1);
        public int i剩余切片数量 {
            get {
                if (_b无缓转码) return vTimeBase.i剩余分段;

                if (DateTime.Now.Subtract(time上次查找剩余切片).TotalSeconds < 10) //限制10秒内只查找一次
                    return i统计剩余切片;

                i统计剩余切片 = 0;
                if (Directory.Exists(di切片.FullName)) {
                    FileInfo[] arrFI_MKV视频 = di切片.GetFiles("*.mkv");
                    for (int i = 0; i < arrFI_MKV视频.Length; i++) {
                        string num = arrFI_MKV视频[i].Name.Substring(0, arrFI_MKV视频[i].Name.Length - 4);
                        if (uint.TryParse(num, out uint value)) {
                            i统计剩余切片++;
                        }
                    }
                }
                //只统计存机工作目录下缓存。
                //DirectoryInfo[] di协编们 = di切片.GetDirectories("*协同编码");// 协同编码的子工作目录
                //foreach (DirectoryInfo di协编 in di协编们) {
                //    FileInfo[] arrFI_MKV协转 = di协编.GetFiles("*.mkv");
                //    for (int i = 0; i < arrFI_MKV协转.Length; i++) {
                //        if (uint.TryParse(arrFI_MKV协转[i].Name.Substring(0, arrFI_MKV协转[i].Name.Length - 4), out uint value)) {
                //            i统计剩余切片++;
                //        }
                //    }
                //}
                time上次查找剩余切片 = DateTime.Now;
                return i统计剩余切片;
            }
        }

        public bool is无缓视频未完成 {
            get {
                if (File.Exists(path无缓转码csv)) {// && Directory.Exists(di编码成功.FullName)
                    if (vTimeBase.is扫描完成 && vTimeBase.i剩余分段 <= 0) {
                        FileInfo[] arrFI_MKV视频 = di编码成功.GetFiles("*.mkv");
                        if (arrFI_MKV视频.Length >= vTimeBase.i总分段) {
                            int count = 0;
                            for (int i = 0; i < arrFI_MKV视频.Length; i++) {
                                string num = arrFI_MKV视频[i].Name.Substring(0, arrFI_MKV视频[i].Name.Length - 4);
                                if (int.TryParse(num, out _)) count++;
                            }
                            if (count >= vTimeBase.i总分段) return false;
                        }
                    }
                }
                return true;
            }
        }

        public bool b文件夹下还有切片 {
            get {
                if (_b_opus && !_b音轨同时切片 && info.list音频轨.Count > 0) {
                    if (fiOPUS != null && !File.Exists(fiOPUS.FullName))
                        return true;
                }

                if (_b无缓转码) {
                    if (vTimeBase.is扫描完成 && vTimeBase.i剩余分段 <= 0) {
                        foreach (int i in vTimeBase.dic_分段_偏移.Keys) {
                            if (!File.Exists(di编码成功.FullName + "\\" + i + ".mkv")) return true;
                        }
                        return false;
                    }
                    return true;
                } else {
                    if (Directory.Exists(di切片.FullName)) {
                        FileInfo[] arrFI_MKV视频 = di切片.GetFiles("*.mkv");
                        for (int i = 0; i < arrFI_MKV视频.Length; i++) {
                            string num = arrFI_MKV视频[i].Name.Substring(0, arrFI_MKV视频[i].Name.Length - 4);
                            if (uint.TryParse(num, out _)) return true;//做最简单判断，是序列号名。mkv就返回还有。
                        }

                        DirectoryInfo[] di协编们 = di切片.GetDirectories("*协同编码");// 协同编码的子工作目录
                        foreach (DirectoryInfo di协编 in di协编们) {
                            FileInfo[] arrFI_MKV协转 = di协编.GetFiles("*.mkv");
                            for (int i = 0; i < arrFI_MKV协转.Length; i++) {
                                if (uint.TryParse(arrFI_MKV协转[i].Name.Substring(0, arrFI_MKV协转[i].Name.Length - 4), out _)) {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
        }

        public bool b音轨需要更新 {
            get {
                if (info.list音频轨.Count == 0) return false;
                if (Settings.opus && !_b音轨同时切片 && str音频摘要 != Settings.opus摘要) {//同时切片的模式不更新设置，音轨同时切片转码后帧有变化。
                    string path = $"{di切片.FullName}\\转码音轨{Settings.opus摘要}\\opus.mka";
                    if (File.Exists(path)) {
                        fiOPUS = new FileInfo(path);
                        str音频摘要 = Settings.opus摘要;
                        str最终格式 = info.OUT.str视流格式 == "av1" ? ".webm" : ".mkv";
                    } else {
                        return true;
                    }
                }
                return false;
            }
        }

        //工作流程设计：
        //1.读取输入文件信息→1.1信息处理
        //2.查找到有切片[进入4.]→
        //3.→切片→
        //4.转码视频→
        //5.转码音频（默认不转音频）→
        //6.合成→
        //7.移动合成后视频到切片父目录→
        //8.源文件处理，删除或者移动源文件到【源文件】。

        public void fn测试单核解码帧率( ) {
            string str = $"-hide_banner -threads 1 -filter_threads 1 -filter_complex_threads 1 -benchmark -i \"{fi输入视频.Name}\" -an -sn -to 10 -f null NUL";
            External_Process ep = new External_Process(ffmpeg, str, fi输入视频);
            if (ep.async_FFmpeg(out Process pFFmpeg)) {
                while (!pFFmpeg.HasExited) {
                    if (ep.encFps > 0) {
                        info.IN.f单核解码能力 = (float)ep.encFps;
                    }
                    try { Thread.Sleep(99); } catch { }
                }
            }
        }

        public bool b解码60帧判断交错(out StringBuilder builder) {
            string str视频头信息文件路径 = $"{str切片路径}\\{fi输入视频.Name}.info";

            if (File.Exists(str视频头信息文件路径)) {
                string[] lines;
                try { lines = File.ReadAllLines(str视频头信息文件路径); } catch { lines = null; }
                if (lines != null) {
                    builder = new StringBuilder( );
                    info.v以帧判断隔行扫描(lines);
                    for (int i = 0; i < lines.Length; i++) {
                        info.fx信息分类(lines[i]);
                        builder.AppendLine(lines[i]);
                    }
                    if (info.list视频轨.Count > 0) {
                        fi视频头信息 = new FileInfo(str视频头信息文件路径);
                        return true;
                    }
                }
            }
            int scan_frame = 60;
            builder = new StringBuilder($"扫描{scan_frame}帧判断隔行扫描：");

            if (!info.b隔行扫描) {//视频头信息中有概率识别隔行扫描。
                string commamd = $"-hide_banner -i \"{fi输入视频.FullName}\" -select_streams v -read_intervals \"%+#{scan_frame}\" -show_entries \"frame=interlaced_frame\"";
                builder.AppendLine( ).AppendLine(commamd);
                if (new External_Process(ffprobe, commamd, str切片路径, fi输入视频).sync(out List<string> listOutput, out List<string> listError)) {
                    for (int i = 0; i < listError.Count; i++) info.fx信息分类(listError[i]);
                    info.v以帧判断隔行扫描(scan_frame, listOutput);

                    for (int i = 0; i < listError.Count; i++) builder.AppendLine(listError[i]);

                    builder.AppendLine( );
                    for (int i = 2; i < listOutput.Count; i++) builder.AppendLine(listOutput[i]);

                    try { File.WriteAllText(str视频头信息文件路径, builder.ToString( )); } catch { }
                    fi视频头信息 = new FileInfo(str视频头信息文件路径);
                    return true;
                }
            }
            //info.手动剪裁;
            return false;
        }
        public bool b读取视频头(out StringBuilder builder) {
            builder = new StringBuilder("读取视频头信息：");
            string str视频头信息文件 = $"{fi输入视频.Name}.info";
            string str视频头信息文件路径 = $"{str切片路径}\\{str视频头信息文件}";

            string commamd = $"-i \"{fi输入视频.FullName}\"";
            builder.AppendLine( ).AppendLine(commamd);
            if (new External_Process(ffprobe, commamd, str切片路径, fi输入视频).sync_FFProbeInfo保存消息(str视频头信息文件, out string[] logs, ref builder)) {
                for (int i = 0; i < logs.Length; i++) info.fx信息分类(logs[i]);
                fi视频头信息 = new FileInfo(str视频头信息文件路径);
            }
            if (info.list视频轨.Count > 0) {
                return true;
            }
            return false;
        }
        public bool b检测场景切换(decimal f检测阈值, ref StringBuilder builder) {//1080p视频时，大约跑满12线程。可以跑双进程。
            if (b读取场景切片数据($"{str输入路径}\\检测镜头({f检测阈值:F3}).{fi输入视频.Name}.log")) return true;
            //另一个镜头检测工具生成的日志，不区分关键帧，使用算法计算切片时间戳

            string str检测镜头文件名 = $"检测镜头({f检测阈值:F3}).{fi输入视频.Name}.info";//本程序筛选转场帧信息，以关键帧来对比

            string str检测镜头完整路径_1 = $"{str输入路径}\\{str检测镜头文件名}";//优先查找筛选后的数，提高轻微效率
            if (b读取场景大于分割GOP切片数据(str检测镜头完整路径_1)) return true;
            //b读取场景切片数据

            string str检测镜头完整路径_2 = $"{str切片路径}\\{str检测镜头文件名}";
            if (b读取场景大于分割GOP切片数据(str检测镜头完整路径_2)) return true;

            string str检测镜头完整路径_3 = $"{str输入路径}\\检测镜头({f检测阈值:F3}).{fi输入视频.Name}.txt";
            if (b读取场景切片数据(str检测镜头完整路径_3)) return true;//源目录下数据，视作外部程序生成日志。

            string str检测镜头完整路径_4 = $"{str切片路径}\\检测镜头({f检测阈值:F3}).{fi输入视频.Name}.txt";
            if (b读取场景切片数据_全部(str检测镜头完整路径_4)) return true;//读取工作目录数据，认作本本地工具生成，完全切片。

            //无法读取外部则开扫
            string commamd = $"-hide_banner -loglevel info -i \"{fi输入视频.FullName}\" -an -sn -vf \"select='eq(pict_type,I)',select='gt(scene,{f检测阈值:F3})',showinfo\" -f null -";//快速分割方案，以关键帧差异去判断切割点位。
            //string commamd = $"-loglevel info -i \"{fi输入视频.FullName}\" -an -sn -vf \"select='gt(scene,{f检测阈值})',showinfo\" -f null -";//固定帧率硬件压制资源切割点几乎不在关键帧上。

            if (!转码队列.b允许入队) commamd = EXE.ffmpeg单线程 + " " + commamd;//CPU资源被占满后，扫描以单线程运行减少损耗。

            builder.AppendLine( ).AppendLine("检测关键帧场景切换：").AppendLine(commamd);

            StringBuilder builder检测镜头 = new StringBuilder("检测关键帧场景切换：").AppendLine( ).AppendLine(commamd);

            转码队列.process场景 = new External_Process(ffmpeg, commamd, str切片路径, fi输入视频);
            bool b扫描 = 转码队列.process场景.sync_FFmpegInfo保存消息(str检测镜头文件名, out string[] logs, ref builder检测镜头);
            转码队列.process场景 = null;

            if (b扫描) {
                List<string> list筛选后的数据 = new List<string>( );
                StringBuilder builder筛选后数据 = new StringBuilder( );
                builder筛选后数据.AppendLine("检测关键帧场景切换：").AppendLine(commamd).AppendLine( );
                int i = 0;
                for (; i < logs.Length; i++) {
                    if (logs[i].StartsWith("Duration:", StringComparison.OrdinalIgnoreCase)) {
                        builder筛选后数据.AppendLine(logs[i]);
                        list筛选后的数据.Add(logs[i]);
                        break;
                    }
                }
                for (; i < logs.Length; i++) {
                    if (logs[i].StartsWith("[Parsed_showinfo") && logs[i].Contains("pts_time:")) {
                        builder筛选后数据.AppendLine(logs[i]);
                        list筛选后的数据.Add(logs[i]);
                    }
                }
                builder.AppendLine( ).Append(builder筛选后数据);

                string str筛选后数据 = builder筛选后数据.ToString( );
                string str汇流数据 = builder检测镜头.ToString( );
                try { File.WriteAllText(str检测镜头完整路径_1, str筛选后数据); } catch { }
                try { File.WriteAllText(str检测镜头完整路径_2, str筛选后数据); } catch { }

                try { File.WriteAllText(str检测镜头完整路径_3, str汇流数据); } catch { }
                try { File.WriteAllText(str检测镜头完整路径_4, str汇流数据); } catch { }//检测场景需要解码一遍视频，（硬件解码分析效果不理想，只能软解码），较为耗时，多保存几个副本，防止误删。

                Scene scene = new Scene( );
                scene.Add_TypeI(list筛选后的数据.ToArray( ));
                list_typeI_pts_time = scene.Get_List_TypeI_pts_time( );

                if (list_typeI_pts_time.Count < 3) {
                    return b解析场景大于分割GOP切片数据(list筛选后的数据.ToArray( ));
                } else
                    return list_typeI_pts_time.Count > 0;//至少能切2段;

            }
            return false;
        }

        bool _黑边未扫描 = true;
        public bool b扫描视频黑边生成剪裁参数( ) {
            if (_黑边未扫描) {
                StringBuilder builder = new StringBuilder( );
                Dictionary<string, int> crops = new Dictionary<string, int>( );
                uint count_Crops = 0;
                if (info.time视频时长.TotalSeconds < 33) {
                    fx扫描黑边(0, (int)info.time视频时长.TotalSeconds, ref count_Crops, ref crops, ref builder);
                } else {
                    float step = (float)(info.time视频时长.TotalSeconds / 11);
                    float endSec = (float)(info.time视频时长.TotalSeconds - step);
                    for (float ss = step; ss < endSec; ss += step) {
                        if (Settings.b自动裁黑边) fx扫描黑边(ss, 3, ref count_Crops, ref crops, ref builder);
                        else return false;//中途改变设置的话，立刻跳出。
                    }
                }
                info.fx匹配剪裁(crops, count_Crops);
            }
            _黑边未扫描 = false;
            return false;
        }

        public void Fx按场景切片并获取列表(ref StringBuilder builder) {
            if (list_typeI_pts_time.Count < 1) return;
            StringBuilder builder切片命令行 = new StringBuilder("--output %d.mkv --stop-after-video-ends --no-track-tags --no-global-tags --no-attachments");

            if (Settings.b音轨同时切片转码) _b音轨同时切片 = true;//音轨同时切片设计为自定义删减片段，保留字幕和章节
            else builder切片命令行.Append(" --no-audio --no-subtitles --no-chapters");//此命令行有顺序，不可放到输入文件后。

            builder切片命令行.AppendFormat(" \"{0}\" --split timestamps:{1}s", fi输入视频.FullName, list_typeI_pts_time[0]);

            for (int i = 1; i < list_typeI_pts_time.Count; i++)
                builder切片命令行.AppendFormat(",{0}s", list_typeI_pts_time[i]);
            //builder切片命令行.AppendFormat(" --title \"{0}\"", fi输入视频.Name);

            _b有切片记录 = b视频切片(ref builder切片命令行);
            builder.AppendLine( ).AppendLine("按场景切片：").Append(builder切片命令行);
        }
        public void Fx按时间切片并获取列表(int i切片间隔秒, ref StringBuilder builder) {
            StringBuilder builder切片命令行 = new StringBuilder("--output %d.mkv --stop-after-video-ends --no-track-tags --no-global-tags --no-attachments");

            if (Settings.b音轨同时切片转码) _b音轨同时切片 = true;//音轨同时切片设计为自定义删减片段，保留字幕和章节
            else builder切片命令行.Append(" --no-audio --no-subtitles --no-chapters");//此命令行有顺序，不可放到输入文件后。

            builder切片命令行.AppendFormat(" \"{0}\" --split duration:{1}s", fi输入视频.FullName, i切片间隔秒);
            //builder切片命令行.AppendFormat(" --title \"{0}\"", fi输入视频.Name);

            _b有切片记录 = b视频切片(ref builder切片命令行);
            builder.AppendLine( ).Append("按间隔").Append(i切片间隔秒).Append("秒切片：").AppendLine( ).Append(builder切片命令行);
        }

        ASS aSS;
        SRT sRT;
        DirectoryInfo di分段字幕;
        public void fx查找可渲染同名硬字幕文件( ) {
            string path_ASS = fi输入视频.Directory.FullName + '\\' + info.str视频名无后缀 + ".ass";
            if (File.Exists(path_ASS)) {
                aSS = new ASS(new FileInfo(path_ASS));
            } else {
                string path_SSA = fi输入视频.Directory.FullName + '\\' + info.str视频名无后缀 + ".ssa";
                if (File.Exists(path_SSA)) {
                } else {
                    string path_SRT = fi输入视频.Directory.FullName + '\\' + info.str视频名无后缀 + ".srt";
                    if (File.Exists(path_SRT)) {
                        sRT = new SRT(new FileInfo(path_SRT));
                    }
                }
            }

            if (aSS != null || sRT != null) {
                di分段字幕 = new DirectoryInfo(fi输入视频.Directory.FullName + "\\" + info.str视频名无后缀);
                if (!di分段字幕.Exists) {
                    try {
                        di分段字幕.Create( );
                        _b使用全局滤镜 = false;
                        _b分段字幕 = true;
                        if (vTimeBase != null) vTimeBase.setSubtitle(aSS, sRT, di分段字幕);
                    } catch {
                        di分段字幕 = null;
                        return;
                    }
                    vTimeBase.fx分割字幕( );//支持异步分割功能，边扫边分
                } else {
                    _b使用全局滤镜 = false;
                    _b分段字幕 = true;
                }
            } else {
                _b硬字幕 = false;//找不到同名字幕文件：标记无需硬字幕
            }
        }

        public bool b查找MKA音轨( ) {
            strMKA名 = $"{fi输入视频.Name.Substring(0, fi输入视频.Name.LastIndexOf('.'))}.mka";
            str完整路径MKA = $"{str切片路径}\\{strMKA名}";
            string str日志文件 = $"提取音轨_{fi输入视频.Name}.log";

            if (File.Exists(str完整路径MKA)) {
                fiMKA = new FileInfo(str完整路径MKA);
                fi拆分日志 = new FileInfo($"{str切片路径}\\{str日志文件}");
                return true;
            }
            return false;
        }
        public bool b提取MKA音轨(ref StringBuilder builder) {
            if (!File.Exists(fi输入视频.FullName)) return false; //存在间隔时间，判断原始文件存在情况。

            string str日志文件 = $"提取音轨_{fi输入视频.Name}.log";
            string commamd = $"--output \"{strMKA名}\" --no-global-tags --no-video \"{fi输入视频.FullName}\" --disable-track-statistics-tags";

            builder.AppendLine( ).AppendLine("提取MKA音轨：").Append(commamd);
            builder.AppendLine(commamd);

            转码队列.process切片 = new External_Process(mkvmerge, commamd, str切片路径, fi输入视频);
            转码队列.process切片.sync_MKVmerge保存消息(str切片路径, str日志文件, out string[] logs, ref builder);
            转码队列.process切片 = null;

            if (File.Exists(str完整路径MKA)) {
                fiMKA = new FileInfo(str完整路径MKA);
                fi拆分日志 = new FileInfo($"{str切片路径}\\{str日志文件}");
                return true;
            }
            return fiMKA != null;
        }//提取音轨流程可放到转码前中后任意时刻
        LibEnc.命令行 v命令行;
        public bool b拼接转码摘要( ) {
            if (info.list视频轨.Count < 1) return false;

            info.fx输出宽高( );
            for (int i = 0; i < 30; i++) { //可等待3秒，获得真实解码能力。
                try { Thread.Sleep(111); } catch { }
                if (info.IN.f单核解码能力 < float.MaxValue) break;//解码能力通过测试获得。
            }
            lib视频编码器 = Settings.Get_视频编码库(info, out v命令行);

            str滤镜lavfi = get_lavfi( );//全片通用滤镜
            str音频命令 = get_encAudio( );

            v命令行.str编码指令 = gop + lib视频编码器 + str音频命令;
            v命令行.str多线程编码指令 = gop + v命令行.str多线程编码库 + str音频命令;

            v命令行.box_CRF编码指令 = gop + v命令行.box_CRF视参数 + str音频命令;
            v命令行.box_CRF多线程编码指令 = gop + v命令行.box_CRF多线程编码库 + str音频命令;

            v命令行.str编码指令_极压 = gop_max + v命令行.str最低画质编码库 + str音频命令;
            v命令行.str多线程编码指令_极压 = gop_max + v命令行.str多线程最低画质编码库 + str音频命令;

            string vfr = _b转可变帧率 ? ".vfr" : string.Empty;

            str编码摘要 = $"{info.str长乘宽}{vfr}.{info.OUT.enc}.{info.OUT.str量化名}{info.OUT.adjust_crf}.p{info.OUT.preset}{info.OUT.denoise}";
            str连接视频名 = $"{info.str视频名无后缀}.{info.get输出Progressive}{vfr}.{info.OUT.enc}.crf{info.OUT.adjust_crf}.p{info.OUT.preset}{info.OUT.denoise}";
            if (_b硬字幕 && (_b分段字幕 || _b切片字幕)) str连接视频名 += ".硬字幕";

            di编码成功 = new DirectoryInfo($"{str切片路径}\\转码完成.{str编码摘要}");

            if (!_b无缓转码) {
                if (!di编码成功.Exists) try { di编码成功.Create( ); } catch { }

                if (Directory.Exists(di编码成功.FullName)) {
                    watcher编码成功文件夹 = new FileSystemWatcher(di编码成功.FullName.ToLower( ), "*.mkv") {
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName
                    };
                }
            }

            //try { di编码成功文件夹.Create( ); } catch { }\\目录等任意切片转码完成后再创建。

            int font_size = info.i输出宽 / 100;
            if (font_size > 19) fontsize = font_size;//1920/100=19
            else fontsize = 19;

            if (_b切片序号水印) {
                if (_b无缓转码) {
                    str水印字体参数 = ":font='Microsoft YaHei'";//有效
                    fontsize -= 2;//微软雅黑比常见字体大1~2号
                } else {
                    string path字体复制到 = _b无缓转码 ? fi输入视频.Directory.FullName : str切片路径;
                    if (File.Exists(path字体复制到 + "\\drawtext.otf")) {
                        str水印字体参数 = ":fontfile=drawtext.otf";
                        str水印字体路径 = path字体复制到 + "\\drawtext.otf";
                    } else if (File.Exists(path字体复制到 + "\\drawtext.ttf")) {
                        str水印字体参数 = ":fontfile=drawtext.ttf";
                        str水印字体路径 = path字体复制到 + "\\drawtext.ttf";
                    } else {
                        if (File.Exists("水印.otf")) {
                            try {
                                File.Copy("水印.otf", path字体复制到 + "\\drawtext.otf");
                                str水印字体路径 = path字体复制到 + "\\drawtext.otf";
                                str水印字体参数 = ":fontfile=drawtext.otf";
                                return true;
                            } catch { }
                        } else if (File.Exists("水印.ttf")) {
                            try {
                                File.Copy("水印.ttf", path字体复制到 + "\\drawtext.ttf");
                                str水印字体路径 = path字体复制到 + "\\drawtext.ttf";
                                str水印字体参数 = ":fontfile=drawtext.ttf";
                                return true;
                            } catch { }
                        }
                        str水印字体参数 = ":font='Microsoft YaHei'";//有效
                        fontsize -= 2;//微软雅黑比常见字体大1~2号
                    }
                }
            }
            //str水印字体参数 = ":font='Microsoft YaHei'";//有效
            //str水印字体参数 = ":font='微软雅黑'";//有效
            return true;
        }
        public void fx保存任务配置( ) {
            StringBuilder sb编码配置 = new StringBuilder( );
            sb编码配置.Append("str编码摘要=").AppendLine(str编码摘要);
            sb编码配置.Append("str输出格式=").AppendLine(str输出格式);
            sb编码配置.Append("str滤镜lavfi=").AppendLine(str滤镜lavfi);
            sb编码配置.Append("lavfi全局值=").AppendLine(lavfi全局值);
            sb编码配置.Append("_b转可变帧率=").Append(_b转可变帧率).AppendLine( );

            sb编码配置.Append("info.str视频名无后缀=").AppendLine(info.str视频名无后缀);
            sb编码配置.Append("info.i输出宽=").AppendLine(info.i输出宽.ToString( ));

            sb编码配置.Append("str编码指令=").AppendLine(v命令行.str编码指令);
            sb编码配置.Append("str多线程编码指令=").AppendLine(v命令行.str多线程编码指令);

            //sb编码配置.Append("Settings.b单线程=").Append(Settings.b单线程).AppendLine( ); //SVT-AV1、VVenC 多线程不降低画质，允许灵活调整算力节点线程。
            sb编码配置.Append("di编码成功文件夹.Name=").AppendLine($"转码完成.{str编码摘要}");
            sb编码配置.Append("").AppendLine( );

            try { File.WriteAllText(str切片路径 + "\\任务配置.ini", sb编码配置.ToString( )); } catch { }
        }
        public void fx清理存编终止切片( ) {
            if (Directory.Exists(di切片.FullName)) {
                DirectoryInfo[] arr_转码完成 = di切片.GetDirectories("转码完成*");
                for (int i = 0; i < arr_转码完成.Length; i++) {
                    if (arr_转码完成[i].Name != di编码成功.Name && arr_转码完成[i].GetFiles( ).Length == 0)
                        try { arr_转码完成[i].Delete( ); } catch { }
                }

                FileInfo[] arrFI_MKV视频 = di切片.GetFiles("*.mkv");
                DateTime now = DateTime.Now;
                string tag = "丨" + now.Year;//转码关键字匹配
                for (int i = 0; i < arrFI_MKV视频.Length; i++) {
                    char name_1 = arrFI_MKV视频[i].Name[0];
                    if (name_1 > 48 && name_1 < 58 //如果是数字，不包含0开头
                        && arrFI_MKV视频[i].Name.Contains(tag)
                        && now.Subtract(arrFI_MKV视频[i].LastWriteTime).TotalDays > 3)//删除三天前
                       {
                        try { arrFI_MKV视频[i].Delete( ); } catch { }
                    }
                }
            }
        }

        public bool b转码下一个分段(out External_Process external_Process) {
            if (vTimeBase.hasNext_序列Span偏移(di编码成功, out Span偏移 span偏移, out int i剩余, out bool b全黑场)) {
                转码队列.dic_切片路径_剩余[str切片路径] = i剩余;
                span偏移.fx计算帧量(info.f输入帧率, info.f输出帧率);

                string path编码后切片 = $"{di切片.FullName}\\{span偏移.i分段号}_{str编码摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}{str输出格式}";

                string str命令行;
                string dec_1th = info.IN.ffmpeg单线程解码;
                string input = span偏移.get二次跳转_SS_i_SS_TO(fi输入视频);

                string lavfi滤镜 = _b使用全局滤镜 ? str滤镜lavfi : get_分段模式文字滤镜(span偏移);



                if (Settings.b多线程) {
                    string encV = v命令行.format_CRF多线程编码库(b全黑场, span偏移.f指定画质CRF);
                    str命令行 = $"{dec_1th}{input}{lavfi滤镜}{encV} {str软件标签} \"{path编码后切片}\"{EXE.ffmpeg不显库}";
                } else {
                    string encV = v命令行.format_CRF编码指令(b全黑场, span偏移.f指定画质CRF);
                    str命令行 = $"{EXE.ffmpeg单线程}{input}{lavfi滤镜}{encV} {str软件标签} \"{path编码后切片}\"{EXE.ffmpeg不显库}";
                }
                external_Process = new External_Process(span偏移, ffmpeg, str命令行, !Settings.b多线程, fi输入视频, di切片, di编码成功);

                external_Process.fi编码 = new FileInfo(path编码后切片);//fi切片设计为局域网编码时移动到另外文件夹，防止多机处理相同切片
                external_Process.b单线程 = !Settings.b多线程;

                return true;
            }

            external_Process = null;
            return false;
        }
        public bool b转码下一个切片(out External_Process external_Process) {
            FileInfo fi切片 = null;
            lock (obj切片) {
                while (list_切片体积降序.Count > 0) {
                    if (File.Exists(list_切片体积降序[0].FullName)) {
                        fi切片 = list_切片体积降序[0];
                        list_切片体积降序.RemoveAt(0);
                        break;
                    } else {
                        list_切片体积降序.RemoveAt(0);
                    }
                }
            }
            转码队列.dic_切片路径_剩余[str切片路径] = list_切片体积降序.Count;

            if (fi切片 != null) {//音频和视频同时编码方案，允许删除不需要片段。 视频分片+音轨单编，就不能缺失片。
                string name = fi切片.Name.Substring(0, fi切片.Name.Length - 4);

                string str滤镜 = _b使用全局滤镜 ? str滤镜lavfi : get_切片模式文字滤镜(name);

                string str编码后切片 = $"{name}_{str编码摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}{str输出格式}";

                string str命令行;

                if (Settings.b多线程) {
                    //str命令行 = $"-hide_banner -i {fi切片.Name}{str滤镜}{str多线程编码指令} \"{str编码后切片}\"";
                    str命令行 = $"{info.IN.ffmpeg单线程解码}-i {fi切片.Name}{str滤镜}{v命令行.str多线程编码指令} {str软件标签} \"{str编码后切片}\"{EXE.ffmpeg不显库}";
                    //单线程解码超4K有些跟不上编码速度。
                } else {
                    str命令行 = $"{EXE.ffmpeg单线程}-i {fi切片.Name}{str滤镜}{v命令行.str编码指令} {str软件标签} \"{str编码后切片}\"{EXE.ffmpeg不显库}";
                }

                external_Process = new External_Process(ffmpeg, str命令行, !Settings.b多线程, name, fi切片, di编码成功);
                external_Process.fi编码 = new FileInfo($"{fi切片.DirectoryName}\\{str编码后切片}");//fi切片设计为局域网编码时移动到另外文件夹，防止多机处理相同切片
                return true;
            } else
                external_Process = null;

            return false;
        }

        public bool b协同切片尝试回调( ) {
            DirectoryInfo[] di协编们 = di切片.GetDirectories("*协同编码");// 协同编码的子工作目录
            foreach (DirectoryInfo di协编 in di协编们) {
                FileInfo[] arrFI_MKV协转 = di协编.GetFiles("*.mkv");

                DateTime now = DateTime.Now;
                string tag = "丨" + now.Year;//转码关键字匹配

                for (int i = 0; i < arrFI_MKV协转.Length; i++) {
                    if (uint.TryParse(arrFI_MKV协转[i].Name.Substring(0, arrFI_MKV协转[i].Name.Length - 4), out uint value)) {
                        try {//尝试移动协同编码文件到切片目录。计算节点灵活下线
                            arrFI_MKV协转[i].MoveTo(di切片.FullName + "\\" + arrFI_MKV协转[i].Name);
                        } catch { continue; }
                        lock (obj切片) { list_切片体积降序.Insert(0, arrFI_MKV协转[i]); }
                    } else {
                        char name_1 = arrFI_MKV协转[i].Name[0];
                        if (name_1 > 48 && name_1 < 58 //如果是数字，不包含0开头
                            && arrFI_MKV协转[i].Name.Contains(tag)
                            && now.Subtract(arrFI_MKV协转[i].LastWriteTime).TotalDays > 3)//删除三天前
                        {
                            try { arrFI_MKV协转[i].Delete( ); } catch { }//尝试删除编码节点已下线的碎片任务半成品
                        }
                    }
                }
                try { di协编.Delete( ); } catch { }
            }
            return list_切片体积降序.Count > 0;
        }
        public bool b后台转码MKA音轨( ) {//在切片环节启动的转码音轨，用于视频转码完成，等待音频转码完成合并。
            if (info.list音频轨.Count > 0 && _b_opus && !_b音轨同时切片) {//转码opus时，可以不分解mka文件
                if (fiMKA != null && File.Exists(fiMKA.FullName)) {
                    str音频摘要 = ".opus";
                    StringBuilder builder = new StringBuilder(EXE.ffmpeg单线程);

                    builder.Append(" -i \"").Append(fiMKA.FullName).Append('"');
                    builder.Append(" -vn -map 0:a -c:a libopus -vbr on -compression_level 10").Append(info.OUT.ar);//忽略视频轨道，转码全部音轨，字幕轨可能会保留一条。
                    //builder.Append(" -map 0:a:0 -c:a libopus -vbr on -compression_level 10"); //只保留一条音轨
                    //builder.Append(" -c:a libopus -vbr 2.0 -compression_level 10");//vbr 0~2;//vbr不太好用
                    if (info.list字幕轨.Count > 0) builder.Append(" -map 0:s -c:s copy");
                    if (Settings.i音频码率 == 96 && Settings.i声道 == 0) {
                        //opus默认码率每声道48K码率。多声道自动计算方便。
                    } else
                        builder.Append(" -b:a ").Append(Settings.i音频码率).Append('k');

                    if (Settings.i声道 > 0) {
                        if (info.IN.f最大声道 > Settings.i声道)
                            builder.Append(" -ac ").Append(Settings.i声道);
                        str音频摘要 += $"{Settings.i声道}.0";
                    }

                    str音频摘要 += $".{Settings.i音频码率}k";

                    fiOPUS = new FileInfo($"{fiMKA.DirectoryName}\\转码音轨{str音频摘要}\\opus.mka");
                    if (!fiOPUS.Exists) {
                        fiOPUS = new FileInfo($"{di切片}\\临时{str音频摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.mka");
                        builder.AppendFormat(" -metadata encoding_tool=\"{0} {1}\" \"{2}\" ", Application.ProductName, Application.ProductVersion, fiOPUS.FullName);

                        External_Process external_Process = new External_Process(ffmpeg, builder.ToString( ), fiMKA);
                        external_Process.fi编码 = fiOPUS;
                        external_Process.di编码成功 = new DirectoryInfo($"{di切片.FullName}\\转码音轨{str音频摘要}");

                        th音频转码 = new Thread(new ParameterizedThreadStart(fn_音频转码成功信号)) { IsBackground = true };
                        th音频转码.Start(external_Process); //避免音频比视频后出结果，额外开一条等待线程，完成时触发b音轨转码成功布尔值。
                        return true;
                    } else {
                        Form破片压缩.autoReset合并.Set( );
                    }
                } else if (File.Exists(fi输入视频.FullName)) {
                    str音频摘要 = ".opus";
                    StringBuilder builder = new StringBuilder(EXE.ffmpeg单线程);

                    builder.Append(" -i \"").Append(fi输入视频.FullName).Append('"');
                    builder.Append(" -vn -map 0:a -c:a libopus -vbr on -compression_level 10").Append(info.OUT.ar);//忽略视频轨道，转码全部音轨，字幕轨可能会保留一条。
                    //builder.Append(" -map 0:a:0 -c:a libopus -vbr on -compression_level 10"); //只保留一条音轨
                    //builder.Append(" -c:a libopus -vbr 2.0 -compression_level 10");//vbr 0~2;//vbr不太好用
                    if (info.list字幕轨.Count > 0) builder.Append(" -map 0:s -c:s copy");
                    if (Settings.i音频码率 == 96 && Settings.i声道 == 0) {
                        //opus默认码率每声道48K码率。多声道自动计算方便。
                    } else
                        builder.Append(" -b:a ").Append(Settings.i音频码率).Append('k');

                    if (Settings.i声道 > 0) {
                        if (info.IN.f最大声道 > Settings.i声道)
                            builder.Append(" -ac ").Append(Settings.i声道);
                        str音频摘要 += $"{Settings.i声道}.0";
                    }

                    str音频摘要 += $".{Settings.i音频码率}k";

                    fiOPUS = new FileInfo($"{di切片.FullName}\\转码音轨{str音频摘要}\\opus.mka");
                    if (!fiOPUS.Exists) {
                        fiOPUS = new FileInfo($"{di切片}\\临时{str音频摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.mka");
                        builder.AppendFormat(" -metadata encoding_tool=\"{0} {1}\" \"{2}\" ", Application.ProductName, Application.ProductVersion, fiOPUS.FullName);

                        External_Process external_Process = new External_Process(ffmpeg, builder.ToString( ), fi输入视频);
                        external_Process.fi编码 = fiOPUS;
                        external_Process.di编码成功 = new DirectoryInfo($"{di切片.FullName}\\转码音轨{str音频摘要}");

                        th音频转码 = new Thread(new ParameterizedThreadStart(fn_音频转码成功信号)) { IsBackground = true };
                        th音频转码.Start(external_Process); //避免音频比视频后出结果，额外开一条等待线程，完成时触发b音轨转码成功布尔值。
                        return true;
                    } else {
                        Form破片压缩.autoReset合并.Set( );
                    }
                }
            }
            return false;
        }
        public bool b更新OPUS音轨( ) {//在合并环节启动，视频没完成之前，音频码率可以热修改。
            if (_b音轨同时切片 || th音频转码 != null && th音频转码.IsAlive) return false;

            if (fiMKA == null || !File.Exists(fiMKA.FullName)) {//任务穿插进视频转码全过程，可能出现音轨被删除、视频被删除的情况。
                if (File.Exists(fi输入视频.FullName))
                    fiMKA = fi输入视频;
                else {
                    info.list音频轨.Clear( );
                    str音频摘要 = ".noAu";
                    return false;
                }
            }

            StringBuilder builder = new StringBuilder( );
            if (!转码队列.b允许入队) builder.Append(EXE.ffmpeg单线程);
            builder.Append(" -i \"").Append(fiMKA.FullName).Append('"');
            builder.Append(" -vn -map 0:a -c:a libopus -vbr on -compression_level 10").Append(info.OUT.ar);//-vn不处理视频， -map 0:a 转码全部音轨
            if (info.list字幕轨.Count > 0) builder.Append(" -map 0:s -c:s copy");

            if (Settings.i音频码率 == 96 && Settings.i声道 == 0) {
                //opus默认码率每声道48K码率。多声道自动计算方便。
            } else {
                builder.Append(" -b:a ").Append(Settings.i音频码率).Append('k');
            }
            if (Settings.i声道 > 0) {
                if (info.IN.f最大声道 > Settings.i声道)
                    builder.Append(" -ac ").Append(Settings.i声道);
            }
            str音频摘要 = Settings.opus摘要;
            string str临时文件 = $"{di切片.FullName}\\临时{str音频摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.mka";//绝对目录输出音轨
            builder.AppendFormat(" -metadata encoding_tool=\"{0} {1}\" \"{2}\" ", Application.ProductName, Application.ProductVersion, str临时文件);

            External_Process ep = new External_Process(ffmpeg, builder.ToString( ), fiMKA);
            转码队列.process音轨 = ep;
            ep.sync_FFmpegInfo(out List<string> list);//音轨转码线程不占用队列。会超出cpu核心数。
            转码队列.process音轨 = null;

            FileInfo fi临时音轨 = new FileInfo(str临时文件);
            if (ep.b安全退出 && fi临时音轨.Exists) {
                FileInfo fi成功音轨 = new FileInfo($"{di切片.FullName}\\转码音轨{Settings.opus摘要}\\opus.mka");
                try { fi成功音轨.Directory.Create( ); } catch { return false; }
                try { fi临时音轨.MoveTo(fi成功音轨.FullName); } catch { return false; }
                fiOPUS = fi成功音轨;
                str最终格式 = info.OUT.str视流格式 == "av1" ? ".webm" : ".mkv";
                return File.Exists(fiOPUS.FullName);
            } else
                return false;
        }

        public bool b转码后混流( ) {//混流线程中有加入是否还有剩余切片判断。
            bool bSuccess = true;
            if (di编码成功 != null && Directory.Exists(di编码成功.FullName)) {//处理正常流程合并任务
                if (b连接序列切片(di编码成功.FullName, out FileInfo fi连接后视频)) {
                    new External_Process(ffprobe, $"-i \"{fi连接后视频.Name}\"", fi连接后视频).sync_FFmpegInfo(out List<string> arr);
                    bool b有音轨 = false;
                    for (int i = 0; i < arr.Count; i++) {
                        if (arr[i].StartsWith("Stream #") && arr[i].Contains("Audio")) {
                            if (VideoInfo.regexAudio.IsMatch(arr[i]))
                                str音频摘要 = '.' + VideoInfo.regexAudio.Match(arr[i]).Groups[1].Value;

                            b有音轨 = true; break;
                        }
                    }
                    if (b有音轨) {
                        bSuccess = b移动带音轨切片合并视频(fi连接后视频);
                    } else
                        bSuccess = b封装视频音频(fi连接后视频);
                } else
                    bSuccess = false;
            } else {//处理断点续合并任务
                string[] arrDir = Directory.GetDirectories(str切片路径);
                for (int i = 0; i < arrDir.Length; i++) {
                    int start = arrDir[i].LastIndexOf("\\转码完成.") + 6;
                    if (start > 10) {
                        str编码摘要 = arrDir[i].Substring(start);
                        str连接视频名 = $"{fi输入视频.Name}.{str编码摘要}";

                        if (b连接序列切片(arrDir[i], out FileInfo fi连接后视频)) {
                            new External_Process(ffprobe, $"-i \"{fi连接后视频.Name}\"", fi连接后视频).sync_FFmpegInfo(out List<string> arr);

                            bool b有音轨 = false;
                            for (int j = 0; j < arr.Count; j++) {
                                if (arr[j].StartsWith("Stream #") && arr[j].Contains("Audio")) {
                                    if (VideoInfo.regexAudio.IsMatch(arr[j]))
                                        str音频摘要 = '.' + VideoInfo.regexAudio.Match(arr[j]).Groups[1].Value;
                                    b有音轨 = true; break;
                                }
                                ;
                            }
                            if (b有音轨) {
                                bSuccess |= b移动带音轨切片合并视频(fi连接后视频);
                            } else {
                                bSuccess |= b封装视频音频(fi连接后视频);
                            }
                        } else
                            bSuccess = false;
                    }
                }
            }

            if (!string.IsNullOrEmpty(str水印字体路径)) {
                try { File.Delete(str水印字体路径); } catch { }
            }

            if (bSuccess) {
                if (Settings.b转码成功后删除源视频) {
                    try { fi输入视频.Delete( ); } catch { }
                    if (fi输入视频.Directory.GetFiles("*.*", SearchOption.AllDirectories).Length == 0) {
                        try { fi输入视频.Directory.Delete( ); } catch { }
                    }
                } else {
                    string path源视频 = $"{str输入路径}\\源视频";
                    //string path源视频 = bSuccess ? $"{str输入路径}\\源视频" : $"{str输入路径}\\合并失败";

                    if (!Directory.Exists(path源视频)) {
                        try { Directory.CreateDirectory(path源视频); } catch { }
                    }
                    try { fi输入视频.MoveTo($"{path源视频}\\{fi输入视频.Name}"); } catch {
                        try { fi输入视频.MoveTo($"{path源视频}\\{DateTime.Now:yy-MM-dd HH.mm.ss.fff} {fi输入视频.Name}"); } catch { }
                    }//正是运行再打开
                }
            }

            return bSuccess;
        }

        public void fx删除协编文件( ) {
            if (Directory.Exists(di切片.FullName)) {
                DirectoryInfo[] arrDI子文件夹 = di切片.GetDirectories("*协同编码");// 协同编码的子工作目录
                for (int i = 0; i < arrDI子文件夹.Length; i++) {
                    try { File.Delete(arrDI子文件夹[i] + "\\drawtext.otf"); } catch { }
                    try { File.Delete(arrDI子文件夹[i] + "\\drawtext.ttf"); } catch { }
                    try { arrDI子文件夹[i].Delete(true); } catch { }//尝试删除协同编码目录
                }
            }
            try { File.Delete(str切片路径 + "\\任务配置.ini"); } catch { }
        }

        void fx查找封装字幕文件( ) {
            //可增加语言字幕识别 _cn .En 等
            string path_ASS = fi输入视频.Directory.FullName + '\\' + info.str视频名无后缀 + ".ass";
            if (File.Exists(path_ASS)) {
                list_fi外挂字幕.Add(new FileInfo(path_ASS));
            }
            string path_SSA = fi输入视频.Directory.FullName + '\\' + info.str视频名无后缀 + ".ssa";
            if (File.Exists(path_SSA)) {
                list_fi外挂字幕.Add(new FileInfo(path_SSA));
            }
            string path_SRT = fi输入视频.Directory.FullName + '\\' + info.str视频名无后缀 + ".srt";
            if (File.Exists(path_SRT)) {
                list_fi外挂字幕.Add(new FileInfo(path_SRT));
            }
        }
        void fn_音频转码成功信号(object obj) {
            External_Process external_Process = (External_Process)obj;
            StringBuilder builder = new StringBuilder( );
            转码队列.process音轨 = external_Process;

            if (external_Process.sync_FFmpegInfo(out _)) {//音轨转码线程不占用队列。会超出cpu核心数。
                转码队列.process音轨 = null;
                FileInfo fi临时音轨 = external_Process.fi编码;
                if (fi临时音轨.Exists) {
                    FileInfo fi成功音轨 = new FileInfo($"{external_Process.di编码成功}\\opus.mka");
                    try { fi成功音轨.Directory.Create( ); } catch { return; }
                    try { fi临时音轨.MoveTo(fi成功音轨.FullName); } catch { return; }
                    fiOPUS = fi成功音轨;
                    str最终格式 = info.OUT.str视流格式 == "av1" ? ".webm" : ".mkv";
                }
            } else
                转码队列.process音轨 = null;

            if (!b文件夹下还有切片) Form破片压缩.autoReset合并.Set( );//有时音频转码速度比视频转码慢。
        }

        void fx扫描黑边(float ss, float t, ref uint count_Crop, ref Dictionary<string, int> dicCropdetect, ref StringBuilder builder) {
            string commamd = $"-hide_banner -ss {ss} -i \"{fi输入视频.Name}\" -t {t} -vf cropdetect=round=2 -f null -an /dev/null";

            bool b单线程 = !转码队列.b允许入队;
            if (b单线程) commamd = EXE.ffmpeg单线程 + " " + commamd;//CPU资源被占满后，以单线程运行减少损耗。

            转码队列.process黑边 = new External_Process(ffmpeg, commamd, fi输入视频);
            bool b扫描 = 转码队列.process黑边.sync_FFmpegInfo(out List<string> list);

            if (b单线程 && 转码队列.process黑边.HasFrame(out _) && 转码队列.process黑边.encFps > info.IN.f单核解码能力) {
                info.IN.f单核解码能力 = (float)转码队列.process黑边.encFps;
            }

            转码队列.process黑边 = null;

            if (b扫描) {
                for (int i = 0; i < list.Count; i++) {
                    if (list[i].StartsWith("[Parsed_cropdetect")) {
                        count_Crop++;
                        int starIndex = list[i].LastIndexOf("crop=");
                        if (starIndex > 18) {
                            string crop = list[i].Substring(starIndex, list[i].Length - starIndex);
                            if (dicCropdetect.ContainsKey(crop))
                                dicCropdetect[crop]++;
                            else
                                dicCropdetect.Add(crop, 1);
                        }
                        builder.AppendLine(list[i]);
                    }
                }
            }
        }

        bool b读取场景切片数据(string path) {
            FileInfo fi = new FileInfo(path);
            if (fi.Exists && fi.Length < 31457290) {//读取30MB以内文件。
                string[] arr;
                try { arr = File.ReadAllLines(path); } catch { return false; }
                Scene scene = new Scene( );
                scene.Add_TypeI(arr);
                list_typeI_pts_time.Clear( );
                list_typeI_pts_time = scene.Get_List_TypeI_pts_time( );

                /*
                for (int i = 0; i < arr.Length; i++) {
                    if (arr[i].StartsWith("[Parsed_showinfo") && arr[i].Contains("type:I")) {
                        string pts = regexPTS_Time.Match(arr[i]).Groups[1].Value;//外部读取使用正则提升鲁棒性
                        if (!string.IsNullOrEmpty(pts)) list_typeI_pts_time.Add(float.Parse(pts));
                    }
                }
                */

                return list_typeI_pts_time.Count > 0;//至少能切2段;

            }
            return false;
        }

        bool b读取场景切片数据_全部(string path) {
            FileInfo fi = new FileInfo(path);
            if (fi.Exists && fi.Length < 31457290) {//读取30MB以内文件。) {
                string[] arr;
                try { arr = File.ReadAllLines(path); } catch { return false; }
                list_typeI_pts_time.Clear( );
                for (int i = 0; i < arr.Length; i++) {
                    if (arr[i].StartsWith("[Parsed_showinfo") && arr[i].Contains("type:I")) {
                        string pts = regexPTS_Time.Match(arr[i]).Groups[1].Value;//外部读取使用正则提升鲁棒性
                        if (!string.IsNullOrEmpty(pts)) list_typeI_pts_time.Add(float.Parse(pts));
                    }
                }
                return list_typeI_pts_time.Count > 0;//至少能切2段;
            }
            return false;
        }

        bool b读取场景大于分割GOP切片数据(string path) {
            FileInfo fi = new FileInfo(path);
            if (fi.Exists && fi.Length < 31457290) {//读取30MB以内文件。
                string[] arr;
                try { arr = File.ReadAllLines(path); } catch { return false; }
                return b解析场景大于分割GOP切片数据(arr);
            }
            return false;
        }

        bool b解析场景大于分割GOP切片数据(string[] arr) {
            list_typeI_pts_time.Clear( );
            float last_pts_time = 0.0f;//记录上一段时刻，初始时刻为片头
            for (int i = 0; i < arr.Length; i++) {
                if (arr[i].StartsWith("[Parsed_showinfo") && arr[i].Contains("type:I")) {
                    int i_pts_time_start = arr[i].IndexOf("pts_time:") + 9;
                    if (i_pts_time_start > 0) {
                        int i_pts_time_end = i_pts_time_start + 3;//结果应该包含3位数字
                        for (; i_pts_time_end < arr[i].Length; i_pts_time_end++) {
                            if (arr[i][i_pts_time_end] == ' ') { break; }
                        }
                        if (float.TryParse(arr[i].Substring(i_pts_time_start, i_pts_time_end - i_pts_time_start), out float pts_time)) {
                            if (pts_time - last_pts_time > Settings.i分割GOP) {//大于GOP长度
                                list_typeI_pts_time.Add(pts_time);
                                last_pts_time = pts_time;
                            }
                        }
                    }
                }
            }
            return list_typeI_pts_time.Count > 3;//至少能切3段，片头、片中、片尾。
        }

        void fx删除数字名称视频切片( ) {//切片失败的情况，先清空
            string[] arrFile = Directory.GetFiles(str切片路径, "*.mkv");
            for (int i = 0; i < arrFile.Length; i++) {
                FileInfo fi = new FileInfo(arrFile[i]);
                string name = fi.Name.Substring(0, fi.Name.LastIndexOf('.'));
                if (int.TryParse(name, out int num)) {
                    if (num.ToString( ) == name)
                        try { fi.Delete( ); } catch { }
                }
            }
        }

        bool b视频切片(ref StringBuilder builder切片命令行与输出结果) {//当机械硬盘为存储盘，SSD为缓存盘时，指定输出到SSD上，提升随机读写优势。
            string str日志文件名 = $"视频切片_{fi输入视频.Name}.log";
            fx删除数字名称视频切片( );
            builder切片命令行与输出结果.Append(" --disable-track-statistics-tags");
            External_Process ep = new External_Process(mkvmerge, builder切片命令行与输出结果.ToString( ), str切片路径, fi输入视频);

            转码队列.process切片 = ep;//可设计为多进程队列，切片受限于磁盘读写影响，最多3进程可填满IO带宽。
            bool b完成切片 = ep.sync_MKVmerge保存消息(str切片路径, str日志文件名, out string[] logs, ref builder切片命令行与输出结果);
            转码队列.process切片 = null;

            if (b完成切片) {
                查找并按体积降序切片( );
                字幕切片(logs);//不判断硬字幕设置，切片完成后，片段时间连续，尝试直接割字幕。

                if (list_切片体积降序.Count > 0) {
                    return true;
                } else return false;
            } else { //切片失败的情况，删除切片。
                fx删除数字名称视频切片( );
            }
            return false;
        }

        void 查找并按体积降序切片( ) {
            string[] arr_MKV视频 = Directory.GetFiles(str切片路径, "*.mkv");
            int i = 0;
            lock (obj切片) { list_切片体积降序.Clear( ); }

            for (; i < arr_MKV视频.Length; i++) {
                FileInfo fi = new FileInfo(arr_MKV视频[i]);
                if (int.TryParse(fi.Name.Substring(0, fi.Name.Length - 4), out int num)) {
                    lock (obj切片) { list_切片体积降序.Add(fi); }
                    break;
                }
            }
            for (++i; i < arr_MKV视频.Length; i++) {
                FileInfo fi = new FileInfo(arr_MKV视频[i]);
                if (int.TryParse(fi.Name.Substring(0, fi.Name.Length - 4), out int num)) {
                    for (int j = 0; j < list_切片体积降序.Count; j++) {
                        if (fi.Length > list_切片体积降序[j].Length) {
                            lock (obj切片) { list_切片体积降序.Insert(j, fi); }
                            goto 下一片;
                        }
                    }
                    lock (obj切片) { list_切片体积降序.Add(fi); }
                    下一片:;
                }
            }
        }
        void 字幕切片(string[] arrLog) {
            Dictionary<int, float> dic_name_sec = new Dictionary<int, float>( ) { { 1, 0 } };
            int last = arrLog.Length - 1;
            for (int i = 0; i < last; i++) {
                if (arrLog[i].StartsWith("Timestamp used in split decision:", StringComparison.OrdinalIgnoreCase)) {
                    Match matchTime = regex日时分秒.Match(arrLog[i].Substring(33));
                    if (matchTime.Success) {
                        if (float.TryParse(matchTime.Groups["Sec"].Value, out float sec)) {
                            if (int.TryParse(matchTime.Groups["Day"].Value, out int day)) sec += day + 86400;
                            if (int.TryParse(matchTime.Groups["Hour"].Value, out int hour)) sec += hour * 3600;
                            if (int.TryParse(matchTime.Groups["Min"].Value, out int min)) sec += min * 60;
                            if (float.TryParse("0." + matchTime.Groups["MS"].Value, out float sec_ms)) sec += sec_ms;//ASS毫秒单位保留两位数字，整除100 
                            for (++i; i < arrLog.Length; i++) {
                                if (arrLog[i].StartsWith("The file", StringComparison.OrdinalIgnoreCase)) {
                                    if (int.TryParse(regex切片号.Match(arrLog[i]).Groups[1].Value, out int n)) {
                                        dic_name_sec.Add(n, sec);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            last = dic_name_sec.Count;
            dic_name_sec.Add(dic_name_sec.Last( ).Key + 1, (float)sec视频时长);
            if (aSS != null) {
                for (int i = 1; i < last; i++) {
                    if (dic_name_sec.TryGetValue(i, out float start)) {
                        if (dic_name_sec.TryGetValue(i + 1, out float end)) {
                            aSS.fx顺序分割并保存(di切片, start, end, i.ToString( ));
                        }
                    }
                }
                _b切片字幕 = true;
            } else if (sRT != null) {
                for (int i = 1; i < last; i++) {
                    if (dic_name_sec.TryGetValue(i, out float start)) {
                        if (dic_name_sec.TryGetValue(i + 1, out float end)) {
                            sRT.fx顺序分割并保存(di切片, start, end, i.ToString( ));
                        }
                    }
                }
                _b切片字幕 = true;
            }



        }
        void 字幕切片( ) {
            if (list_切片体积降序.Count > 0) {
                int count = list_切片体积降序.Count( );
                float sec_Count = 0;
                if (aSS != null) {
                    for (int i = 1; i <= count; i++) {
                        if (External_Process.get_ffprobe读取视频时长(str切片路径 + "\\" + i + ".mkv", out double sec)) {
                            float sec_Next = sec_Count + (float)sec;
                            aSS.fx顺序分割并保存(di切片, sec_Count, sec_Next, i.ToString( ));
                            sec_Count = sec_Next;
                        }
                    }
                    _b切片字幕 = true;
                } else if (sRT != null) {
                    for (int i = 1; i <= count; i++) {
                        if (External_Process.get_ffprobe读取视频时长(str切片路径 + "\\" + i + ".mkv", out double sec)) {
                            float sec_Next = sec_Count + (float)sec;
                            sRT.fx顺序分割并保存(di切片, sec_Count, sec_Next, i.ToString( ));
                            sec_Count = sec_Next;
                        }
                    }
                    _b切片字幕 = true;

                }
            }
        }

        bool b重算时间码(string path转码完成, List<int> list_SerialName) {
            float f帧毫秒 = 1000 / info.f输出帧率;
            string path切片日志 = string.Format("{0}\\视频切片_{1}.log", di切片.FullName, di切片.Name.Substring(3));
            List<float> list_timestamp = new List<float>( ) { };

            bool b成功重算 = false;
            if (File.Exists(path切片日志)) {
                string timestamps;
                try { timestamps = File.ReadAllText(path切片日志); } catch { timestamps = string.Empty; }
                int i开始 = timestamps.IndexOf("--split timestamps:") + 19;

                if (i开始 > 19) {
                    int i结束 = timestamps.IndexOf(" --disable-track-statistics-tags", i开始);//生成的日志本文紧跟禁用标记参数
                    if (i结束 > i开始 + 2) {
                        timestamps = "0s," + timestamps.Substring(i开始, i结束 - i开始);//补齐第一个切片时间偏移0秒
                        string[] arr指定秒 = timestamps.Split(',');
                        if (arr指定秒.Length > 1) {//最少有两个切片时间戳
                            bool b中段缺失 = false;
                            for (int i = 0; i < arr指定秒.Length; i++) {
                                if (float.TryParse(arr指定秒[i].TrimEnd('s'), out float f开始秒)) {//生成的切片时间戳格式 123.45s,234.56s
                                    string path时间码 = string.Format("{0}\\{1}_timestamp.txt", path转码完成, i + 1);
                                    if (File.Exists(path时间码)) {
                                        if (b中段缺失) return false;//前一个读取失败代表缺失一整段，直接退出。
                                        string[] arr时间码;
                                        try { arr时间码 = File.ReadAllLines(path时间码); } catch { arr时间码 = null; b中段缺失 = true; }
                                        if (arr时间码 != null && arr时间码.Length > 2) {//经分析：第一行版本，第二行→第一帧起始时间戳，最后一行→最后一帧结束时间戳（最后一行有BUG，出现全片时长）
                                            int end = arr时间码.Length - 1;//不要最后一行截止时间戳，和下一个分片起始时间冲突。
                                            float f开始毫秒 = f开始秒 * 1000;
                                            float f当前毫秒 = f开始毫秒;
                                            for (int t = 1; t < end; t++) {
                                                if (float.TryParse(arr时间码[t], out float f偏移毫秒)) {
                                                    f当前毫秒 = f开始毫秒 + f偏移毫秒;//切片起始时间戳+帧起始时间戳（偏移毫秒）
                                                } else {
                                                    f当前毫秒 += f帧毫秒;
                                                }
                                                list_timestamp.Add(f当前毫秒);
                                            }
                                        }//允许无帧切片，可变帧率视频数秒内无新帧，但被独立切片。
                                    } else { b中段缺失 = true; }//缺失时间戳文件则不能继续，一个切片无法正确则全片时间不重算。（完美计算则允许删除末尾片段。）
                                } else { b成功重算 = false; break; }//秒数据不规范
                            }//遍历
                            b成功重算 = true;
                        } else b成功重算 = false;//时间戳过少
                    } else b成功重算 = false; //时间戳末尾不匹配
                } else b成功重算 = false; //无法匹配以时间参数
            } else b成功重算 = false;  //找不到切片日志

            if (!b成功重算) {
                list_timestamp.Clear( );
                string[] arr_转码完成 = Directory.GetFiles(path转码完成, "*_转码完成.log");
                string[] arr_时间码 = Directory.GetFiles(path转码完成, "*_timestamp.txt");

                if (arr_转码完成.Length == arr_时间码.Length) {
                    float f汇总毫秒 = 0;
                    bool b中段缺失 = false;
                    for (int i = 1; i <= arr_转码完成.Length; i++) {
                        string log;
                        try { log = File.ReadAllText(path转码完成 + "\\" + i + "_转码完成.log"); } catch { log = null; }

                        int end = 0;
                        float f切片时长 = 0;

                        if (_b无缓转码 && vTimeBase.GetSpan偏移(i, out Span偏移 span)) {
                            f切片时长 = span.f持续秒;
                            end = span.out_frames;
                        }

                        if (!string.IsNullOrEmpty(log)) {
                            if (External_Process.regexFrame.IsMatch(log))
                                int.TryParse(External_Process.regexFrame.Match(log).Groups[1].Value, out end);

                            if (f切片时长 <= 0) {
                                if (regex秒长.IsMatch(log))
                                    f切片时长 = float.Parse(VideoInfo.regexDuration.Match(log).Groups[1].Value) * 1000;
                                else if (!log.Contains("-ss ") && !log.Contains(" -to "))
                                    f切片时长 = (float)TimeSpan.Parse(regex日时分秒.Match(log).Groups[1].Value).TotalMilliseconds;
                            }
                        }

                        string[] arr时间码;
                        try { arr时间码 = File.ReadAllLines(path转码完成 + "\\" + i + "_timestamp.txt"); } catch { arr时间码 = null; b中段缺失 = true; }//读取时间码失败则标记为中断缺失

                        if (arr时间码 != null && arr时间码.Length > 2) {
                            if (b中段缺失)
                                return false;//当遇到有标记缺失，正确读取到下一段则直接退出。

                            if (end == 0 || end >= arr时间码.Length)
                                end = arr时间码.Length - 2;

                            float f当前毫秒 = f汇总毫秒;
                            for (int t = 1; t <= end; t++) {
                                if (float.TryParse(arr时间码[t], out float f偏移毫秒)) {
                                    f当前毫秒 = f汇总毫秒 + f偏移毫秒; //切片起始时间戳+帧起始时间戳（偏移毫秒）
                                } else {
                                    f当前毫秒 += f帧毫秒;
                                }
                                list_timestamp.Add(f当前毫秒);
                            }
                        }
                        f汇总毫秒 += f切片时长;
                    }
                }
            }

            float f全片时长 = (float)info.time视频时长.TotalSeconds;

            if (list_timestamp.Count > 1) {
                if (list_timestamp[list_timestamp.Count - 1] > f全片时长)
                    f全片时长 = list_timestamp[list_timestamp.Count - 1] + f帧毫秒;

                list_timestamp.Add(f全片时长);

                list_timestamp.Sort( );

                StringBuilder sbTC = new StringBuilder("# timestamp format v2");
                for (int i = 0; i < list_timestamp.Count; i++)
                    sbTC.AppendLine( ).AppendFormat("{0:F0}", list_timestamp[i]);

                string txt时间码 = sbTC.ToString( );
                try { File.WriteAllText($"{str切片路径}\\重算时间码_{str编码摘要}.txt", txt时间码); } catch { }
                return true;
            }
            return false;
        }

        bool b连接序列切片(string path转码完成, out FileInfo fi连接后视频) {
            fi连接后视频 = new FileInfo($"{di切片.FullName}\\{str编码摘要}{str输出格式}");
            if (path转码完成.LastIndexOf("\\转码完成.") + 6 < 10) return false;

            string[] arr切片_转码后 = Directory.GetFiles(path转码完成, $"*{str输出格式}");
            List<int> list_SerialName = new List<int>( );
            for (int i = 0; i < arr切片_转码后.Length; i++) {
                string name = arr切片_转码后[i].Substring(path转码完成.Length + 1, arr切片_转码后[i].Length - path转码完成.Length - str输出格式.Length - 1);
                if (int.TryParse(name, out int num) && name == num.ToString( )) {
                    list_SerialName.Add(num);
                    if (!_b音轨同时切片) {
                        string timeCodeFile = $"{path转码完成}\\{num}_timestamp.txt";
                        if (!File.Exists(timeCodeFile))
                            External_Process.subProcess(EXE.mkvextract, $"timestamps_v2 {num}{str输出格式} 0:{num}_timestamp.txt", path转码完成, out string Output, out string Error);
                    }
                }
            }
            if (!_b音轨同时切片) {
                if (_b无缓转码) vTimeBase.is重算时间码(path转码完成, str编码摘要);
                else b重算时间码(path转码完成, list_SerialName);
            }

            if (fi连接后视频.Exists) {
                return true;
            } else {
                if (list_SerialName.Count < 1) return false;
                else list_SerialName.Sort( );

                StringBuilder builder = new StringBuilder( );

                FileInfo fi第一个webM = new FileInfo($"{path转码完成}\\{list_SerialName[0]}{str输出格式}");
                builder.AppendFormat("--output \"{0}\" {1}{2}", fi连接后视频.FullName, list_SerialName[0], str输出格式);

                for (int i = 1; i < list_SerialName.Count; i++)
                    builder.Append(" + ").Append(list_SerialName[i]).Append(str输出格式);

                builder.Append("  --title \"").Append(str连接视频名).Append("\"");
                builder.Append(" --disable-track-statistics-tags --flush-on-close ");

                External_Process ep = new External_Process(mkvmerge, builder.ToString( ), path转码完成, fi第一个webM);

                string str日志文件 = $"连接序列切片_{fi连接后视频.Name}.log";
                bool bsuccess = ep.sync_MKVmerge保存消息(path转码完成, str日志文件, out string[] logs, ref builder);

                if (File.Exists(fi连接后视频.FullName)) {
                    fi合并日志 = new FileInfo($"{path转码完成}\\{str日志文件}");
                    return true;
                } else {
                    fi连接后视频 = null;
                    try { File.WriteAllText($"{di切片.FullName}\\连接序列视频失败{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.errlog", builder.ToString( )); } catch { }
                    return false;
                }
            }
        }

        bool b封装视频音频(FileInfo fi连接后视频) {
            if (fi连接后视频 == null) return false;

            str转码后MKV名 = str连接视频名 + str音频摘要;

            string str封装的视频路径 = $"{di切片.Parent.FullName}\\{str转码后MKV名}{str最终格式}";

            string str转码后MKV路径_1 = $"{str切片路径}\\{str转码后MKV名}.mkv";
            string str转码后MKV路径_2 = $"{di切片.Parent.FullName}\\{str转码后MKV名}.mkv";

            string str转码后MKV路径_3 = $"{str切片路径}\\{str转码后MKV名}.webm";
            string str转码后MKV路径_4 = $"{di切片.Parent.FullName}\\{str转码后MKV名}.webm";

            if (File.Exists(str转码后MKV路径_2) || File.Exists(str转码后MKV路径_4)) {
                return true;
            } else if (File.Exists(str转码后MKV路径_1)) {
                try { File.Move(str转码后MKV路径_1, str转码后MKV路径_2); return true; } catch { return false; }
            } else if (File.Exists(str转码后MKV路径_3)) {
                try { File.Move(str转码后MKV路径_3, str转码后MKV路径_4); return true; } catch { return false; }
            }

            StringBuilder builder = new StringBuilder( );
            builder.AppendFormat("--output \"{0}.mkv\" --no-track-tags --no-global-tags --track-name 0:{1}", str转码后MKV名, str编码摘要);//视轨有文件移动操作，使用相对路径

            FileInfo fi时间码 = new FileInfo($"{str切片路径}\\重算时间码_{str编码摘要}.txt");
            if (fi时间码.Exists) {
                builder.Append(" --timestamps 0:").Append(fi时间码.Name);
            }

            builder.AppendFormat(" --no-chapters \"{0}\"", fi连接后视频.FullName);

            if (fiOPUS != null && File.Exists(fiOPUS.FullName)) {//先查找独立转码opus。
                builder.AppendFormat(" --no-track-tags --no-global-tags \"{0}\"", fiOPUS.FullName);//音轨使用绝对路径，无需音轨拷贝一次。
            } else if (fiMKA != null && File.Exists(fiMKA.FullName)) {//再查找准备好的音轨。
                builder.AppendFormat(" --no-video --no-track-tags --no-global-tags \"{0}\"", fiMKA.FullName);
            } else if (File.Exists(fi输入视频.FullName)) {//再查找准备好的音轨。
                builder.AppendFormat(" --no-video --no-track-tags --no-global-tags \"{0}\"", fi输入视频.FullName);//最后尝试使用视频源
            }
            if (!_b硬字幕) {
                fx查找封装字幕文件( );
                if (list_fi外挂字幕.Count > 0) {
                    for (int i = 0; i < list_fi外挂字幕.Count; i++)
                        if (File.Exists(list_fi外挂字幕[i].FullName))
                            builder.AppendFormat(" \"{0}\"", list_fi外挂字幕[i].FullName);
                }
            }

            FileInfo fi切片日志 = null;
            if (fi拆分日志 != null && File.Exists(fi拆分日志.FullName)) {
                fi切片日志 = fi拆分日志;
            } else if (fi视频头信息 != null) {
                string copyPath = $"{fi连接后视频.DirectoryName}\\{fi视频头信息.Name}";
                if (!File.Exists(copyPath)) {
                    try { fi视频头信息.CopyTo(copyPath); } catch { }
                }
                if (File.Exists(copyPath)) {
                    fi切片日志 = new FileInfo(copyPath);
                }
            }
            if (_b无缓转码) {
                FileInfo fi无缓转码CSV = new FileInfo(str切片路径 + "\\无缓转码.csv");
                if (fi无缓转码CSV.Exists)
                    builder.AppendFormat(" --attachment-name 分段.csv --attachment-mime-type text/csv --attach-file \"{0}\"", fi无缓转码CSV.FullName);
            } else {
                if (fi切片日志 != null)
                    builder.AppendFormat(" --attachment-name 切片日志.txt --attachment-mime-type text/plain --attach-file \"{0}\"", fi切片日志.FullName);
            }


            if (fi合并日志 != null) {
                string copy = $"{fi连接后视频.DirectoryName}\\{fi合并日志.Name}";
                if (File.Exists(copy)) {
                    try { File.Delete(copy); } catch { }
                }
                try { fi合并日志.CopyTo(copy); } catch { }

                if (File.Exists(copy)) {
                    fi合并日志 = new FileInfo(copy);
                    builder.AppendFormat(" --attachment-name 合并日志.txt --attachment-mime-type text/plain --attach-file \"{0}\"", fi合并日志.FullName);
                }
            }

            builder.AppendFormat(" --title \"{0}\" --disable-track-statistics-tags --track-order 0:0,1:0", str转码后MKV名);

            string str日志文件 = $"封装视频音频_{str转码后MKV名}.log";

            bool bSuccess = new External_Process(mkvmerge, builder.ToString( ), fi连接后视频.DirectoryName, fi连接后视频).sync_MKVmerge保存消息(fi连接后视频.DirectoryName, str日志文件, out string[] logs, ref builder);

            if (bSuccess) {
                try { File.Move(str转码后MKV路径_1, str封装的视频路径); return true; } catch { }
            } else {
                if (File.Exists(str转码后MKV路径_1)) {
                    try { File.Move(str转码后MKV路径_1, str封装的视频路径); return true; } catch { }
                    try { File.WriteAllText($"{di切片.FullName}\\封装视频音频有异常{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.errlog", builder.ToString( )); } catch { }
                    return true;
                } else {
                    try {
                        File.WriteAllText($"{di切片.FullName}\\封装视频音频失败{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.errlog", builder.ToString( ));
                    } catch { }
                }
            }
            return false;
        }

        bool b移动带音轨切片合并视频(FileInfo fi连接后视频) {
            if (fi连接后视频 == null) return false;

            str转码后MKV名 = str连接视频名 + str音频摘要;
            string str封装的视频路径 = $"{di切片.Parent.FullName}\\{str转码后MKV名}{str最终格式}";

            string str转码后MKV路径_1 = $"{str切片路径}\\{str转码后MKV名}.mkv";
            string str转码后MKV路径_2 = $"{di切片.Parent.FullName}\\{str转码后MKV名}.mkv";

            string str转码后MKV路径_3 = $"{str切片路径}\\{str转码后MKV名}.webm";
            string str转码后MKV路径_4 = $"{di切片.Parent.FullName}\\{str转码后MKV名}.webm";

            if (File.Exists(str转码后MKV路径_2) || File.Exists(str转码后MKV路径_4)) {
                return true;
            } else if (File.Exists(str转码后MKV路径_1)) {
                try { File.Move(str转码后MKV路径_1, str转码后MKV路径_2); return true; } catch { return false; }
            } else if (File.Exists(str转码后MKV路径_3)) {
                try { File.Move(str转码后MKV路径_3, str转码后MKV路径_4); return true; } catch { return false; }
            }

            try { fi连接后视频.MoveTo(str封装的视频路径); return true; } catch { }

            return false;
        }

    }
}
