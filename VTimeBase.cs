using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace 破片压缩器 {
    internal class VTimeBase {
        public static Regex regex秒长 = new Regex(@"\[FORMAT\]\s+duration=(\d+\.\d+)\s+\[/FORMAT\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        SynchronizedCollection<float> list关键帧 = new SynchronizedCollection<float>( ) { 0 }, list所有帧 = new SynchronizedCollection<float>( ) { 0 }
           , list转场 = new SynchronizedCollection<float>( ) { 0 }
           , list黑场 = new SynchronizedCollection<float>( ) { 0 }
           , list白场 = new SynchronizedCollection<float>( ) { 0 }
           ;//全预先插入0帧起手，对比前一帧无需判断下标负溢出

        public ConcurrentDictionary<int, Span偏移> dic_分段_偏移 = new ConcurrentDictionary<int, Span偏移>( );

        VideoInfo vinfo;
        FileInfo fi输入文件;
        DirectoryInfo di输出目录;

        string str扫转场 = string.Empty, str扫黑场 = string.Empty, str扫白场 = string.Empty, str扫关键帧 = string.Empty;

        TimeSpan span扫转场进度 = TimeSpan.MaxValue, span扫黑场进度 = TimeSpan.MaxValue, span扫白场进度 = TimeSpan.MaxValue;

        Thread th扫关键帧, th扫转场, th扫黑场, th扫白场, th循环计算;

        ConcurrentDictionary<float, Scene.Info> dic转场帧 = new ConcurrentDictionary<float, Scene.Info>( );

        ConcurrentDictionary<float, string> dic帧类型 = new ConcurrentDictionary<float, string>( )
            , dic关键帧 = new ConcurrentDictionary<float, string>( )
            , dic黑场 = new ConcurrentDictionary<float, string>( )
            , dic白场 = new ConcurrentDictionary<float, string>( )
            ;
        Dictionary<float, HashSet<float>> dic_连续黑场 = new Dictionary<float, HashSet<float>>( ) { };//连续黑场可以用最低画质压缩

        string str单线程 = string.Empty;
        float scene = 0.11f, sec分割至少 = 2, sec_gop, Duration = 0, Duration加一帧, f连续黑场最小秒 = 0.5f;

        public AutoResetEvent event计算 = new AutoResetEvent(false);
        public AutoResetEvent reset再次获取 = new AutoResetEvent(false);

        bool b读取关键帧 = false, b读取转场 = false, b读取黑场 = false, b读取白场 = false, b正在计算 = true;

        string path输出目录关键帧时间戳, path输出目录转场时间戳, path输出目录黑场时间戳, path输出目录白场时间戳;


        public class Span偏移 {
            public int i分段号;
            public bool b已转码 = false;
            public float f指定画质CRF = -1;
            public float f关键帧, f转场, f结束, f偏移转场, f偏移结束, f持续秒;

            public int in_frames, out_frames;

            public TimeSpan ts分割时刻 => TimeSpan.FromSeconds(f转场);

            public Span偏移(int i分段号, float f关键帧, float f转场, float f结束, float in_fps) {
                this.i分段号 = i分段号;
                this.f关键帧 = f关键帧;
                this.f转场 = f转场;
                //f下一场 = f结束;
                f偏移转场 = f转场 - f关键帧;

                //this.f结束 = f结束 - f末帧秒长;
                //f偏移结束 = f结束 - f关键帧 - f末帧秒长; 
                this.f结束 = f结束;
                f偏移结束 = f结束 - f关键帧;//-ss 秒 含头， -to 秒 不包含时间戳帧 （含头不含尾）

                f持续秒 = f结束 - f转场;
                in_frames = (int)(f持续秒 * in_fps);
            }
            public Span偏移(string[] cells, out bool success, float in_fps) {
                //序号,转场秒,结束秒,最近关键帧秒
                if (cells.Length > 4) {
                    if (!int.TryParse(cells[0], out i分段号)) { success = false; return; }
                    if (!float.TryParse(cells[1], out f转场)) { success = false; return; }
                    if (!float.TryParse(cells[2], out f结束)) { success = false; return; }
                    if (!float.TryParse(cells[3], out f关键帧)) { success = false; return; }
                }
                if (float.TryParse(cells[4], out float crf)) {
                    if (crf > -1) f指定画质CRF = crf;
                }

                if (f关键帧 > f转场) f关键帧 = f转场;

                f偏移转场 = f转场 - f关键帧;
                f偏移结束 = f结束 - f关键帧;
                f持续秒 = f结束 - f转场;

                in_frames = (int)(f持续秒 * in_fps);
                success = true;
            }

            public void fx计算帧量(float in_fps, float out_fps) {
                in_frames = (int)(in_fps * f持续秒);
                out_frames = (int)(out_fps * f持续秒);
            }

            public string get二次跳转_SS_i_SS_T(FileInfo fi输入文件) {
                string cmd;
                if (f关键帧 > 0)
                    cmd = $"-ss {f关键帧} -i \"{fi输入文件.Name}\"";
                else
                    cmd = $"-i \"{fi输入文件.Name}\"";

                if (f偏移转场 > 0) cmd += $" -ss {f偏移转场}";
                if (f偏移结束 > 0) cmd += $" -t {f持续秒}";

                return cmd;
            }
            public string get二次跳转_SS_i_SS_TO(FileInfo fi输入文件) {
                string cmd;
                if (f关键帧 > 0)
                    cmd = $"-ss {f关键帧} -i \"{fi输入文件.Name}\"";
                else
                    cmd = $"-i \"{fi输入文件.Name}\"";

                if (f偏移转场 > 0) cmd += $" -ss {f偏移转场}";
                if (f偏移结束 > 0) cmd += $" -to {f偏移结束}";

                return cmd;
            }
            public string get精确跳转_i_SS_TO(FileInfo fi输入文件) {
                string cmd = $"-i \"{fi输入文件.Name}\"";
                if (f转场 > 0) cmd += $" -ss {f转场}";
                if (f结束 > 0) cmd += $" -to {f结束}";
                return cmd;
            }
        }
        HashSet<int> set体积降序编码序列 = new HashSet<int>( ), set已编码 = new HashSet<int>( );

        public int i剩余分段 => set体积降序编码序列.Count;
        public int i总分段 => dic_分段_偏移.Count;
        public VTimeBase(VideoInfo vinfo, DirectoryInfo di输出目录) {
            this.fi输入文件 = vinfo.fileInfo;
            this.di输出目录 = di输出目录;

            this.vinfo = vinfo;
            if (vinfo.time视频时长 > TimeSpan.Zero)
                Duration = (float)vinfo.time视频时长.TotalSeconds;

            path输出目录关键帧时间戳 = di输出目录.FullName + "\\关键帧时间戳.info";

        }

        public bool is扫描完成 {
            get {
                if (list关键帧.Count == 0) return true;

                if (list关键帧.Last( ) == Duration加一帧
                    //&& list白场.Last( ) == Duration加一帧
                    && list黑场.Last( ) == Duration加一帧
                    && list转场.Last( ) == Duration加一帧
                    ) return true;

                return false;
            }
        }

        Encoding getEncoding(FileInfo fi) {
            byte[] fileBytes = new byte[3]; // 创建一个缓冲区
            try {
                using (FileStream fileStream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read)) {
                    //读取文件的一部分数据
                    fileStream.Read(fileBytes, 0, fileBytes.Length);
                }
            } catch { }
            Encoding encoding = Encoding.Default;
            //UTF - 8
            if (fileBytes.Length == 3) {
                if ((fileBytes[0] == 34 && fileBytes[1] == 232 && fileBytes[2] == 167) ||
                    (fileBytes[0] == 239 && fileBytes[1] == 187 && fileBytes[2] == 191) ||
                    (fileBytes[0] == 229 && fileBytes[1] == 186 && fileBytes[2] == 143)
                    ) {
                    encoding = Encoding.UTF8;
                } else if (fileBytes[0] == 34 && fileBytes[1] == 202 && fileBytes[2] == 211) {
                    encoding = Encoding.GetEncoding("GB2312");
                } else if ((fileBytes[0] == 255 && fileBytes[1] == 254 && fileBytes[2] == 34) ||
                        (fileBytes[0] == 254 && fileBytes[1] == 255 && fileBytes[2] == 0)) {
                    encoding = Encoding.UTF32;
                }
            }
            return encoding;
        }

        public bool b读取无缓转码csv(DirectoryInfo di编码成功, TimeSpan ts视频时长) {
            double sec = ts视频时长.TotalSeconds - 0.5;
            FileInfo fiCSV = new FileInfo(di输出目录.FullName + "\\无缓转码.csv");
            if (fiCSV.Exists) {
                string[] arr;
                Encoding encoding = getEncoding(fiCSV);
                try { arr = File.ReadAllLines(fiCSV.FullName, encoding); } catch { return false; }
                if ((arr.Length > 3 && arr.Last( ).Trim( ).StartsWith("完成")) || (arr.Length > 4 && arr[arr.Length - 2].Trim( ).StartsWith("完成"))) {
                    for (int i = 1; i < arr.Length - 1; i++) {
                        string[] cell = arr[i].Split(',');
                        if (cell.Length > 5) {//理论上读取转场秒、结束秒即可工作。后续可以补全。
                            if (int.TryParse(cell[0], out int num)) {
                                if (!dic_分段_偏移.ContainsKey(num)) {
                                    Span偏移 span = new Span偏移(cell, out bool success, vinfo.f输入帧率);
                                    if (success) {
                                        dic_分段_偏移.TryAdd(num, span);
                                        if (File.Exists($"{di编码成功.FullName}\\{span.i分段号}.mkv")) set已编码.Add(span.i分段号);
                                    }
                                }
                            }
                        }
                    }

                    if (dic_分段_偏移.Count > 0) {
                        fx读取黑场Info( );

                        var dicSort_Sec = from objDic in dic_分段_偏移 orderby objDic.Value.f持续秒 descending select objDic;
                        foreach (var item in dicSort_Sec) lock (obj读取文件号)
                                if (!set已编码.Contains(item.Key)) set体积降序编码序列.Add(item.Key);//按时长排序添加

                        Span偏移 spanLast = dic_分段_偏移.Last( ).Value;
                        if (spanLast.f结束 >= sec) spanLast.f偏移结束 = spanLast.f结束 = 0;

                        b正在计算 = false;
                        return true;
                    }
                }
            }
            return false;
        }

        public void Start按关键帧(float sec_gop) {
            fx获取时长( );
            this.sec_gop = sec_gop * 6;

            fx读取关键帧( );
            if (!b读取关键帧) 转码队列.Add_VTimeBase(this);

            th循环计算 = new Thread(fn循环计算关键帧并存盘) { IsBackground = true, Name = "循环计算关键帧并存盘" + fi输入文件.Name };
            th循环计算.Start( );
        }

        public void Start按转场(bool b单线程, float scene, float sec_gop, float sec分割至少, float f连续黑场最小秒) {
            fx获取时长( );
            if (scene <= 0.001) this.scene = 0.001f;
            else if (scene >= 0.999) this.scene = 0.999f;
            else this.scene = scene;

            if (sec_gop >= sec分割至少) {
                this.sec_gop = sec_gop;
                this.sec分割至少 = sec分割至少;
            } else {
                this.sec_gop = sec分割至少;
                this.sec分割至少 = sec_gop;
            }
            this.f连续黑场最小秒 = f连续黑场最小秒;
            this.sec分割至少 = sec分割至少 < sec_gop ? sec分割至少 : sec_gop;
            this.sec_gop = sec_gop > sec分割至少 ? sec_gop : sec分割至少;

            //str单线程 = b单线程 ? EXE.ffmpeg单线程 : string.Empty;

            int x白度 = 98, x像素白阈 = 250;

            path输出目录转场时间戳 = di输出目录.FullName + $"\\检测镜头({scene:F3}).info";
            b读取转场 = is成功读取(ref list转场, "转场", path输出目录转场时间戳, ref span扫转场进度);
            if (!b读取转场) {
                string path视频同目录转场时间戳 = $"{fi输入文件.DirectoryName}\\检测镜头({scene:F3}).{fi输入文件.Name}.info";
                b读取转场 = is成功读取(ref list转场, "转场", path视频同目录转场时间戳, ref span扫转场进度);
                if (!b读取转场) {
                    th扫转场 = new Thread(fn扫转场) { IsBackground = true, Name = "扫转场" + fi输入文件.Name };
                    th扫转场.Start( );
                }
            }

            fx读取关键帧( );
            fx读取黑场Info( );

            path输出目录白场时间戳 = di输出目录.FullName + $"\\检测白场({x白度},{x像素白阈}).info";
            //b读取白场 = is成功读取(ref list白场, "白场", path输出目录白场时间戳, ref span扫白场进度);
            //if (!b读取白场) {
            //    string path视频同目录白场时间戳 = $"{fi输入文件.DirectoryName}\\检测白场({x白度},{x像素白阈}).{fi输入文件.Name}.info";
            //    b读取白场 = is成功读取(ref list白场, "白场", path视频同目录白场时间戳, ref span扫黑场进度);
            //    if (!b读取白场) {
            //        th扫白场 = new Thread(fn扫白场) { IsBackground = true ,Name = "扫白场" + fi输入文件.Name};
            //        th扫白场.Start( );
            //    }
            //}

            if (is扫描完成) {
                fx同步计算分段点并存盘( );
            } else {
                //th循环计算 = new Thread(fn循环计算分段点并存盘) { IsBackground = true, Name = "循环计算分段点并存盘" + fi输入文件.Name };
                //th循环计算.Start( );//异步计算算法有BUG，改用同步计算。
            }
            转码队列.Add_VTimeBase(this);
        }

        void fx读取黑场Info( ) {
            int x黑度 = 98, x像素黑阈 = 32;
            path输出目录黑场时间戳 = di输出目录.FullName + $"\\检测黑场({x黑度},{x像素黑阈}).info";

            b读取黑场 = is成功读取(ref list黑场, "黑场", path输出目录黑场时间戳, ref span扫黑场进度);
            if (!b读取黑场) {
                string path视频同目录黑场时间戳 = $"{fi输入文件.DirectoryName}\\检测黑场({x黑度},{x像素黑阈}).{fi输入文件.Name}.info";
                b读取黑场 = is成功读取(ref list黑场, "黑场", path视频同目录黑场时间戳, ref span扫黑场进度);
                if (!b读取黑场) {
                    list黑场 = new SynchronizedCollection<float>( ) { 0 };
                    th扫黑场 = new Thread(fn扫黑场) { IsBackground = true, Name = "扫黑场" + fi输入文件.Name };
                    th扫黑场.Start( );
                }
            }
        }
        void fx读取关键帧( ) {
            TimeSpan span = TimeSpan.Zero;
            b读取关键帧 = is成功读取(ref list关键帧, "关键帧", path输出目录关键帧时间戳, ref span);
            if (!b读取关键帧) {
                string path视频同目录关键帧时间戳 = $"{fi输入文件.DirectoryName}\\关键帧时间戳_{fi输入文件.Name}.info";
                b读取关键帧 = is成功读取(ref list关键帧, "关键帧", path视频同目录关键帧时间戳, ref span);
                if (!b读取关键帧) {
                    list关键帧 = new SynchronizedCollection<float>( ) { 0 };
                    th扫关键帧 = new Thread(fn扫关键帧) { IsBackground = true, Name = "扫关键帧" + fi输入文件.Name };
                    th扫关键帧.Start( );
                }
            }
        }

        public bool GetTxt(out string txt) {
            txt = "\r\n" + fi输入文件.FullName + "\r\n";
            if (!string.IsNullOrEmpty(str扫转场)) txt += "正在扫描转场时刻：" + str扫转场 + "\r\n";
            if (!string.IsNullOrEmpty(str扫黑场)) txt += "正在扫描黑场时刻：" + str扫黑场 + "\r\n";
            if (!string.IsNullOrEmpty(str扫白场)) txt += "正在扫描白场时刻：" + str扫白场 + "\r\n";
            if (!string.IsNullOrEmpty(str扫关键帧)) txt += "正在扫描关键帧：" + str扫关键帧 + "\r\n";
            if (list关键帧.Count > 0) txt += "关键帧：" + list关键帧.Last( ) + "秒 （" + list关键帧.Count + " 组）\r\n";
            if (is扫描完成) {
                txt += "扫描完成！\r\n";
                转码队列.Remove_VTimeBase(this);
            }
            return true;
        }

        public bool GetSpan偏移(int i分段号, out Span偏移 span偏移) {
            if (dic_分段_偏移.TryGetValue(i分段号, out span偏移))
                return true;
            else return false;
        }
        public bool is重算时间码(string path转码完成, string str编码摘要) {
            fx获取时长( );
            string timestamp_v2 = "# timestamp format v2";
            StringBuilder @string = new StringBuilder(timestamp_v2);
            var dicSort_Part = from objDic in dic_分段_偏移 orderby objDic.Key ascending select objDic;
            foreach (var item in dicSort_Part) {
                string path = $"{path转码完成}\\{item.Key}_timestamp.txt";
                string[] arr;
                try { arr = File.ReadAllLines(path); } catch { return false; }
                if (arr.Length > 1) {
                    for (int end = arr.Length - 1; end > 1; end--) {
                        if (float.TryParse(arr[end], out _)) {
                            float ms本场起始 = item.Value.f转场 * 1000;
                            for (int i = 1; i < end; i++) {//最后一帧是结束时间戳，不需要计算
                                @string.AppendLine( ).Append(ms本场起始 + float.Parse(arr[i]));
                            }
                            goto 下一个;
                        }
                    }
                }
                下一个:;
            }
            @string.AppendLine( ).Append(Duration * 1000);
            timestamp_v2 = @string.ToString( );
            DirectoryInfo di成功目录 = new DirectoryInfo(path转码完成);//合成函数会尝试合成切片目录下不同参数的文件
            try {
                File.WriteAllText($"{di成功目录.Parent.FullName}\\重算时间码_{str编码摘要}.txt", timestamp_v2);
                return true;
            } catch { }
            return false;
        }


        readonly object obj读取文件号 = new object( );
        public bool hasNext_序列Span偏移(DirectoryInfo di编码成功, out Span偏移 span偏移, out int i剩余, out bool b全黑场) {
            再次检查:
            while (set体积降序编码序列.Count > 0) {
                int n;
                lock (obj读取文件号) n = set体积降序编码序列.First( );
                if (File.Exists($"{di编码成功.FullName}\\{n}.mkv")) {//检查并绕过已有任务
                    lock (obj读取文件号) {
                        set体积降序编码序列.Remove(n);
                        set已编码.Add(n);
                    }
                } else { break; }
            }

            while (set体积降序编码序列.Count < 1 && b正在计算) {//如果没有等待转码，，并且扫描线程还在工作则无限等待
                try { reset再次获取.WaitOne(666); } catch { }
                event计算.Set( );
                if (set体积降序编码序列.Count > 0) goto 再次检查; //延迟后，新计算出的分段需再次检查文件已存在。
            }

            b全黑场 = false;
            if (set体积降序编码序列.Count > 0) {
                int n;
                lock (obj读取文件号) n = set体积降序编码序列.First( );
                if (dic_分段_偏移.TryGetValue(n, out span偏移)) {
                    lock (obj读取文件号) {
                        set体积降序编码序列.Remove(n);
                        set已编码.Add(n);
                    }
                    i剩余 = set体积降序编码序列.Count;
                    if (span偏移.f结束 > 0 && dic_连续黑场.TryGetValue(span偏移.f转场, out var map)) {
                        if (map.Count > 0 && map.Last( ) >= span偏移.f结束 || map.Contains(span偏移.f结束)) {
                            b全黑场 = true;
                        }
                    }
                    return true;
                }
            }

            i剩余 = 0;
            span偏移 = null;
            return false;
        }

        double getSpan最慢 {
            get {
                double min = double.MaxValue;
                double f转场 = span扫转场进度.TotalSeconds;
                double f黑场 = span扫黑场进度.TotalSeconds;
                double f白场 = span扫白场进度.TotalSeconds;

                if (min > f转场) min = f转场;
                if (min > f黑场) min = f黑场;
                if (min > f白场) min = f白场;

                if (list关键帧.Count > 1) {
                    float f关键帧 = list关键帧.Last( );
                    if (min > f关键帧) min = f关键帧;
                }

                return min;
            }
        }
        void fx匹配关键帧(ref int index关键帧, ref List<float> list分段秒) {
            for (int index分段 = dic_分段_偏移.Count; index分段 < list分段秒.Count - 1; index分段++) {//找最近的关键帧秒
                if (dic关键帧.ContainsKey(list分段秒[index分段])) {
                    Span偏移 span = new Span偏移(dic_分段_偏移.Count + 1, list分段秒[index分段], list分段秒[index分段], list分段秒[index分段 + 1], vinfo.f输入每帧秒);
                    dic_分段_偏移.TryAdd(span.i分段号, span);
                } else {
                    float sec关键帧 = list关键帧[index关键帧];
                    while (list关键帧.Last( ) < list分段秒[index分段]) try { event计算.WaitOne(666); } catch { }

                    while (index关键帧 < list关键帧.Count - 1) {
                        if (list关键帧[index关键帧] <= list分段秒[index分段] && list分段秒[index分段] <= list关键帧[index关键帧 + 1]) {
                            sec关键帧 = list关键帧[index关键帧];
                            Span偏移 span = new Span偏移(dic_分段_偏移.Count + 1, sec关键帧, list分段秒[index分段], list分段秒[index分段 + 1], vinfo.f输入每帧秒);
                            dic_分段_偏移.TryAdd(span.i分段号, span);
                            break;
                        } else {
                            if (list关键帧[index关键帧] > list分段秒[index分段]) {
                                if (index关键帧 > 1) index关键帧--;
                            } else if (list关键帧[index分段] < list分段秒[index分段 + 1]) {
                                if (index关键帧 < list关键帧.Count - 1) index关键帧++;
                            } else {
                                break;
                            }
                        }
                    }
                }
            }
        }

        void fx查找区间镜头切换(float secMin, float secMax, ref int index转场, ref List<float> list分段秒) {
            do {
                int start = index转场;
                for (; start < list转场.Count; start++) {
                    if (list转场[start] >= secMin) {
                        if (list转场[start] > secMax) {
                            return;
                        } else if (list转场[start] > list分段秒.Last( ) + sec_gop * 6) {
                            list分段秒.Add(list转场[start]);
                            secMin = list转场[start] + sec分割至少;
                            index转场 = start + 1;
                        } else
                            break;
                    }
                }
                if (start >= list转场.Count) {
                    index转场 = start + 1;
                    return;
                }
                if (list转场[start] - list分段秒.Last( ) >= sec分割至少 && list转场[start] <= secMax) {
                    int end = start + 1;
                    float sec_gop_end = list转场[start] + sec_gop * 6;
                    float sec止步 = sec_gop_end < secMax ? sec_gop_end : secMax;
                    for (; end < list转场.Count && list转场[end] < sec止步; end++) ;
                    if (end < list转场.Count) {
                        for (; end >= start && list转场[end] > sec止步; end--) ;//结束范围必须小于等于最大秒或者末尾。 
                        if (list转场[end] >= secMax) {
                            list分段秒.Add(list转场[start]);//时段内只有一个镜头切换
                            index转场 = start + 1;
                            return;
                        } else {
                            if (end > start) {
                                int i最大画面方差 = start;
                                double f最大画面方差 = dic转场帧[list转场[start]].d画面方差;
                                for (int i = start + 1; i <= end; i++) {
                                    double f画面方差 = dic转场帧[list转场[i]].d画面方差;
                                    if (f画面方差 >= f最大画面方差) {
                                        f最大画面方差 = f画面方差;
                                        i最大画面方差 = i;
                                    }
                                }
                                float f转场秒 = list转场[i最大画面方差];
                                index转场 = i最大画面方差 + 1;
                                secMin = f转场秒 + sec分割至少;

                                list分段秒.Add(f转场秒);

                            } else if (end == start) {
                                list分段秒.Add(list转场[start]);//时段内只有一个镜头切换
                                index转场 = start + 1;
                                return;
                            } else
                                return;
                        }

                    } else {
                        list分段秒.Add(list转场[start]);//时段内只有一个镜头切换

                        secMin = list转场[start] + sec分割至少;
                        index转场 = start + 1;
                        return;
                    }
                } else {
                    return;
                }
            } while (index转场 < list转场.Count && list转场[index转场] < secMax);
        }

        void fn循环计算关键帧并存盘( ) {
            int i = 1;
            float last = 0;
            do {
                if (th扫关键帧 != null && th扫关键帧.IsAlive) try { event计算.WaitOne(666); } catch { }
                int count = dic_分段_偏移.Count;
                for (; i < list关键帧.Count; i++) {
                    if (list关键帧[i] - last >= sec_gop) {
                        int n = dic_分段_偏移.Count + 1;
                        if (dic_分段_偏移.TryAdd(n, new Span偏移(n, last, last, list关键帧[i], vinfo.f输入每帧秒))) {
                            last = list关键帧[i];
                        }
                    }
                }
                if (dic_分段_偏移.Count - count > 0) {
                    if (转码队列.b允许入队) fx保存有序无缓转码csv(b完成: false);
                }
            } while (list关键帧.Last( ) < Duration);

            fx保存有序无缓转码csv(b完成: true);
        }

        void fn循环计算分段点并存盘( ) {
            DateTime timeStart = DateTime.Now.AddMinutes(-1);
            if (!is扫描完成) Thread.Sleep(6666);
            int index黑场 = 1, index转场 = 1, index关键帧 = 0;
            List<float> list分段秒 = new List<float>( ) { 0 };
            float f后6组 = 0;

            do {
                while (转码队列.b队列满 && set体积降序编码序列.Count > 0 && !is扫描完成)
                    try { event计算.WaitOne(666); } catch { }

                f后6组 += sec_gop * 6;//每轮
                float sec一打GOP = f后6组 + sec_gop * 6;
                if (f后6组 > Duration) {
                    sec一打GOP = f后6组 = Duration;
                }
                for (; getSpan最慢 <= sec一打GOP;) try { event计算.WaitOne(6666); } catch { }//缓冲跨步时长12组。

                //if ((index黑场 < list黑场.Count - 1 || (index黑场 < list黑场.Count && list黑场.Last( ) < Duration)) && list黑场[index黑场] <= f后6组) {
                if (index黑场 < list黑场.Count && list黑场[index黑场] <= f后6组) {
                    list分段秒.Add(list黑场[index黑场]);
                    for (++index黑场; index黑场 < list黑场.Count && list黑场[index黑场] < f后6组; index黑场++) {//步时长6图组内寻找下一黑场。
                        if (list黑场[index黑场] - list分段秒.Last( ) > sec_gop * 3) {//两个黑场之间超过3个图组尝试寻找插入转场。
                            fx查找区间镜头切换(list分段秒.Last( ) + sec分割至少, list黑场[index黑场] - sec分割至少, ref index转场, ref list分段秒);
                        }
                        list分段秒.Add(list黑场[index黑场]);
                    }
                } else if (index转场 < list转场.Count && list转场[index转场] <= f后6组) { //时段内无黑场，可能有转场
                    fx查找区间镜头切换(list分段秒.Last( ) + sec分割至少, f后6组, ref index转场, ref list分段秒);//每次后移6组
                }

                if (!is扫描完成 && DateTime.Now.Subtract(timeStart).TotalSeconds > 10) {
                    int i上次分段数 = dic_分段_偏移.Count;
                    fx匹配关键帧(ref index关键帧, ref list分段秒);
                    if (dic_分段_偏移.Count > i上次分段数 && getSpan最慢 < Duration) {
                        fx保存有序无缓转码csv(b完成: false);
                        timeStart = DateTime.Now;
                        Thread.Sleep(999);
                    }
                }
            } while (f后6组 < Duration);//分段结束
            list分段秒.Add(Duration加一帧);

            fx匹配关键帧(ref index关键帧, ref list分段秒);
            fx保存有序无缓转码csv(b完成: true);
        }
        void fx同步计算分段点并存盘( ) {
            int index黑场 = 1, index转场 = 1, index关键帧 = 0;
            List<float> list分段秒 = new List<float>( ) { 0 };
            float f后6组 = 0;
            do {
                f后6组 += sec_gop * 6;//每轮
                if (f后6组 > Duration) f后6组 = Duration;
                if (index黑场 < list黑场.Count && list黑场[index黑场] <= f后6组) {
                    list分段秒.Add(list黑场[index黑场]);
                    for (++index黑场; index黑场 < list黑场.Count && list黑场[index黑场] < f后6组; index黑场++) {//步时长6图组内寻找下一黑场。
                        if (list黑场[index黑场] - list分段秒.Last( ) > sec_gop * 3) {//两个黑场之间超过3个图组尝试寻找插入转场。
                            fx查找区间镜头切换(list分段秒.Last( ) + sec分割至少, list黑场[index黑场] - sec分割至少, ref index转场, ref list分段秒);
                        }
                        list分段秒.Add(list黑场[index黑场]);
                    }
                } else if (index转场 < list转场.Count && list转场[index转场] <= f后6组) { //时段内无黑场，可能有转场
                    fx查找区间镜头切换(list分段秒.Last( ) + sec分割至少, f后6组, ref index转场, ref list分段秒);//每次后移6组
                }
            } while (f后6组 < Duration);//分段结束

            list分段秒.Add(Duration加一帧);

            list分段秒.Distinct( );
            list分段秒.Sort( );
            fx匹配关键帧(ref index关键帧, ref list分段秒);
            fx保存有序无缓转码csv(b完成: true);
        }

        void fx获取时长( ) {
            using (Process p = new Process( )) {
                string cmd = "-v error -show_entries format=duration \"" + fi输入文件.FullName + '"';
                p.StartInfo = get_StartInfo(EXE.ffprobe, cmd);
                try { p.Start( ); } catch { return; }
                string Output = p.StandardOutput.ReadToEnd( );
                if (float.TryParse(regex秒长.Match(Output).Groups[1].Value, out float sec)) {
                    vinfo.time视频时长 = TimeSpan.FromSeconds(sec);
                    Duration = sec;
                    Duration加一帧 = sec + +vinfo.f输入每帧秒;
                }
            }
        }

        void fn扫关键帧( ) {
            using (Process process = new Process( )) {
                //string cmd= $"-select_streams v:0 -show_entries frame=pts_time,duration_time,pict_type -of csv \"{fi输入文件.FullName}\""; //单线程解码速度过慢，只取关键帧时间戳做偏移量。
                string cmd = $"-select_streams v:0 -skip_frame nokey -show_entries frame=pts_time,duration_time,pict_type -of csv \"{fi输入文件.FullName}\"";
                process.StartInfo = get_StartInfo(EXE.ffprobe, cmd);

                try {
                    process.Start( );
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                } catch { return; }

                StringBuilder @string = new StringBuilder( ), @stringErr = new StringBuilder(cmd);
                @stringErr.AppendLine( ).AppendLine( );
                Task.Run(( ) => @stringErr.AppendLine(process.StandardError.ReadToEnd( )));

                while (!process.StandardOutput.EndOfStream) {
                    str扫关键帧 = process.StandardOutput.ReadLine( ).Trim( );
                    if (is关键帧(str扫关键帧))
                        @string.AppendLine(str扫关键帧.TrimEnd(','));
                    else @stringErr.Append(str扫关键帧);
                }

                process.WaitForExit( );
                if (process.ExitCode == 0) {
                    @string.AppendLine("安全退出");
                    string txt = @stringErr.AppendLine( ).Append(@string).ToString( );

                    string path视频同目录关键帧时间戳 = fi输入文件.FullName + "_关键帧时间戳.info";
                    //try { File.WriteAllText(path视频同目录关键帧时间戳, txt); } catch { }//调试禁用
                    try { File.WriteAllText(path输出目录关键帧时间戳, txt); } catch { }
                }
                list关键帧.Add(Duration加一帧);//队列末用于计算完成时刻，同时判断扫描线程结束。
                if (is扫描完成) fx同步计算分段点并存盘( );//所有扫描线程完毕后，触发同步计算
            }
        }
        void fn扫转场( ) {
            span扫转场进度 = TimeSpan.Zero;
            using (Process process = new Process( )) {
                string cmd = $"{str单线程}-i \"{fi输入文件.FullName}\" -vf \"select='gt(scene,{scene})',showinfo\" -an -sn -f null -";
                process.StartInfo = get_StartInfo(EXE.ffmpeg, cmd);
                try { process.Start( ); } catch {
                    return;
                }

                StringBuilder @stringError = new StringBuilder(process.StartInfo.FileName);
                @stringError.Append(' ').AppendLine(cmd).AppendLine( );
                while (!process.StandardError.EndOfStream) {
                    string StandardError = process.StandardError.ReadLine( );
                    if (StandardError.StartsWith("Press [q] to stop")) break;
                    else @stringError.AppendLine(StandardError);
                }

                string path视频同目录时间戳 = fi输入文件.FullName + "_转场帧时间戳.info";
                if (!process.HasExited) {
                    str扫转场 = DateTime.Now + "……";
                    process.PriorityClass = ProcessPriorityClass.Idle;

                    Dictionary<string, int> dic错误计数 = new Dictionary<string, int>( ) { };
                    StringBuilder @string = new StringBuilder( );
                    while (!process.StandardError.EndOfStream) {
                        string StandardError = process.StandardError.ReadLine( );
                        if (StandardError.StartsWith("frame=")) {
                            Format_Time(StandardError, ref span扫转场进度);
                            str扫转场 = StandardError;
                        } else {
                            if (is转场(StandardError)) {
                                event计算.Set( );
                                @string.AppendLine(StandardError);
                            } else if (dic错误计数.ContainsKey(StandardError))
                                dic错误计数[StandardError]++;
                            else
                                dic错误计数.Add(StandardError, 1);
                        }
                    }
                    @stringError.AppendLine(str扫转场).AppendLine( );
                    fx保存信息(process, path视频同目录时间戳, path输出目录转场时间戳, @string, stringError, dic错误计数);
                } else {
                    fx保存信息(process, path视频同目录时间戳, path输出目录转场时间戳, null, @stringError, null);
                }

                span扫转场进度 = TimeSpan.FromSeconds(Duration加一帧);
                list转场.Add(Duration加一帧);//队列末尾也用于判断进程结束
                if (is扫描完成) fx同步计算分段点并存盘( );//所有扫描线程完毕后，触发同步计算
            }
        }
        void fn扫黑场( ) {
            span扫黑场进度 = TimeSpan.Zero;
            using (Process process = new Process( )) {
                string cmd = $"{str单线程}-i \"{fi输入文件.FullName}\" -vf \"blackframe\" -f null -";
                process.StartInfo = get_StartInfo(EXE.ffmpeg, cmd);
                try { process.Start( ); } catch {
                    return;
                }
                StringBuilder @stringError = new StringBuilder(process.StartInfo.FileName);
                @stringError.Append(' ').AppendLine(cmd).AppendLine( );
                while (!process.StandardError.EndOfStream) {
                    string StandardError = process.StandardError.ReadLine( );
                    if (StandardError.StartsWith("Press [q] to stop")) break;
                    else @stringError.AppendLine(StandardError);
                }

                string path视频同目录时间戳 = fi输入文件.FullName + "_黑场时间戳.info";
                if (!process.HasExited) {
                    str扫黑场 = DateTime.Now + "……";
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    Dictionary<string, int> dic错误计数 = new Dictionary<string, int>( ) { };
                    StringBuilder @string = new StringBuilder( );
                    while (!process.StandardError.EndOfStream) {
                        string StandardError = process.StandardError.ReadLine( );
                        if (StandardError.StartsWith("frame=")) {
                            Format_Time(StandardError, ref span扫黑场进度);
                            str扫黑场 = StandardError;
                        } else {
                            if (is黑场(StandardError))
                                @string.AppendLine(StandardError);
                            else if (dic错误计数.ContainsKey(StandardError))
                                dic错误计数[StandardError]++;
                            else
                                dic错误计数.Add(StandardError, 1);
                        }
                    }

                    @stringError.AppendLine(str扫黑场).AppendLine( );
                    fx保存信息(process, path视频同目录时间戳, path输出目录黑场时间戳, @string, @stringError, dic错误计数);
                } else {
                    fx保存信息(process, path视频同目录时间戳, path输出目录黑场时间戳, null, @stringError, null);
                }

                span扫黑场进度 = TimeSpan.FromSeconds(Duration加一帧);
                list黑场.Add(Duration加一帧);//队列末尾也用于判断进程结束
                if (is扫描完成) fx同步计算分段点并存盘( );//所有扫描线程完毕后，触发同步计算
            }
        }
        void fn扫白场( ) {
            span扫白场进度 = TimeSpan.Zero;
            using (Process process = new Process( )) {
                string cmd = $"{str单线程} -i \"{fi输入文件.FullName}\" -vf \"signalstats,metadata=print:key=lavfi.signalstats.YAVG,duration_time\" - f null -";
                process.StartInfo = get_StartInfo(EXE.ffmpeg, cmd);
                try { process.Start( ); } catch {
                    return;
                }
                StringBuilder @stringError = new StringBuilder(process.StartInfo.FileName);
                @stringError.Append(' ').AppendLine(cmd).AppendLine( );
                while (!process.StandardError.EndOfStream) {
                    string StandardError = process.StandardError.ReadLine( );
                    if (StandardError.StartsWith("Press [q] to stop")) break;
                    else @stringError.AppendLine(StandardError);
                }

                string path视频同目录时间戳 = fi输入文件.FullName + "_白场时间戳.info";
                if (!process.HasExited) {
                    str扫白场 = DateTime.Now + "……";
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    StringBuilder @string = new StringBuilder( );
                    Dictionary<string, int> dic错误计数 = new Dictionary<string, int>( ) { };
                    while (!process.StandardError.EndOfStream) {
                        string StandardError = process.StandardError.ReadLine( );
                        if (StandardError.StartsWith("frame=")) {
                            Format_Time(StandardError, ref span扫白场进度);
                            str扫白场 = StandardError;
                        } else {
                            if (is白场(StandardError))
                                @string.AppendLine(StandardError);
                            else if (dic错误计数.ContainsKey(StandardError))
                                dic错误计数[StandardError]++;
                            else
                                dic错误计数.Add(StandardError, 1);
                        }
                    }
                    @stringError.AppendLine(str扫白场).AppendLine( );
                    fx保存信息(process, path视频同目录时间戳, path输出目录白场时间戳, @string, stringError, dic错误计数);
                } else
                    fx保存信息(process, path视频同目录时间戳, path输出目录白场时间戳, null, stringError, null);

                span扫白场进度 = TimeSpan.FromSeconds(Duration加一帧);
                list白场.Add(Duration加一帧);//队列末尾也用于判断进程结束
                if (is扫描完成) fx同步计算分段点并存盘( );//所有扫描线程完毕后，触发同步计算
            }
        }
        ProcessStartInfo get_StartInfo(string exe, string cmd) {
            string full_cmd = str单线程 + cmd + EXE.ffmpeg不显库;
            ProcessStartInfo StartInfo = new ProcessStartInfo(exe, full_cmd);
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.RedirectStandardError = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.StandardErrorEncoding = Encoding.UTF8;
            StartInfo.StandardOutputEncoding = Encoding.UTF8;
            return StartInfo;
        }

        bool is成功读取(ref SynchronizedCollection<float> list, string type, string path, ref TimeSpan span) {
            if (File.Exists(path)) {
                string[] lines;
                try { lines = File.ReadAllLines(path); } catch { lines = null; }
                if (lines != null && lines.Length > 3 && lines.Last( ).TrimEnd( ).EndsWith("安全退出")) {
                    if (type == "转场") {
                        for (int i = 0; i < lines.Length; i++) is转场(lines[i]);
                    } else if (type == "黑场") {
                        for (int i = 0; i < lines.Length; i++) is黑场(lines[i]);
                    } else if (type == "白场") {
                        for (int i = 0; i < lines.Length; i++) is白场(lines[i]);
                    } else if (type == "关键帧") {
                        for (int i = 0; i < lines.Length; i++) is关键帧(lines[i]);
                    }

                    if (list.Count > 0) {
                        span = TimeSpan.FromSeconds(Duration加一帧);
                        list.Add(Duration加一帧);//队列末尾加入片尾+1帧空时间戳，与下一帧帧对比无需判断下标溢出。
                        if (is扫描完成)
                            event计算.Set( );
                        return true;
                    }
                }
            }
            return false;
        }

        bool is关键帧(string StandardOutput) {
            if (StandardOutput.StartsWith("frame,")) { //frame,0.559667,0.033367,I
                string[] arr = StandardOutput.Split(',');
                if (arr.Length > 2) {
                    if (float.TryParse(arr[1], out float sec)) {
                        list所有帧.Add(sec);
                        if (StandardOutput.Contains(",I")) {
                            list关键帧.Add(sec);
                            dic关键帧.TryAdd(sec, StandardOutput);
                        } else
                            dic帧类型.TryAdd(sec, StandardOutput);
                    }
                }
                return true;
            }
            return false;
        }

        Scene.Info last_sinfo = new Scene.Info( );
        bool is转场(string StandardError) {
            if (StandardError.StartsWith("[Parsed_showinfo")) {
                int index_pts = StandardError.IndexOf("pts_time:", 16) + 9;
                if (index_pts >= 25) {
                    int end_pts = StandardError.IndexOf(" ", index_pts);
                    if (end_pts >= 0) {
                        if (float.TryParse(StandardError.Substring(index_pts, end_pts - index_pts), out float sec)) {
                            list转场.Add(sec);
                            if (StandardError.Contains("type:I")) {
                                dic关键帧.TryAdd(sec, StandardError);
                            }
                            Scene.Info info = new Scene.Info(StandardError);
                            info.fx画面方差(last_sinfo);
                            dic转场帧.TryAdd(sec, info);
                            last_sinfo = info;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        float sec上次黑场 = 0, sec连续起始 = 0;
        int blackframe = 0;
        bool is黑场(string StandardError) {
            if (StandardError.StartsWith("[Parsed_blackframe")) {
                bool b连续场 = false;
                int i_frame = StandardError.IndexOf("frame:", 18) + 6;
                if (i_frame > 24) {
                    int end_frame = StandardError.IndexOf(' ', i_frame);
                    if (end_frame > 0) {
                        if (int.TryParse(StandardError.Substring(i_frame, end_frame - i_frame), out int frame)) {
                            b连续场 = frame == blackframe + 1; //过滤掉连续黑场
                            blackframe = frame;
                        }
                        int index_pts = StandardError.IndexOf("t:", end_frame) + 2;
                        if (index_pts > end_frame + 2) {
                            int end_pts = StandardError.IndexOf(' ', index_pts);
                            if (end_pts >= 0) {
                                if (float.TryParse(StandardError.Substring(index_pts, end_pts - index_pts), out float sec)) {
                                    if (b连续场) {
                                        if (dic_连续黑场.TryGetValue(sec连续起始, out var map)) {
                                            map.Add(sec);
                                        } else {
                                            dic_连续黑场.Add(sec连续起始, new HashSet<float>( ) { sec });
                                        }
                                    } else {
                                        sec连续起始 = sec;
                                        dic_连续黑场.Add(sec, new HashSet<float>( ));

                                        if (sec - sec上次黑场 > f连续黑场最小秒) {
                                            if (sec上次黑场 - list黑场.Last( ) > f连续黑场最小秒) {
                                                list黑场.Add(sec上次黑场);//连续黑场的结尾帧
                                            }
                                            list黑场.Add(sec);//过滤掉不足分割最小秒黑场
                                        }
                                    }

                                    sec上次黑场 = sec;
                                    dic黑场.TryAdd(sec, StandardError);
                                    return true;
                                }
                            }
                        }

                    }
                }
            }
            return false;
        }

        bool is白场(string StandardError) {
            if (StandardError.StartsWith("[Parsed_showinfo")) {
                int index_pts = StandardError.IndexOf("pts_time:", 16) + 9;
                if (index_pts >= 25) {
                    int end_pts = StandardError.IndexOf(" ", index_pts);
                    if (end_pts >= 0) {
                        if (float.TryParse(StandardError.Substring(index_pts, end_pts - index_pts), out float sec)) {
                            list白场.Add(sec);
                            dic白场.TryAdd(sec, StandardError);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        void Format_Time(string StandarError, ref TimeSpan span) {
            int index_time = StandarError.IndexOf("time=") + 5;
            if (index_time >= 5) {
                int end_time = StandarError.IndexOf(' ', index_time);
                if (end_time >= 0) {
                    if (TimeSpan.TryParse(StandarError.Substring(index_time, end_time - index_time), out TimeSpan timeSpan)) {
                        span = timeSpan;
                    }
                }
            }
        }

        void fx保存信息(Process process, string path视频同目录时间戳, string path输出目录时间戳, StringBuilder @string, StringBuilder @stringError, Dictionary<string, int> dic错误计数) {
            process.WaitForExit( );

            if (dic错误计数 != null) {
                foreach (var kv in dic错误计数) {
                    if (kv.Value < 3) @stringError.AppendLine(kv.Key);
                    else @stringError.Append(kv.Key).Append(" × ").Append(kv.Value).AppendLine( );
                }
            }

            if (process.ExitCode == 0) {
                if (@string != null) @stringError.AppendLine( ).Append(@string).Append(DateTime.Now).AppendLine("安全退出");
            } else {
                @stringError.AppendLine( ).Append(DateTime.Now).Append("异常退出");
            }

            string txt = @stringError.ToString( );

            //try { File.WriteAllText(path视频同目录时间戳, txt); } catch { }
            try { File.WriteAllText(path输出目录时间戳, txt); } catch { }
        }

        void fx保存有序无缓转码csv(bool b完成) {
            ICollection<Span偏移> arr_span = dic_分段_偏移.Values;
            foreach (Span偏移 span in arr_span) {
                if (span.f持续秒 < f连续黑场最小秒) {
                    dic_分段_偏移.TryRemove(span.i分段号, out _);
                }
            }
            var dicSort_Sec = from objDic in dic_分段_偏移 orderby objDic.Value.f持续秒 descending select objDic;

            lock (obj读取文件号) {
                set体积降序编码序列.Clear( );
                foreach (var item in dicSort_Sec)
                    if (!set已编码.Contains(item.Key))
                        set体积降序编码序列.Add(item.Key);
            }
            var dicSort_Part = from objDic in dic_分段_偏移 orderby objDic.Key ascending select objDic;


            StringBuilder @string = new StringBuilder("序号,转场秒,结束秒,最近关键帧秒,f指定画质CRF,片段持续秒,分割时刻");
            foreach (var item in dicSort_Part) @string.AppendFormat("\r\n{0},{1},{2},{3},-1,{4},{5}时{6}分{7}.{8}秒"
                , item.Key, item.Value.f转场, item.Value.f结束, item.Value.f关键帧, item.Value.f持续秒
                , item.Value.ts分割时刻.Hours, item.Value.ts分割时刻.Minutes, item.Value.ts分割时刻.Seconds, item.Value.ts分割时刻.Milliseconds);

            if (b完成) {
                int end = dic_分段_偏移.Keys.OrderByDescending(x => x).First( );
                Span偏移 spanLast = dic_分段_偏移[end];
                if (spanLast.f结束 >= Duration) spanLast.f偏移结束 = spanLast.f结束 = 0;

                @string.AppendLine( ).Append("完成");
                list关键帧.Clear( );
                list所有帧.Clear( );
                list转场.Clear( );
                list黑场.Clear( );
                list白场.Clear( );

                dic帧类型.Clear( );
                dic黑场.Clear( );
                dic白场.Clear( );
                dic转场帧.Clear( );
                b正在计算 = false;
                转码队列.Remove_VTimeBase(this);
                Form破片压缩.autoReset转码.Set( );
            }

            try { File.WriteAllText(di输出目录.FullName + "\\无缓转码.csv", @string.ToString( )); } catch (Exception err) {
                @string.AppendLine( ).AppendLine(err.Message);
                try { File.WriteAllText(di输出目录.FullName + "\\无缓转码.csv" + DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss.fff") + ".csv", @string.ToString( )); } catch { }
            }
        }

    }
}
