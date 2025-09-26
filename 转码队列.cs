using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace 破片压缩器 {
    internal static class 转码队列 {
        public static Stopwatch stopwatch = Stopwatch.StartNew( );

        public static int i切片缓存 = 0;

        public static int i物理核心数 = 8;
        public static int i逻辑核心数 = 16;

        public static int[] arr_核心号调度排序 = null;
        public static IntPtr[] arr_单核指针 = null;

        public static int i多进程数量 = 3;

        static readonly object obj增删排锁 = new object( );
        static int padLen = 0;
        public static AutoResetEvent autoReset入队 = new AutoResetEvent(false);

        public static string strPast = string.Empty;
        public static double totalFileKbit = 0, totalVideoSeconds = 0.01f;
        public static ulong ul累计完成帧 = 0;

        public static Dictionary<string, int> dic_切片路径_剩余 = new Dictionary<string, int>( );

        public static bool b有任务 => list.Count > 0;
        public static int i并发任务数 => list.Count;
        public static int i可入队数 => i多进程数量 - list.Count;

        static List<External_Process> list = new List<External_Process>( );

        public static External_Process process黑边 = null, process场景 = null, process切片 = null, process音轨 = null;//暂时设计为单任务。

        public static SynchronizedCollection<VTimeBase> list扫分段 = new SynchronizedCollection<VTimeBase>( );

        public static bool b允许入队 => list.Count < i多进程数量;
        public static bool b队列满 => i多进程数量 > 0 && list.Count >= i多进程数量;

        public static bool b缓存余量充足 {
            get {
                int sum = 0;
                foreach (int n in dic_切片路径_剩余.Values) {
                    sum += n;
                }

                return (sum > i多进程数量 * 3);
            }
        }

        public static bool ffmpeg等待入队(External_Process p) {
            //while (list.Count >= i多进程数量) autoReset入队.WaitOne( );需外部实现指定0任务，等待入队功能。
            if (p.async_FFmpeg编码( )) {
                lock (obj增删排锁) {
                    list.Add(p);
                    list = list.OrderBy(ep => ep.pid).ToList( );
                    padLen = 0;
                    for (int i = 0, a = 0; i < list.Count; i++) {
                        if (padLen < list[i].fi编码.Name.Length)
                            padLen = list[i].fi编码.Name.Length;

                        if (arr_核心号调度排序 != null && list[i].b单线程 && list.Count >= i多进程数量) {//达到指定任务数量开始分配内核
                            list[i].fx绑定编码进程到CPU单核心(arr_核心号调度排序[a]);//每增加一个，重排核心调度。
                            if (++a >= arr_核心号调度排序.Length) a = 0;
                        }
                    }
                }
            }
            while (list.Count >= i多进程数量) autoReset入队.WaitOne( );//先入队，再等待
            return true;
        }

        public static bool process移除结束(External_Process p) {
            int i进程数 = list.Count;

            bool success = false;
            lock (obj增删排锁) {
                success = list.Remove(p);//移除成功，队列计数会比进程数小1.
            }
            if (success) {//移除队列可以 在编码数据流停止&进程未退出 触发。b安全退出未标记就触发统计
                uint encodingFrames = 0;
                lock (obj增删排锁) {
                    for (int i = 0; i < list.Count; i++) {
                        if (list[i].HasFrame(out uint f)) {//进程退出时触发，p已从list移除，无需判断
                            encodingFrames += f;//正在编码的帧量求和，计算更准确的平均速度。
                        }
                    }
                }
                if (p.b安全退出) {
                    if (p.HasFrame(out uint f)) {
                        ul累计完成帧 += f;
                        totalFileKbit += p.fi编码.Length / 1024.0f * 8;//fi编码.Length=文件体积byte，×8换算为bit；
                        totalVideoSeconds += p.span输入时长.TotalSeconds;

                        double avg_vfr2tbr = ul累计完成帧 / totalVideoSeconds;// tbr (Time Base Rate)
                        double avgKbps = totalFileKbit / totalVideoSeconds;
                        double avgFps = (ul累计完成帧 + encodingFrames) * 1000.0f / stopwatch.ElapsedMilliseconds;

                        strPast = $"  已压[{ul累计完成帧}帧÷{totalVideoSeconds:F0}秒={avg_vfr2tbr:F2}(tbr)]  平均编码效率[{avgFps:F5}fps @ {avgKbps:F0}Kbps]";
                    }
                }
            }

            bool b等待入队 = autoReset入队.Set( );//移除、增加转码队列，在文件处理逻辑之前，合并信号由文件处理线程发起。

            Thread.Sleep(3333);//挂起的3秒钟内，有可能成功入队，队列计数有增长 。

            if (i进程数 > list.Count) {//收尾工作队列，程序主动管理内核分配。
                padLen = 0;
                lock (obj增删排锁) {
                    for (int i = 0, a = 0; i < list.Count; i++) {
                        if (padLen < list[i].fi编码.Name.Length)
                            padLen = list[i].fi编码.Name.Length;

                        if (arr_核心号调度排序 != null && list[i].b单线程) {
                            list[i].fx绑定编码进程到CPU单核心(arr_核心号调度排序[a]);//每增加一个，重排核心调度。
                            if (++a > arr_核心号调度排序.Length) a = 0;
                        }
                    }
                }
            }
            return success;
        }

        public static bool Has汇总输出信息(out string str编码信息) {
            str编码信息 = string.Empty;
            if (list.Count < 1) {
                if (process音轨 != null) {
                    str编码信息 = process音轨.sb输出数据流.ToString( ) + "\r\n"
                        + process音轨.get_ffmpeg_Pace;
                    return true;
                } else if (!string.IsNullOrEmpty(strPast)) {
                    str编码信息 = strPast;
                    return true;
                } else
                    return false;
            }

            double encFps = 0, encBitrate = 0;
            TimeSpan encSpan = TimeSpan.Zero;
            uint sum_encFrames = 0;

            StringBuilder sb = new StringBuilder( );
            Dictionary<string, List<string>> pairs = new Dictionary<string, List<string>>( );
            lock (obj增删排锁) {
                for (int i = 0; i < list.Count; i++) {
                    encFps += list[i].getFPS( );//求帧率函数会计算编码帧量。
                    sum_encFrames += list[i].encodingFrames;
                    string info = list[i].get_ffmpeg_Pace;
                    list[i].CountSpan_BitRate(ref encSpan, ref encBitrate);
                    if (!string.IsNullOrWhiteSpace(info)) {
                        string txt = $"{list[i].fi编码.Name.PadLeft(padLen)} \t {info}";
                        string title = list[i].di输出文件夹.FullName;
                        if (pairs.TryGetValue(title, out var li)) {
                            li.Add(txt);
                        } else {
                            pairs.Add(title, new List<string>( ) { txt });
                        }
                    }
                }
            }

            if (sum_encFrames > 0)
                sb.Append($"∑frame=").Append(sum_encFrames).Append(" ");

            if (encFps > 0) {
                if (encFps > 1) {
                    sb.Append($"∑fps={encFps:F3}");
                } else if (encFps > 1.0f / 60) {
                    sb.Append($"∑fpm={encFps * 60:F3}");
                } else if (encFps > 1.0f / 3600) {
                    sb.Append($"∑fpH={encFps * 3600:F3}");
                } else {
                    sb.Append($"∑fpDay={encFps * 86400:F3}");
                }
                if (encSpan > TimeSpan.Zero && encBitrate > 0) {
                    double avgKbps = encBitrate / encSpan.TotalSeconds;
                    if (avgKbps > 10) {
                        if (avgKbps < 10000) {
                            sb.AppendFormat(" X̄ Kbps={0:F0}", avgKbps);
                        } else {
                            sb.AppendFormat(" X̄ Mbps={0:F2}", avgKbps / 1024);
                        }
                    }
                }
                sb.Append(" @").Append(list.Count).Append("process");
                sb.Append(strPast).AppendLine( ).AppendLine( );
            } else if (strPast.Length > 0) {
                sb.Append(strPast).AppendLine( ).AppendLine( );
            }

            foreach (var kv in pairs) {
                if (dic_切片路径_剩余.TryGetValue(kv.Key, out var num)) {
                    sb.AppendFormat("（{0}/{1}） ", kv.Value.Count, kv.Value.Count + num);
                }
                sb.AppendLine(kv.Key);
                kv.Value.Sort( );
                foreach (var l in kv.Value) {
                    sb.AppendLine(l);
                }
                sb.AppendLine( );
            }
            if (sb.Length > 0) {
                str编码信息 = sb.ToString( );
                return true;
            }
            return false;
        }

        static readonly object obj扫描队列 = new object( );
        public static void Add_VTimeBase(VTimeBase vTime) {
            lock (obj扫描队列) list扫分段.Add(vTime);
        }
        public static void Remove_VTimeBase(VTimeBase vTime) {
            lock (obj扫描队列) list扫分段.Remove(vTime);
        }

        public static bool Get独立进程输出(out string info) {
            info = string.Empty;

            if (list扫分段.Count > 0) {
                for (int i = 0; i < list扫分段.Count; i++) {
                    if (list扫分段[i].GetTxt(out string txt)) {
                        info += txt;
                    }
                }
                return true;
            }

            if (process黑边 != null) {
                try {
                    info = process黑边.sb输出数据流.ToString( ) + "\r\n"
                      + process黑边.get_ffmpeg_Pace;
                } catch {
                    info = process黑边.get_ffmpeg_Pace;
                }
                return true;
            }
            if (process场景 != null) {
                try {
                    info = process场景.sb输出数据流.ToString( ) + "\r\n"
                     + process场景.get_ffmpeg_Pace;
                } catch {
                    info = process场景.get_ffmpeg_Pace;
                }
                return true;
            }
            if (process切片 != null) {
                info = process切片.sb输出数据流.ToString( );
                return true;
            }
            return false;
        }
    }
}
