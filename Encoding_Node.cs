using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 破片压缩器 {
    internal class Encoding_Node {
        public static string cpuId = string.Empty, SerialNumber = string.Empty;
        public static string str主机名称 = Environment.MachineName;

        int fontsize = 19, i输出宽 = 1920;
        string str切片路径
            , str编码摘要 = string.Empty
            , str输出格式 = string.Empty
            , str滤镜lavfi = string.Empty
            , lavfi全局值 = string.Empty
            , str视频名无后缀 = string.Empty
            , str水印字体参数 = string.Empty
            , str编码指令 = string.Empty, str多线程编码指令 = string.Empty;


        bool _b转可变帧率 = false, _b使用全局滤镜 = true;

        string[] arr滤镜值;
        ushort u硬字幕下标 = ushort.MaxValue, u切片序号水印下标 = ushort.MaxValue;

        public DirectoryInfo di切片文件夹;
        DirectoryInfo di编码成功 = null, di协同编码 = null;

        List<FileInfo> list_切片体积降序 = new List<FileInfo>( );
        Dictionary<int, FileInfo> dic_序列_源切片 = new Dictionary<int, FileInfo>( );

        public string ffmpeg = "ffmpeg";

        object obj切片队列 = new object( );

        string get_文字滤镜(string num) {
            if (u硬字幕下标 < arr滤镜值.Length) {
                if (File.Exists(string.Format("{0}\\{1}.ass", di切片文件夹.FullName, num))) {
                    arr滤镜值[u硬字幕下标] = "subtitles='..\\\\" + num + ".ass'";
                } else {
                    if (File.Exists(string.Format("{0}\\{1}.ssa", di切片文件夹.FullName, num))) {
                        arr滤镜值[u硬字幕下标] = "subtitles='..\\\\" + num + ".ssa'";
                    } else {
                        if (File.Exists(string.Format("{0}\\{1}.srt", di切片文件夹.FullName, num))) {
                            arr滤镜值[u硬字幕下标] = $"subtitles='..\\\\{num}.srt{Settings.str文本硬字幕样式}'";
                        } else
                            arr滤镜值[u硬字幕下标] = string.Empty;
                    }
                }
            }
            if (u切片序号水印下标 < arr滤镜值.Length) {
                arr滤镜值[u切片序号水印下标] = $"drawtext=text='{str视频名无后缀} - {num}'{str水印字体参数}:fontsize={fontsize}:fontcolor=white@0.618:x=(w-text_w):y=0";
            }

            StringBuilder builder = new StringBuilder( );

            builder.Append(arr滤镜值[0]);
            for (ushort u = 1; u < arr滤镜值.Length; u++)
                builder.Append(',').Append(arr滤镜值[u]);

            if (builder.Length > 0) {
                builder.Insert(0, " -lavfi \"").Append('"');
            }
            builder.Append(" -fps_mode ").Append(_b转可变帧率 ? "vfr" : "passthrough");

            return builder.ToString( );
        }

        public Encoding_Node(FileInfo fi任务配置) {
            di切片文件夹 = fi任务配置.Directory;
            str切片路径 = di切片文件夹.FullName;

            string[] lines;
            try { lines = File.ReadAllLines(fi任务配置.FullName); } catch { return; }

            for (int i = 0; i < lines.Length; i++) {
                int i等号 = lines[i].IndexOf('=');

                if (i等号 > 0) {
                    string str变量 = lines[i].Substring(0, i等号),
                         str值 = lines[i].Substring(i等号 + 1);

                    switch (str变量) {
                        case "str编码摘要": str编码摘要 = str值.Trim( ); break;
                        case "str输出格式": str输出格式 = str值.Trim( ); break;
                        case "str滤镜lavfi": str滤镜lavfi = str值.Trim( ); break;
                        case "lavfi全局值": lavfi全局值 = str值.Trim( ); break;
                        case "str编码指令": str编码指令 = str值.Trim( ); break;
                        case "_b转可变帧率": _b转可变帧率 = str值.Trim( ).ToLower( ) == "true"; break;
                        case "str多线程编码指令": str多线程编码指令 = str值.Trim( ); break;
                        case "info.i输出宽": if (int.TryParse(str值, out int i宽)) i输出宽 = i宽; break;
                        case "info.str视频名无后缀=": str视频名无后缀 = str值.Trim( ); break;//不是必要参数，水印使用
                        case "di编码成功文件夹.Name": {
                            di编码成功 = new DirectoryInfo(fi任务配置.Directory.FullName + "\\" + str值);
                            break;
                        }
                    }
                }
            }

            arr滤镜值 = lavfi全局值.Split(',');
            for (ushort u = 0; u < arr滤镜值.Length; u++) {
                if (arr滤镜值[u] == "{硬字幕}") {
                    u硬字幕下标 = u;
                    _b使用全局滤镜 = false;
                } else if (arr滤镜值[u] == "{切片序号水印}") {
                    u切片序号水印下标 = u;
                    _b使用全局滤镜 = false;
                }
            }
        }

        public bool b准备协同任务(out string tips) {
            if (di编码成功 == null) {
                tips = "编码成功文件夹未设置：" + str切片路径;
                return false;
            }
            if (!b准备协同文件夹( )) {
                tips = "协同编码文件夹创建失败：" + str切片路径;
                return false;
            } else {
                if (!转码队列.dic_切片路径_剩余.ContainsKey(di协同编码.FullName))
                    转码队列.dic_切片路径_剩余.Add(di协同编码.FullName, 0);
            }
            if (!b查找剩余切片( )) {
                tips = null;
                return false;
            }
            if (string.IsNullOrEmpty(str编码摘要)) {
                tips = "编码摘要未设置：" + str切片路径;
                return false;
            } else if (string.IsNullOrEmpty(str输出格式)) {
                tips = "输出格式未设置：" + str切片路径;
                return false;
            } else if (string.IsNullOrEmpty(str滤镜lavfi)) {
                tips = "滤镜命令未设置：" + str切片路径;
                return false;
            }

            if (string.IsNullOrEmpty(str视频名无后缀)) {
                if (di切片文件夹.Name.StartsWith("切片_")) {
                    int end = di切片文件夹.Name.LastIndexOf('.');
                    if (end > 3) str视频名无后缀 = di切片文件夹.Name.Substring(3, end - 3);//默认取切片文件夹名称，去掉前缀“切片_”。
                }
            }

            list_切片体积降序.AddRange(dic_序列_源切片.Values.OrderByDescending(v => v.Length));


            if (list_切片体积降序.Count > 0) {
                准备水印字体( );
                EXE.find最新版ffmpeg(out ffmpeg);
                tips = "协同编码准备就绪：" + str切片路径;
                return true;
            } else {
                tips = "已扫描：" + str切片路径;
                return false;
            }
        }

        DateTime time上次刷新切片 = DateTime.Now.AddHours(1);
        public bool b转码下一个切片(out External_Process external_Process) {
            if (DateTime.Now.Subtract(time上次刷新切片).TotalHours > 1) {//每小时查找一次。
                time上次刷新切片 = DateTime.Now;
                Task.Run(( ) => { b未处理切片加入队列( ); });//异步刷新剩余切片，避免阻塞。
            }
            FileInfo fi切片 = null;
            lock (obj切片队列) {
                while (list_切片体积降序.Count > 0) {
                    if (File.Exists(list_切片体积降序[0].FullName)) {
                        if (list_切片体积降序[0].Directory.FullName.ToLower( ) != di协同编码.FullName.ToLower( )) {
                            try {
                                di协同编码.Create( );//协编文件夹有可能被删除，移动前尝试创建。
                                list_切片体积降序[0].MoveTo(di协同编码.FullName + "\\" + list_切片体积降序[0].Name);
                            } catch {
                                list_切片体积降序.RemoveAt(0);
                                continue;
                            }
                        } else {
                            try {
                                using (FileStream fs = System.IO.File.OpenWrite(list_切片体积降序[0].FullName)) { fs.Close( ); }
                            } catch {
                                list_切片体积降序.RemoveAt(0);
                                continue;
                            }
                        }
                        fi切片 = list_切片体积降序[0];
                        list_切片体积降序.RemoveAt(0);
                        break;

                    } else {
                        list_切片体积降序.RemoveAt(0);
                    }
                }
            }
            //启动逻辑不合理，有一个缓存在等待中，可以被其他机器抢占任务。

            转码队列.dic_切片路径_剩余[di协同编码.FullName] = list_切片体积降序.Count;

            if (fi切片 != null) {//音频和视频同时编码方案，允许删除不需要片段。 视频分片+音轨单编，就不能缺失片。
                string name = fi切片.Name.Substring(0, fi切片.Name.Length - 4);

                string str滤镜 = _b使用全局滤镜 ? str滤镜lavfi : get_文字滤镜(name);

                string str编码后切片 = $"{name}_{str编码摘要}丨{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}{str输出格式}";

                string str命令行;

                //待优化单线程解码可以降低解码并发损耗：1.跑解码能力测试 2.开启编码，获得编码速度，3决定单线程解码
                if (Settings.b多线程)
                    str命令行 = $"{EXE.ffmpeg单线程解码}-i {fi切片.Name} {str滤镜} {str多线程编码指令} \"{str编码后切片}\"{EXE.ffmpeg不显库}{EXE.ffmpeg单线程滤镜}";
                else {
                    str命令行 = $"{EXE.ffmpeg单线程解码}-i {fi切片.Name} {str滤镜} {str编码指令} \"{str编码后切片}\"{EXE.ffmpeg不显库}{EXE.ffmpeg单线程滤镜}";
                }
                external_Process = new External_Process(ffmpeg, str命令行, !Settings.b多线程, name, fi切片, di编码成功);
                external_Process.fi编码 = new FileInfo($"{fi切片.DirectoryName}\\{str编码后切片}");//fi切片设计为局域网编码时移动到另外文件夹，防止多机处理相同切片
                return true;
            } else
                external_Process = null;

            return false;
        }

        public bool b未处理切片加入队列( ) {
            if (b查找剩余切片( )) {//查找剩余切片带过滤处理过的路径，每次刷新获得新增的切片。
                lock (obj切片队列) {
                    list_切片体积降序.AddRange(dic_序列_源切片.Values);
                    list_切片体积降序 = list_切片体积降序.OrderByDescending(v => v.Length).ToList( );
                }
                return true;
            }
            return false;
        }

        HashSet<string> set已处理切片 = new HashSet<string>( );//每个路径只有一次机会，失败后不重复处理。
        bool b查找剩余切片( ) {
            dic_序列_源切片.Clear( );
            DirectoryInfo[] arr_di协编 = di切片文件夹.GetDirectories("*协同编码");
            for (int i = 0; i < arr_di协编.Length; i++) {
                FileInfo[] fi已有缓存 = arr_di协编[i].GetFiles("*.mkv");
                for (int j = 0; j < fi已有缓存.Length; j++) {
                    if (set已处理切片.Add(fi已有缓存[j].Name)) {//只记录切片名
                        try {
                            using (FileStream fs = File.OpenWrite(fi已有缓存[j].FullName)) { fs.Close( ); }//第三方节点正在编码的切片不添加队列。
                        } catch { continue; }
                        if (int.TryParse(fi已有缓存[j].Name.Substring(0, fi已有缓存[j].Name.Length - 4), out int n切片序号)) {
                            if (!dic_序列_源切片.ContainsKey(n切片序号)) dic_序列_源切片.Add(n切片序号, fi已有缓存[j]);  // 001.mkv、01.mkv、1.mkv
                        } else {
                            try { fi已有缓存[j].Delete( ); } catch { }//协编随时删除错误的任务碎片
                        }
                    }
                }
            }

            if (Directory.Exists(di切片文件夹.FullName)) {
                FileInfo[] fi已有缓存 = di切片文件夹.GetFiles("*.mkv");
                for (int i = 0; i < fi已有缓存.Length; i++) {
                    if (set已处理切片.Add(fi已有缓存[i].Name)) {
                        try {
                            using (FileStream fs = File.OpenWrite(fi已有缓存[i].FullName)) { fs.Close( ); }//第三方节点正在编码的切片不添加队列。
                        } catch { continue; }
                        if (int.TryParse(fi已有缓存[i].Name.Substring(0, fi已有缓存[i].Name.Length - 4), out int n切片序号))
                            if (!dic_序列_源切片.ContainsKey(n切片序号)) dic_序列_源切片.Add(n切片序号, fi已有缓存[i]);  // 001.mkv、01.mkv、1.mkv
                    }
                }
            }

            return dic_序列_源切片.Count > 0;
        }

        void 准备水印字体( ) {
            int font_size = i输出宽 / 100;
            if (font_size > 19) fontsize = font_size;//1920/100=19
            else fontsize = 19;

            if (u切片序号水印下标 < arr滤镜值.Length || Settings.b右上角文件名_切片序列号水印) {
                string str水印字体路径 = string.Empty;
                if (File.Exists(di协同编码.FullName + "\\drawtext.otf")) {
                    str水印字体路径 = di协同编码.FullName + "\\drawtext.otf";
                    str水印字体参数 = ": fontfile=drawtext.otf";
                } else if (File.Exists(di协同编码.FullName + "\\drawtext.ttf")) {
                    str水印字体路径 = di协同编码.FullName + "\\drawtext.ttf";
                    str水印字体参数 = ": fontfile=drawtext.ttf";
                } else if (File.Exists(str切片路径 + "\\drawtext.otf")) {
                    try {
                        File.Copy(str切片路径 + "\\drawtext.otf", di协同编码.FullName + "\\drawtext.otf");
                        str水印字体路径 = di协同编码.FullName + "\\drawtext.otf";
                        str水印字体参数 = ": fontfile=drawtext.otf";
                    } catch { }
                } else if (File.Exists(str切片路径 + "\\drawtext.ttf")) {
                    try {
                        File.Copy(str切片路径 + "\\drawtext.otf", di协同编码.FullName + "\\drawtext.ttf");
                        str水印字体路径 = di协同编码.FullName + "\\drawtext.ttf";
                        str水印字体参数 = ": fontfile=drawtext.ttf";
                    } catch { }
                } else if (File.Exists("水印.otf")) {
                    try {
                        File.Copy("水印.otf", di协同编码.FullName + "\\drawtext.otf");
                        str水印字体路径 = di协同编码.FullName + "\\drawtext.otf";
                        str水印字体参数 = ": fontfile=drawtext.otf";
                    } catch { }
                } else if (File.Exists("水印.ttf")) {
                    try {
                        File.Copy("水印.ttf", di协同编码.FullName + "\\drawtext.ttf");
                        str水印字体路径 = di协同编码.FullName + "\\drawtext.ttf";
                        str水印字体参数 = ": fontfile=drawtext.ttf";
                    } catch { }
                }

                if (string.IsNullOrEmpty(str水印字体参数)) {
                    str水印字体参数 = ": font='Microsoft YaHei'";//有效
                    fontsize -= 2;//微软雅黑比常见字体大1~2号
                }
            }
        }

        bool b准备协同文件夹( ) {
            string path = string.Format("{0}\\{1}协同编码", di切片文件夹.FullName, str主机名称);
            if (!Directory.Exists(path)) {
                try {
                    Directory.CreateDirectory(path);
                    di协同编码 = new DirectoryInfo(path);
                } catch {
                    path = string.Format("{0}\\{1}协同编码", di切片文件夹.FullName, cpuId);
                    if (!Directory.Exists(path)) {
                        try {
                            Directory.CreateDirectory(path);
                            di协同编码 = new DirectoryInfo(path);
                        } catch {
                            path = string.Format("{0}\\{1}协同编码", di切片文件夹.FullName, SerialNumber);
                            try {
                                Directory.CreateDirectory(path);
                                di协同编码 = new DirectoryInfo(path);
                            } catch { }
                        }
                    } else
                        di协同编码 = new DirectoryInfo(path);
                }
            } else
                di协同编码 = new DirectoryInfo(path);

            return di协同编码 != null;
        }
    }
}
