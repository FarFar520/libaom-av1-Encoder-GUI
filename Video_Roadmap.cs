using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace 破片压缩器 {
    internal class Video_Roadmap {


        public static HashSet<string> mapVideoExt = new HashSet<string>( )
   { ".264",".h264",".x264",".avi",".wmv",".wmp",".wm",".asf",".mpg",".mpeg",".mpe",".m1v",".m2v",".mpv2",".mp2v",".ts",".tp",".tpr",".trp",".vob",".ifo",".ogm",".ogv",".mp4",".m4v",".m4p",".m4b",".3gp",".3gpp",".3g2",".3gp2",".mkv",".rm",".ram",".rmvb",".rpm",".flv",".mov",".qt",".nsv",".dpg",".m2ts",".m2t",".mts",".dvr-ms",".k3g",".skm",".evo",".nsr",".amv",".divx",".wtv",".f4v",".mxf"
};
        string denoise;
        string preset;
        float crf;
        int fontsize = 19;
        bool drawtext;

        const string ffmpeg单线程 = " -threads 1 -filter_threads 1 -filter_complex_threads 1";

        string gop {
            get {
                return $" -g {Math.Ceiling(info.f输出帧率 * Settings.sec_gop)}";
            }
        }

        string get_加水印滤镜(string num) {
            List<string> list = new List<string>( );

            if (info.b隔行扫描) list.Add("bwdif=1:-1:1");//顺序1.反交错

            if (info.b剪裁滤镜) list.Add(info.str剪裁滤镜);
            if (info.b缩放滤镜) list.Add(info.str缩放滤镜);

            if (str自定义滤镜 != null)
                list.Add(str自定义滤镜);

            if (bVFR) list.Add("mpdecimate");//顺序末.去掉重复帧

            list.Add($"drawtext=text='{info.str视频名无后缀} - {num}'{str水印字体参数}: fontsize={fontsize}: fontcolor=white@0.618: x=(w-text_w): y=0");

            StringBuilder builder = new StringBuilder( );
            if (list.Count > 0) {
                builder.Append(" -lavfi \"").Append(list[0]);
                for (int i = 1; i < list.Count; i++) {
                    builder.Append(',').Append(list[i]);
                }
                builder.Append('"');
            }

            builder.Append(" -fps_mode ").Append(Settings.b转可变帧率 ? "vfr" : "passthrough");

            return builder.ToString( );
        }

        string get_lavfi( ) {
            List<string> list = new List<string>( );

            if (info.b隔行扫描) list.Add("bwdif=1:-1:1");//顺序1.反交错

            if (info.b剪裁滤镜) list.Add(info.str剪裁滤镜);
            if (info.b缩放滤镜) list.Add(info.str缩放滤镜);

            if (Settings.b自定义滤镜)
                list.Add(Settings.str自定义滤镜);

            if (bVFR) list.Add("mpdecimate");//顺序末.去掉重复帧

            StringBuilder builder = new StringBuilder( );

            if (list.Count > 0) {
                builder.Append(" -lavfi \"").Append(list[0]);

                for (int i = 1; i < list.Count; i++) {
                    builder.Append(',').Append(list[i]);
                }
                builder.Append('"');
            }

            builder.Append(" -fps_mode ").Append(Settings.b转可变帧率 ? "vfr" : "passthrough");
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
                    if (_b_opus) {
                        str音频摘要 = ".opus";
                        if (info.list音频轨.Count == 1 && Settings.i声道 > 0) {
                            if (Settings.i声道 == 2 && info.list信息流[info.list音频轨[0]].Contains("stereo")) {
                            } else
                                builder.Append(" -ac ").Append(Settings.i声道);

                            str音频摘要 += $"{Settings.i声道}.0";
                        }
                        builder.Append(" -map 0:a -c:a libopus -vbr on -compression_level 10");//-vbr 1~10
                        builder.Append(" -b:a ").Append(Settings.i音频码率).Append("k");
                        str音频摘要 += $".{Settings.i音频码率}k";
                    } else {
                        builder.Append(" -c:a copy");
                        str音频摘要 = info.get音轨code;
                        if (str音频摘要 != ".opus") str最终格式 = ".mkv";
                    }
                    if (info.list字幕轨.Count > 0)
                        builder.Append(" -c:s copy");

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

        public FileInfo fi输入视频;

        List<float> list_typeI_pts_time = new List<float>( );

        List<FileInfo> list_切片体积降序 = new List<FileInfo>( );
        Dictionary<FileInfo, TimeSpan> dic_切片_时长 = new Dictionary<FileInfo, TimeSpan>( );

        public string str输入路径, str切片路径, lower完整路径_输入视频;

        public static string ffmpeg = "ffmpeg", ffprobe = "ffprobe", mkvmerge = "mkvmerge", mkvextract = "mkvextract";

        DirectoryInfo di编码成功文件夹, di切片文件夹;

        public VideoInfo info;


        public static bool bVFR = true;

        FileInfo fiMKA = null, fiOPUS = null, fi视频头信息 = null, fi拆分日志 = null, fi合并日志 = null;


        string lib视频编码器, lib多线程视频编码器, str全片滤镜, str自定义滤镜, str编码摘要, str音频命令, str音频摘要;
        string str连接视频名, str转码后MKV名, strMKA名, str完整路径MKA, str水印字体路径, str水印字体参数;


        string str输出格式 = ".mkv", str最终格式 = ".webm";

        public string str输出文件名 => str转码后MKV名;
        public string strMKA文件名 => strMKA名;
        public string strMKA路径 => str完整路径MKA;


        bool _b有切片记录 = false, _b音轨同时切片 = false, _b_opus = false, b音轨已转码 = false;
        public bool b有切片记录 => _b有切片记录;
        public bool b音轨同时切片 => _b音轨同时切片;

        Thread th音频转码;

        public Video_Roadmap(FileInfo fileInfo, string str正在转码文件夹) {
            _b_opus = Settings.opus;
            bVFR = Settings.b转可变帧率;
            drawtext = Settings.b右上角文件名_切片序列号水印;

            if (Settings.b自定义滤镜)
                str自定义滤镜 = Settings.str自定义滤镜;

            fi输入视频 = fileInfo;
            info = new VideoInfo(fileInfo);

            str输入路径 = fileInfo.Directory.FullName;
            lower完整路径_输入视频 = fileInfo.FullName.ToLower( );
            str切片路径 = $"{str正在转码文件夹}\\切片_{fi输入视频.Name}";

            if (!转码队列.dic_切片路径_剩余.ContainsKey(str切片路径))
                转码队列.dic_切片路径_剩余.Add(str切片路径, 0);

            di切片文件夹 = new DirectoryInfo(str切片路径);

            if (!Directory.Exists(str切片路径)) {
                try { Directory.CreateDirectory(str切片路径); } catch { return; }
            } else {
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
        }

        public static bool b查找可执行文件(out string log) {
            log = string.Empty;
            if (!EXE.find最新版ffmpeg(out ffmpeg)) log += "“ffmpeg.exe”、";
            if (!EXE.find最新版ffprobe(out ffprobe)) log += "“ffprobe.exe”、";
            if (!EXE.find最新版mkvmerge(out mkvmerge)) log += "“mkvmerge.exe”、";
            if (!EXE.find最新版mkvextract(out mkvextract)) log += "“mkvextract.exe”、";
            if (log.Length > 0) {
                log = log.Substring(0, log.Length - 1);
                return false;
            } else
                return true;
        }

        public static bool is视频(FileInfo fileInfo) {//线程可能在长时间等待后，触发该逻辑存，需要判断文件还在。
            return mapVideoExt.Contains(fileInfo.Extension.ToLower( )) && File.Exists(fileInfo.FullName);
        }

        public int i剩余切片数量 => list_切片体积降序.Count;

        Regex regexPTS_Time = new Regex(@"pts_time:(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public bool b文件夹下还有切片 {
            get {
                if (_b_opus && !_b音轨同时切片 && !b音轨已转码 && info.list音频轨.Count > 0) return true;

                if (Directory.Exists(di切片文件夹.FullName)) {
                    FileInfo[] fileInfos = di切片文件夹.GetFiles("*.mkv");
                    for (int i = 0; i < fileInfos.Length; i++) {
                        string num = fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4);
                        if (uint.TryParse(num, out uint value)) return true;//做最简单判断，是序列号名。mkv就返回还有。
                    }

                    DirectoryInfo[] directoryInfos = di切片文件夹.GetDirectories( );
                    for (int i = 0; i < directoryInfos.Length; i++) {
                        if (directoryInfos[i].Name.Contains("正在转码")) {
                            FileInfo[] files = directoryInfos[i].GetFiles("*.mkv");
                            for (int f = 0; f < files.Length; f++) {
                                string num = fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4);
                                if (uint.TryParse(num, out uint value)) return true;// 协同编码的子工作目录，移动的序列mkv还在时表示未完成。
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
                    string path = $"{di切片文件夹.FullName}\\转码音轨{Settings.opus摘要}\\opus.mka";
                    if (File.Exists(path)) {
                        fiOPUS = new FileInfo(path);
                        str最终格式 = ".webm";
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

        public bool b解码60帧判断交错(out StringBuilder builder) {
            int scan_frame = 60;
            builder = new StringBuilder($"扫描{scan_frame}帧判断隔行扫描：");

            if (!info.b隔行扫描) {//视频头信息中有概率识别隔行扫描。
                string commamd = $"-i \"{fi输入视频.FullName}\" -select_streams v -read_intervals \"%+#{scan_frame}\" -show_entries \"frame=interlaced_frame\"";
                builder.AppendLine( ).AppendLine(commamd);
                if (new External_Process(ffprobe, commamd, str切片路径, fi输入视频).sync(out List<string> listOutput, out List<string> listError)) {
                    for (int i = 0; i < listError.Count; i++) info.fx信息分类(listError[i]);
                    info.v以帧判断隔行扫描(scan_frame, listOutput);

                    string str视频头信息文件路径 = $"{str切片路径}\\{fi输入视频.Name}.info";

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

            if (b读取场景切片数据($"{str输入路径}\\检测镜头({f检测阈值}).{fi输入视频.Name}.log")) return true;
            //另一个镜头检测工具生成的日志，不区分关键帧，使用算法计算切片时间戳
            string str检测镜头完整路径_1 = $"{str输入路径}\\检测镜头({f检测阈值}).{fi输入视频.Name}.txt";
            if (b读取场景切片数据(str检测镜头完整路径_1)) return true;
            //源目录下数据，视作外部程序生成日志。

            string str检测镜头完整路径_2 = $"{str切片路径}\\检测镜头({f检测阈值}).{fi输入视频.Name}.txt";
            if (b读取场景切片数据_全部(str检测镜头完整路径_2)) return true;//读取工作目录数据，认作本本地工具生成，完全切片。


            string str检测镜头文件名 = $"检测镜头({f检测阈值}).{fi输入视频.Name}.info";

            string str检测镜头完整路径_3 = $"{str输入路径}\\{str检测镜头文件名}";//本程序生成格式，以关键帧来对比
            if (b读取场景大于GOP切片数据(str检测镜头完整路径_3)) return true; //读取的数据，认作本工具生成，完全切片。

            string str检测镜头完整路径_4 = $"{str切片路径}\\{str检测镜头文件名}";//本程序生成格式，以关键帧来对比
            if (b读取场景大于GOP切片数据(str检测镜头完整路径_4)) return true;

            //无法读取外部则开扫
            string commamd = $"-loglevel info -i \"{fi输入视频.FullName}\" -an -sn -vf \"select='eq(pict_type,I)',select='gt(scene,{f检测阈值})',showinfo\" -f null -";//快速分割方案，以关键帧差异去判断切割点位。
            //string commamd = $"-loglevel info -i \"{fi输入视频.FullName}\" -an -sn -vf \"select='gt(scene,{f检测阈值})',showinfo\" -f null -";//固定帧率硬件压制资源切割点几乎不在关键帧上。

            if (!转码队列.b允许入队) commamd = ffmpeg单线程 + " " + commamd;//CPU资源被占满后，扫描以单线程运行减少损耗。

            builder.AppendLine( ).AppendLine("检测关键帧场景切换：").AppendLine(commamd);

            StringBuilder builder检测镜头 = new StringBuilder("检测关键帧场景切换：").AppendLine( ).AppendLine(commamd);

            转码队列.process场景 = new External_Process(ffmpeg, commamd, str切片路径, fi输入视频);
            bool b扫描 = 转码队列.process场景.sync_FFmpegInfo保存消息(str检测镜头文件名, out string[] logs, ref builder检测镜头);
            转码队列.process场景 = null;

            if (b扫描) {
                StringBuilder builder筛选后数据 = new StringBuilder( );
                builder筛选后数据.AppendLine("检测关键帧场景切换：").AppendLine(commamd).AppendLine( );
                int i = 0;
                for (; i < logs.Length; i++) {
                    if (logs[i].StartsWith("Duration:", StringComparison.OrdinalIgnoreCase)) {
                        builder筛选后数据.AppendLine(logs[i]);
                        break;
                    }
                }
                for (; i < logs.Length; i++) {
                    if (logs[i].StartsWith("[Parsed_showinfo") && logs[i].Contains("pts_time:")) {
                        builder筛选后数据.AppendLine(logs[i]);
                    }
                }
                builder.AppendLine( ).Append(builder筛选后数据);

                string str筛选后数据 = builder筛选后数据.ToString( );
                string str汇流数据 = builder检测镜头.ToString( );
                try { File.WriteAllText(str检测镜头完整路径_1, str汇流数据); } catch { }
                try { File.WriteAllText(str检测镜头完整路径_2, str汇流数据); } catch { }
                try { File.WriteAllText(str检测镜头完整路径_3, str筛选后数据); } catch { }
                try { File.WriteAllText(str检测镜头完整路径_4, str筛选后数据); } catch { }//检测场景需要解码一遍视频，（硬件解码分析效果不理想，只能软解码），较为耗时，多保存几个副本，防止误删。
                //return b解析场景所有切片数据(logs);
                return b解析场景大于GOP切片数据(logs);
            }
            return false;
        }

        public bool b扫描视频黑边生成剪裁参数( ) {
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

            return false;
        }

        public void Fx按场景切片并获取列表(ref StringBuilder builder) {
            if (list_typeI_pts_time.Count < 1) return;
            StringBuilder builder切片命令行 = new StringBuilder("--output %d.mkv --stop-after-video-ends --no-track-tags --no-global-tags --no-attachments");

            if (Settings.b音轨同时切片转码) _b音轨同时切片 = true;//音轨同时切片设计为自定义删减片段，保留字幕和章节
            else builder切片命令行.Append(" --no-audio  --no-subtitles --no-chapters");//此命令行有顺序，不可放到输入文件后。

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

            External_Process ep = new External_Process(mkvmerge, commamd, str切片路径, fi输入视频);
            转码队列.process切片 = ep;
            ep.sync_MKVmerge保存消息(str切片路径, str日志文件, out string[] logs, ref builder);
            转码队列.process切片 = null;

            if (File.Exists(str完整路径MKA)) {
                fiMKA = new FileInfo(str完整路径MKA);
                fi拆分日志 = new FileInfo($"{str切片路径}\\{str日志文件}");
                return true;
            }
            return fiMKA != null;
        }//提取音轨流程可放到转码前中后任意时刻

        public bool b拼接转码摘要( ) {
            if (info.list视频轨.Count < 1) return false;

            lib视频编码器 = Settings.Get_视频编码库(info, out lib多线程视频编码器, out crf, out preset, out denoise);

            info.fx输出宽高( );

            //str滤镜 = get_lavfi( );//滤镜根据视频头生成。
            str全片滤镜 = get_lavfi( );
            str音频命令 = get_encAudio( );

            str编码摘要 = $"{info.str长乘宽}.{Settings.str视频编码库}.crf{crf:F0}.p{preset}{denoise}";
            str连接视频名 = $"{fi输入视频.Name}.{info.get输出Progressive}.{Settings.str视频编码库}.crf{crf:F0}.p{preset}{denoise}";
            if (Settings.b转可变帧率) {
                str编码摘要 += ".vfr";
                str连接视频名 += ".vfr";
            }

            di编码成功文件夹 = new DirectoryInfo($"{str切片路径}\\转码完成.{str编码摘要}");
            //try { di编码成功文件夹.Create( ); } catch { }\\目录等任意切片转码完成后再创建。

            int font_size = info.i输出宽 / 100;
            if (font_size > 19) fontsize = font_size;//1920/100=19
            else fontsize = 19;

            if (drawtext) {
                if (File.Exists(str切片路径 + "\\drawtext.otf")) {
                    str水印字体参数 = ": fontfile=drawtext.otf";
                    str水印字体路径 = str切片路径 + "\\drawtext.otf";

                } else if (File.Exists(str切片路径 + "\\drawtext.ttf")) {
                    str水印字体参数 = ": fontfile=drawtext.ttf";
                    str水印字体路径 = str切片路径 + "\\drawtext.ttf";

                } else {
                    if (File.Exists("水印.otf")) {
                        try {
                            File.Copy("水印.otf", str切片路径 + "\\drawtext.otf");
                            str水印字体路径 = str切片路径 + "\\drawtext.otf";
                            str水印字体参数 = ": fontfile=drawtext.otf";
                            return true;
                        } catch { }
                    } else if (File.Exists("水印.ttf")) {
                        try {
                            File.Copy("水印.ttf", str切片路径 + "\\drawtext.ttf");
                            str水印字体路径 = str切片路径 + "\\drawtext.ttf";
                            str水印字体参数 = ": fontfile=drawtext.ttf";
                            return true;
                        } catch { }
                    }
                    str水印字体参数 = ": font='Microsoft YaHei'";//有效
                    fontsize -= 2;//微软雅黑比常见字体大1~2号
                }
            }
            //str水印字体参数 = ": font='Microsoft YaHei'";//有效
            //str水印字体参数 = ": font='微软雅黑'";//有效

            return true;
        }

        public bool b转码下一个切片(out External_Process external_Process) {
            external_Process = null;
            转码队列.dic_切片路径_剩余[str切片路径] = list_切片体积降序.Count;
            if (list_切片体积降序.Count > 0) {
                FileInfo fi切片;
                lock (obj切片) {
                    if (list_切片体积降序.Count > 0) {
                        fi切片 = list_切片体积降序[0];
                        list_切片体积降序.RemoveAt(0);
                    } else return false;
                }
                if (File.Exists(fi切片.FullName)) {//音频和视频同时编码方案，允许删除不需要片段。 视频分片+音轨单编，就不能缺失片。
                    string name = fi切片.Name.Substring(0, fi切片.Name.Length - 4);
                    string str滤镜 = drawtext ? get_加水印滤镜(name) : str全片滤镜;

                    string str编码后切片 = $"{name}_{str编码摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}{str输出格式}";

                    string str命令行;

                    if (Settings.b单线程)
                        str命令行 = $"-hide_banner{ffmpeg单线程} -i {fi切片.Name}{str滤镜}{lib视频编码器}{gop}{str音频命令} \"{str编码后切片}\"";
                    else {
                        str命令行 = $"-hide_banner -i {fi切片.Name}{str滤镜}{lib多线程视频编码器}{gop}{str音频命令} \"{str编码后切片}\"";
                    }
                    external_Process = new External_Process(ffmpeg, str命令行, fi切片);
                    external_Process.b单线程 = Settings.b单线程;

                    if (bVFR && !_b音轨同时切片)
                        external_Process.b补齐时间戳 = true;//如果ffmpeg删重复帧到最后一帧，会缺最终时间码，额外加了判断补齐破片时间码。

                    external_Process.b编码后删除切片 = true;
                    external_Process.fi编码 = new FileInfo($"{fi切片.DirectoryName}\\{str编码后切片}");
                    external_Process.di编码成功 = new DirectoryInfo($"{fi切片.DirectoryName}\\转码完成.{str编码摘要}");
                    return true;
                }
            }
            return false;
        }

        public bool b后台转码MKA音轨( ) {
            if (info.list音频轨.Count > 0 && _b_opus && !_b音轨同时切片) {//转码opus时，可以不分解mka文件
                if (fiMKA != null && File.Exists(fiMKA.FullName)) {
                    str音频摘要 = ".opus";
                    StringBuilder builder = new StringBuilder(ffmpeg单线程);

                    builder.Append(" -i \"").Append(fiMKA.FullName).Append('"');
                    builder.Append(" -map 0:a -c:a libopus -vbr on -compression_level 10");//忽略视频轨道，转码全部音轨，字幕轨可能会保留一条。
                    //builder.Append(" -map 0:a:0 -c:a libopus -vbr on -compression_level 10"); //只保留一条音轨

                    //builder.Append(" -c:a libopus -vbr 2.0 -compression_level 10");//vbr 0~2;//vbr不太好用

                    if (Settings.i音频码率 == 96 && Settings.i声道 == 0) {
                        //opus默认码率每声道48K码率。多声道自动计算方便。
                    } else
                        builder.Append(" -b:a ").Append(Settings.i音频码率).Append('k');

                    if (Settings.i声道 > 0) {
                        builder.Append(" -ac ").Append(Settings.i声道);
                        str音频摘要 += $"{Settings.i声道}.0";
                    }

                    str音频摘要 += $".{Settings.i音频码率}k";

                    fiOPUS = new FileInfo($"{fiMKA.DirectoryName}\\转码音轨{str音频摘要}\\{fiMKA.Name}");
                    if (!fiOPUS.Exists) {
                        string str临时文件 = $"临时{str音频摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.mka";
                        builder.AppendFormat(" -metadata encoding_tool=\"{0} {1}\" \"{2}\" ", Application.ProductName, Application.ProductVersion, str临时文件);

                        External_Process external_Process = new External_Process(ffmpeg, builder.ToString( ), fiMKA);
                        external_Process.fi编码 = new FileInfo($"{fiMKA.DirectoryName}\\{str临时文件}");
                        external_Process.di编码成功 = new DirectoryInfo($"{fiMKA.DirectoryName}\\转码音轨{str音频摘要}");

                        转码队列.process音轨 = external_Process;
                        external_Process.async_FFmpeg编码( );//音轨转码线程不占用队列。会超出cpu核心数。
                        转码队列.process音轨 = null;

                        th音频转码 = new Thread(new ParameterizedThreadStart(fn_音频转码成功信号));
                        th音频转码.IsBackground = true;
                        th音频转码.Start(external_Process); //避免音频比视频后出结果，额外开一条等待线程，完成时触发b音轨转码成功布尔值。
                        return true;
                    } else
                        b音轨已转码 = true;//文件存在直接标记
                }
            }
            return false;
        }
        public bool b更新OPUS音轨( ) {
            if (fiMKA == null || !File.Exists(fiMKA.FullName)) {//任务穿插进视频转码全过程，可能出现音轨被删除、视频被删除的情况。
                if (File.Exists(fi输入视频.FullName))
                    fiMKA = fi输入视频;
                else {
                    info.list音频轨.Clear( );
                    str音频摘要 = ".noAu";
                    b音轨已转码 = true;
                    return false;
                }
            }

            StringBuilder builder = new StringBuilder( );
            if (!转码队列.b允许入队) builder.Append(ffmpeg单线程);
            builder.Append(" -i \"").Append(fiMKA.FullName).Append('"');
            builder.Append(" -vn -map 0:a -c:a libopus -vbr on -compression_level 10");//-vn不处理视频， -map 0:a 转码全部音轨

            if (Settings.i音频码率 == 96 && Settings.i声道 == 0) {
                //opus默认码率每声道48K码率。多声道自动计算方便。
            } else {
                builder.Append(" -b:a ").Append(Settings.i音频码率).Append('k');
            }

            if (Settings.i声道 > 0) {
                builder.Append(" -ac ").Append(Settings.i声道);
            }
            str音频摘要 = Settings.opus摘要;
            string str临时文件 = $"{di切片文件夹.FullName}\\临时{str音频摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}.mka";//绝对目录输出音轨
            builder.AppendFormat(" -metadata encoding_tool=\"{0} {1}\" \"{2}\" ", Application.ProductName, Application.ProductVersion, str临时文件);

            External_Process external_Process = new External_Process(ffmpeg, builder.ToString( ), fiMKA);
            转码队列.process音轨 = external_Process;
            external_Process.sync_FFmpegInfo(out List<string> list);//音轨转码线程不占用队列。会超出cpu核心数。
            转码队列.process音轨 = null;

            FileInfo fi临时音轨 = new FileInfo(str临时文件);
            if (external_Process.b安全退出 && fi临时音轨.Exists) {
                FileInfo fi成功音轨 = new FileInfo($"{di切片文件夹.FullName}\\转码音轨{Settings.opus摘要}\\opus.mka");
                try { fi成功音轨.Directory.Create( ); } catch { return false; }
                try { fi临时音轨.MoveTo(fi成功音轨.FullName); } catch { return false; }
                fiOPUS = fi成功音轨;
                str最终格式 = ".webm";
                return File.Exists(fiOPUS.FullName);
            }

            b音轨已转码 = true;//尝试转码过也标记为真，不论成功失败。
            return false;
        }

        public bool b转码后混流( ) {//混流线程中有加入是否还有剩余切片判断。
            bool bSuccess = true;
            if (di编码成功文件夹 != null && Directory.Exists(di编码成功文件夹.FullName)) {//处理正常流程合并任务
                if (b连接序列切片(di编码成功文件夹.FullName, out FileInfo fi连接后视频)) {
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
                                };
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

            if (bSuccess && Settings.b转码成功后删除源视频) {
                try { fi输入视频.Delete( ); } catch { }
                if (fi输入视频.Directory.GetFiles("*.*", SearchOption.AllDirectories).Length == 0) {
                    try { fi输入视频.Directory.Delete( ); } catch { }
                }
            } else {
                if (!b文件夹下还有切片) {
                    string path源视频 = bSuccess ? $"{str输入路径}\\源视频" : $"{str输入路径}\\合并失败";

                    if (!Directory.Exists(path源视频)) {
                        try { Directory.CreateDirectory(path源视频); } catch { }
                    }
                    try { fi输入视频.MoveTo($"{path源视频}\\{fi输入视频.Name}"); } catch { }//正是运行再打开
                }
            }

            return bSuccess;
        }

        void fn_音频转码成功信号(object obj) {
            External_Process external_Process = (External_Process)obj;
            external_Process.process.WaitForExit( );
            if (external_Process.process.ExitCode == 0) str最终格式 = ".webm";
            b音轨已转码 = true;//转码过后不论成功失败标记为已转码。

            if (!b文件夹下还有切片) Form破片压缩.autoReset合并.Set( );
        }


        void fx扫描黑边(float ss, float t, ref uint count_Crop, ref Dictionary<string, int> dicCropdetect, ref StringBuilder builder) {
            string commamd = $"-ss {ss} -i \"{fi输入视频.Name}\" -t {t} -vf cropdetect=round=2 -f null -an /dev/null";
            if (!转码队列.b允许入队) commamd = ffmpeg单线程 + " " + commamd;//CPU资源被占满后，以单线程运行减少损耗。

            转码队列.process黑边 = new External_Process(ffmpeg, commamd, fi输入视频);
            bool b扫描 = 转码队列.process黑边.sync_FFmpegInfo(out List<string> list);
            转码队列.process黑边 = null;

            if (b扫描) {
                for (int i = 0; i < list.Count; i++)
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

        bool b读取场景切片数据(string path) {
            FileInfo fi = new FileInfo(path);
            if (fi.Exists && fi.Length < 3145729) {//读取3MB以内文件。
                string[] arr = File.ReadAllLines(path);
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
            if (fi.Exists && fi.Length < 3145729) {//读取3MB以内文件。
                string[] arr = File.ReadAllLines(path);
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

        bool b读取场景大于GOP切片数据(string path) {
            FileInfo fi = new FileInfo(path);
            if (fi.Exists && fi.Length < 3145729) {//读取3MB以内文件。
                string[] arr = File.ReadAllLines(path);
                return b解析场景大于GOP切片数据(arr);
            }
            return false;
        }

        bool b解析场景大于GOP切片数据(string[] arr) {
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
                            if (pts_time - last_pts_time > Settings.sec_gop) {//大于GOP长度
                                list_typeI_pts_time.Add(pts_time);
                                last_pts_time = pts_time;
                            }
                        }
                    }
                }
            }
            return list_typeI_pts_time.Count > 3;//至少能切3段，片头、片中、片尾。
        }
        void fx删除数字名称视频切片( ) {
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
                return list_切片体积降序.Count > 0;
            } else { //切片失败的情况，删除切片。
                fx删除数字名称视频切片( );
            }
            return false;
        }
        void 查找并按体积降序切片( ) {
            string[] arrFile = Directory.GetFiles(str切片路径, "*.mkv");
            int i = 0;
            lock (obj切片) { list_切片体积降序.Clear( ); }

            for (; i < arrFile.Length; i++) {
                FileInfo fi = new FileInfo(arrFile[i]);
                if (int.TryParse(fi.Name.Substring(0, fi.Name.Length - 4), out int num)) {
                    lock (obj切片) { list_切片体积降序.Add(fi); }
                    break;
                }
            }
            for (++i; i < arrFile.Length; i++) {
                FileInfo fi = new FileInfo(arrFile[i]);
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

            Thread th后台排序 = new Thread(fn_读取每个切片时长降序);
            th后台排序.Name = "按时长排序任务";
            th后台排序.IsBackground = true;
            th后台排序.Start( );
        }
        void fn_读取每个切片时长降序( ) {
            while (转码队列.b允许入队) Thread.Sleep(999);//队列满了之后开始按时长重拍
            if (list_切片体积降序.Count < 2) return;

            FileInfo[] fileInfos = list_切片体积降序.ToArray( );//转换成组读取信息，无需编码线程取列表值。
            for (int i = 0; i < fileInfos.Length; i++) {
                try {
                    FileInfo fi = fileInfos[i];
                    string cmd = "-i " + fi.Name;
                    External_Process ep = new External_Process(ffprobe, cmd, fi);
                    if (ep.Get_StandardError(out List<string> errors)) {
                        for (int e = 0; e < errors.Count; e++) {
                            if (errors[e].StartsWith("Duration:", StringComparison.OrdinalIgnoreCase)) {
                                int len = errors[e].IndexOf(',', 11) - 11;
                                if (len > 0) {
                                    if (TimeSpan.TryParse(errors[e].Substring(11, len), out TimeSpan timeSpan)) {
                                        if (!dic_切片_时长.ContainsKey(fi)) dic_切片_时长.Add(fi, timeSpan);
                                    }
                                }
                                break;
                            }
                        }
                    }
                } catch { }
            }

            if (dic_切片_时长.Count > 0) {
                var sort = from pair in dic_切片_时长 orderby pair.Value descending select pair;//按切片时长降序队列
                lock (obj切片) {//全队列加读写锁，另一条编码线程在同时取出。
                    HashSet<FileInfo> setFiles = list_切片体积降序.ToHashSet( );
                    list_切片体积降序.Clear( );
                    foreach (var s in sort) {
                        if (setFiles.Contains(s.Key)) {
                            setFiles.Remove(s.Key);
                            list_切片体积降序.Add(s.Key);
                        }
                    }
                    foreach (var f in setFiles) {//剩余的（读取时长失败）按体积插入队列。
                        for (int i = 0; i < list_切片体积降序.Count; i++) {
                            if (f.Length >= list_切片体积降序[i].Length) {
                                list_切片体积降序.Insert(i, f);
                                break;
                            }
                        }
                    }

                }
            }
        }

        bool b连接序列切片(string path, out FileInfo fi连接后视频) {
            fi连接后视频 = null;
            int start = path.LastIndexOf("\\转码完成.") + 6;
            if (start < 10) return false;

            string str输出文件 = $"{str连接视频名}{str输出格式}";
            string str日志文件 = $"连接序列切片_{str输出文件}.log";
            string str连接后视频路径 = $"{path}\\{str输出文件}";

            if (File.Exists(str连接后视频路径)) {
                fi连接后视频 = new FileInfo(str连接后视频路径);
                return true;
            } else {
                string str连接后视频 = $"{str切片路径}\\{str输出文件}";
                if (File.Exists(str连接后视频)) {
                    fi连接后视频 = new FileInfo(str连接后视频);
                    return true;
                }
            }

            if (File.Exists(str日志文件)) return false;//产生过日志，但找不到封装文件

            string[] arr切片_转码后 = Directory.GetFiles(path, $"*{str输出格式}");
            List<int> list_SerialName = new List<int>( );
            for (int i = 0; i < arr切片_转码后.Length; i++) {
                string name = arr切片_转码后[i].Substring(path.Length + 1, arr切片_转码后[i].Length - path.Length - str输出格式.Length - 1);
                if (int.TryParse(name, out int num) && name == num.ToString( )) {
                    list_SerialName.Add(num);
                }
            }
            if (list_SerialName.Count < 1) return false;

            list_SerialName.Sort( );

            StringBuilder builder = new StringBuilder( );

            FileInfo fi第一个webM = new FileInfo($"{path}\\{list_SerialName[0]}{str输出格式}");
            builder.AppendFormat("--output \"{0}\" {1}{2}", str输出文件, list_SerialName[0], str输出格式);

            for (int i = 1; i < list_SerialName.Count; i++)
                builder.Append(" + ").Append(list_SerialName[i]).Append(str输出格式);

            builder.Append("  --title \"").Append(str输出文件).Append("\"");

            External_Process ep = new External_Process(mkvmerge, builder.ToString( ), path, fi第一个webM);
            bool bsuccess = ep.sync_MKVmerge保存消息(path, str日志文件, out string[] logs, ref builder);

            if (File.Exists(str连接后视频路径)) {
                fi连接后视频 = new FileInfo(str连接后视频路径);
                fi合并日志 = new FileInfo($"{path}\\{str日志文件}");
                return true;
            } else
                return false;
        }

        bool b封装视频音频(FileInfo fi连接后视频) {
            if (fi连接后视频 == null) return false;

            str转码后MKV名 = str连接视频名 + str音频摘要;

            string str封装的视频路径 = $"{di切片文件夹.Parent.FullName}\\{str转码后MKV名}{str最终格式}";

            string str转码后MKV路径_1 = $"{str切片路径}\\{str转码后MKV名}.mkv";
            string str转码后MKV路径_2 = $"{di切片文件夹.Parent.FullName}\\{str转码后MKV名}.mkv";

            string str转码后MKV路径_3 = $"{str切片路径}\\{str转码后MKV名}.webm";
            string str转码后MKV路径_4 = $"{di切片文件夹.Parent.FullName}\\{str转码后MKV名}.webm";

            if (File.Exists(str转码后MKV路径_2) || File.Exists(str转码后MKV路径_4)) {
                return true;
            } else if (File.Exists(str转码后MKV路径_1)) {
                try { File.Move(str转码后MKV路径_1, str转码后MKV路径_2); return true; } catch { return false; }
            } else if (File.Exists(str转码后MKV路径_3)) {
                try { File.Move(str转码后MKV路径_3, str转码后MKV路径_4); return true; } catch { return false; }
            }
            string 切片目录webM = $"{str切片路径}\\{fi连接后视频.Name}";
            if (fi连接后视频.DirectoryName != 切片目录webM) {
                if (File.Exists(切片目录webM)) {
                    try { File.Delete(切片目录webM); } catch { return false; }
                }//删除以前混流过的webm。
                try { fi连接后视频.MoveTo(切片目录webM); } catch { return false; }
            }

            StringBuilder builder = new StringBuilder( );
            builder.AppendFormat("--output \"{0}.mkv\" --no-track-tags --no-global-tags --track-name 0:{1} \"{2}\"", str转码后MKV名, str编码摘要, fi连接后视频.FullName);//视轨有文件移动操作，使用相对路径

            if (fiOPUS != null && File.Exists(fiOPUS.FullName)) {//先查找独立转码opus。
                builder.AppendFormat(" --no-track-tags --no-global-tags \"{0}\"", fiOPUS.FullName);//音轨使用绝对路径，无需音轨拷贝一次。
            } else if (fiMKA != null && File.Exists(fiMKA.FullName)) {//再查找准备好的音轨。
                builder.AppendFormat(" --no-video --no-track-tags --no-global-tags \"{0}\"", fiMKA.FullName);
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

            if (fi切片日志 != null) {
                builder.AppendFormat(" --attachment-name \"切片日志.txt\" --attachment-mime-type text/plain --attach-file \"{0}\"", fi切片日志.Name);
            }

            if (fi合并日志 != null) {
                string copy = $"{fi连接后视频.DirectoryName}\\{fi合并日志.Name}";
                if (File.Exists(copy)) {
                    try { File.Delete(copy); } catch { }
                }

                try { fi合并日志.CopyTo(copy); } catch { }

                if (File.Exists(copy)) {
                    fi合并日志 = new FileInfo(copy);
                    builder.AppendFormat(" --attachment-name \"合并日志.txt\" --attachment-mime-type text/plain --attach-file \"{0}\"", fi合并日志.Name);
                }
            }

            builder.AppendFormat(" --title \"{0}\" --disable-track-statistics-tags --track-order 0:0,1:0", str转码后MKV名);

            string str日志文件 = $"封装视频音频_{str转码后MKV名}.log";

            bool bSuccess = new External_Process(mkvmerge, builder.ToString( ), fi连接后视频.DirectoryName, fi连接后视频).sync_MKVmerge保存消息(fi连接后视频.DirectoryName, str日志文件, out string[] logs, ref builder);

            if (bSuccess) {
                try { File.Move(str转码后MKV路径_1, str封装的视频路径); return true; } catch { }
            }
            return false;
        }

        bool b移动带音轨切片合并视频(FileInfo fi连接后视频) {
            if (fi连接后视频 == null) return false;

            str转码后MKV名 = str连接视频名 + str音频摘要;
            string str封装的视频路径 = $"{di切片文件夹.Parent.FullName}\\{str转码后MKV名}{str最终格式}";

            string str转码后MKV路径_1 = $"{str切片路径}\\{str转码后MKV名}.mkv";
            string str转码后MKV路径_2 = $"{di切片文件夹.Parent.FullName}\\{str转码后MKV名}.mkv";

            string str转码后MKV路径_3 = $"{str切片路径}\\{str转码后MKV名}.webm";
            string str转码后MKV路径_4 = $"{di切片文件夹.Parent.FullName}\\{str转码后MKV名}.webm";

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
