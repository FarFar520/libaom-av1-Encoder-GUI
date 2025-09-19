using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace 破片压缩器 {
    public class Scene {
        public static int i分割最少秒 = 2;
        public List<Info> list_All = new List<Info>( );
        public List<Info> list_TypeI = new List<Info>( );

        public TimeSpan Duration = TimeSpan.Zero;
        public double f平均秒 = 5;//程序默认5秒一个GOP

        public void Add_TypeI(Info info) {
            if (info.pts_time > 0) {//有时间就行
                if (info.fx场景有差异(list_TypeI)) {
                    list_TypeI.Add(info);
                }
            }
        }

        public class Info {
            public int hasNum = 0;
            public int n = -1, img = 0;
            public double pts_time = 0;

            public string type = string.Empty;

            public Some mean = new Some( ), stdev = new Some( );

            public double sec前场时长 = 0, sec本场时长 = 0;

            public double dif前场mean = 0, dif前场stdev = 0, dif后场mean = 0, dif后场stdev = 0;

            public double d时间x空间 = 0;

            public Info info前场;

            public double d画面方差 = 0;

            //所有函数都初始化，避免意外调用。

            public double fx画面方差(Info info) {
                double dif_Mean = 1, dif_Stdev = 1;

                if (mean.has && info.mean.has)
                    dif_Mean = Math.Pow(info.mean.R - mean.R, 2) + Math.Pow(info.mean.G - mean.G, 2) + Math.Pow(info.mean.B - mean.B, 2);

                if (stdev.has && info.stdev.has)
                    dif_Stdev = Math.Pow(info.stdev.R - stdev.R, 2) + Math.Pow(info.stdev.G - stdev.G, 2) + Math.Pow(info.stdev.B - stdev.B, 2);

                d画面方差 = dif_Mean * dif_Mean;//有任意项为0风险。
                return d画面方差;
            }

            public double dif时空(Info info, double d段落) {
                double dif_Mean = 1, dif_Stdev = 1;

                if (mean.has && info.mean.has)
                    dif_Mean = Math.Pow(info.mean.R - mean.R, 2) + Math.Pow(info.mean.G - mean.G, 2) + Math.Pow(info.mean.B - mean.B, 2);

                if (stdev.has && info.stdev.has)
                    dif_Stdev = Math.Pow(info.stdev.R - stdev.R, 2) + Math.Pow(info.stdev.G - stdev.G, 2) + Math.Pow(info.stdev.B - stdev.B, 2);

                double sec持续 = Math.Abs(info.pts_time - pts_time);

                return sec持续 / d段落 * dif_Mean * dif_Mean;//有任意项为0风险。
            }

            public bool fx场景有差异(List<Info> infos) {
                if (infos.Count > 0) {
                    for (int i = infos.Count - 1; i >= 0; i--) {//关键帧差异。
                        if (infos[i].pts_time < pts_time) {//默认是排序好的数据。
                            info前场 = infos[i];
                            if (mean.has && infos[i].mean.has) {
                                dif前场mean = Math.Pow(infos[i].mean.R - mean.R, 2) + Math.Pow(infos[i].mean.G - mean.G, 2) + Math.Pow(infos[i].mean.B - mean.B, 2) + 1;
                                info前场.dif后场mean = dif前场mean;
                            }

                            if (stdev.has && infos[i].stdev.has) {
                                dif前场stdev = Math.Pow(infos[i].stdev.R - stdev.R, 2) + Math.Pow(infos[i].stdev.G - stdev.G, 2) + Math.Pow(infos[i].stdev.B - stdev.B, 2) + 1;
                                info前场.dif后场stdev = dif前场stdev;
                            }
                            info前场.sec本场时长 = sec前场时长 = pts_time - infos[i].pts_time;

                            if (dif前场mean < 4 && dif前场stdev < 4.9 && sec前场时长 < i分割最少秒) return false;//画面非常接近
                            if ((dif前场mean < 34 && dif前场stdev < 12) && sec前场时长 < 1) return false;//变动不大，时长接近

                            //if (sec前场时长 <= 0.5 && (dif前场mean < 202 || dif前场stdev < 118)) return false;//时长短，变化不大。

                            //抽样凡人修仙传第138话.Trim scene 0.061，解析得999场。mean方差高重复分布1~2、stdev高重复分布 0.1~0.5

                            break;
                        } else if (infos[i].pts_time == pts_time) {
                            return false;//过滤时间戳相同
                        }
                    }
                }

                return true;
            }

            public void fx场景差异(List<Info> infos) {
                if (infos.Count > 0) {
                    for (int i = infos.Count - 1; i >= 0; i--) {//关键帧差异。
                        if (infos[i].pts_time < pts_time) {//默认是排序好的数据。
                            info前场 = infos[i];
                            if (mean.has && infos[i].mean.has) {
                                dif前场mean = Math.Pow(infos[i].mean.R - mean.R, 2) + Math.Pow(infos[i].mean.G - mean.G, 2) + Math.Pow(infos[i].mean.B - mean.B, 2);
                                info前场.dif后场mean = dif前场mean;
                            }

                            if (stdev.has && infos[i].stdev.has) {
                                dif前场stdev = Math.Pow(infos[i].stdev.R - stdev.R, 2) + Math.Pow(infos[i].stdev.G - stdev.G, 2) + Math.Pow(infos[i].stdev.B - stdev.B, 2);
                                info前场.dif后场stdev = dif前场stdev;
                            }
                            info前场.sec本场时长 = sec前场时长 = pts_time - infos[i].pts_time;
                            break;
                        }
                    }
                }
            }

            public class Some {
                public static Regex regexNum = new Regex(@"\d+(\.\d+)?", RegexOptions.Compiled);
                public float R, G, B;
                public bool has = false;
                public Some(string set) {
                    MatchCollection matchCollection = regexNum.Matches(set);
                    if (matchCollection.Count == 3) {
                        R = float.Parse(matchCollection[0].Value);
                        G = float.Parse(matchCollection[1].Value);
                        B = float.Parse(matchCollection[2].Value);
                        has = true;
                    }
                }
                public Some( ) {
                    R = 0; G = 0; B = 0;
                }
            }

            public Info( ) {
            }

            public Info(string line) {
                //n:   1 pts:   1197 pts_time:1.197   duration:     40 duration_time:0.04    fmt:yuv420p cl:left sar:1/1 s:3840x2160 i:P iskey:1 type:I checksum:7BF0660B plane_checksum:[1C2C9608 E3667567 1E3E5A8D] mean:[68 132 128] stdev:[31.6 5.9 8.2]
                //[Parsed_showinfo_1 @ 000001f8d8c84e40] n:   1 pts:   1197 pts_time:1.197   duration:     40 duration_time:0.04    fmt:yuv420p cl:left sar:1/1 s:3840x2160 i:P iskey:1 type:I checksum:7BF0660B plane_checksum:[1C2C9608 E3667567 1E3E5A8D] mean:[68 132 128] stdev:[31.6 5.9 8.2]

                for (int i = line.Length - 1; i > 0; i--) {
                    StringBuilder sbValue = new StringBuilder(i, i);
                    for (; line[i] != ':' && i > 0; i--) sbValue.Insert(0, line[i]); // n:0  找到冒号

                    if (i - 1 >= 0) {//冒号前面需要有关键字
                        StringBuilder sbKey = new StringBuilder(i, i);
                        for (--i; i >= 0 && line[i] != ' '; i--) sbKey.Insert(0, line[i]);//冒号前面没有空格的规律，匹配非空格。
                        switch (sbKey.ToString( ).Trim( )) {
                            case "n": {
                                if (int.TryParse(sbValue.ToString( ), out n)) {
                                    img = n + 1;
                                    hasNum++;
                                }
                                break;
                            }
                            case "pts_time": {
                                if (double.TryParse(sbValue.ToString( ), out pts_time)) {
                                    hasNum++;
                                }
                                break;
                            }
                            case "type": {
                                type = sbValue.ToString( ).Trim( );
                                hasNum++;
                                break;
                            }
                            case "mean": {
                                mean = new Some(sbValue.ToString( ));
                                hasNum++;
                                break;
                            }
                            case "stdev": {
                                stdev = new Some(sbValue.ToString( ));
                                hasNum++;
                                break;
                            }
                            //case "": { break; }
                        }
                    } else
                        break;
                }


            }

            public Info(int n, double pts_time) {
                this.n = n;
                this.img = n + 1;
                this.pts_time = pts_time;
                type = "I";
                mean = new Some( );
                stdev = new Some( );
            }
        }
        Regex regex时长 = new Regex(@"Duration: (\d{2}:\d{2}:\d{2}\.\d+)");
        void MatchDuration(string[] lines, ref List<Info> infos) {
            for (int i = 0; i < lines.Length; i++) {
                if (regex时长.IsMatch(lines[i])) {
                    Duration = TimeSpan.Parse(regex时长.Match(lines[i]).Groups[1].Value);
                    if (infos.Count > 0) {
                        Info info = infos[infos.Count - 1];
                        info.sec本场时长 = Duration.TotalSeconds - info.pts_time;
                        f平均秒 = Duration.TotalSeconds / (infos.Count + 1);
                    }

                    break;
                }
            }

            infos = infos.OrderBy(s => s.n).ToList( );

            infos[0].d时间x空间 = infos[0].sec本场时长 / f平均秒 * infos[0].dif后场mean;

            infos[0].d画面方差 = Math.Pow(infos[0].mean.R, 2) + Math.Pow(infos[0].mean.G, 2) + Math.Pow(infos[0].mean.B, 2) * Math.Pow(infos[0].stdev.R, 2) + Math.Pow(infos[0].stdev.G, 2) + Math.Pow(infos[0].stdev.B, 2);

            for (int i = 1; i < infos.Count; i++) {
                infos[i].fx画面方差(infos[i - 1]);
                infos[i].d时间x空间 = infos[i].sec本场时长 / f平均秒 * infos[i].dif后场mean;
            }

            //foreach (Info info in infos) {
            //    info.d时间x空间 = info.sec本场时长 / f平均秒 * info.dif后场mean;//还行

            //    //info.d时间x空间 = info.sec本场时长 / f平均秒 * info.dif后场mean * info.dif后场stdev * info.sec前场时长 / f平均秒 * info.dif前场mean * info.dif后场stdev;

            //    //info.d时间x空间 = info.sec本场时长 / f平均秒 * info.dif后场mean + info.sec前场时长 / f平均秒 * info.dif前场mean;
            //    //前后场景，[持续时长×场景变化]做镜头切换排序一局

            //    //info.d时间x空间 = Math.Pow(info.sec本场时长 / f平均秒, 2) * info.dif后场mean;
            //}
        }

        public void Add_TypeI(string[] lines) {
            for (int i = 0; i < lines.Length; i++) {
                if (lines[i].Contains("type:I"))
                    Add_TypeI(new Info(lines[i]));//需调用一个场景差异函数。。
            }
            MatchDuration(lines, ref list_TypeI);
        }

        public List<float> Get_List_TypeI_pts_time( ) {//逼近平均算法。以时长、色差靠近平均值，做切片取舍。
            if (list_TypeI.Count < 64 || list_TypeI.Count < 转码队列.i物理核心数 * 2) {//分镜较少时，返回全部切割时间戳。
                List<float> list = new List<float>( );
                foreach (var item in list_TypeI) {
                    list.Add((float)item.pts_time);
                }
                return list;
            }

            List<Info> infos = list_TypeI.OrderByDescending(a => a.d画面方差).ToList( );

            int count;
            double d段落;
            if (f平均秒 < i分割最少秒) {
                d段落 = i分割最少秒;
                count = (int)(f平均秒 / i分割最少秒 * infos.Count);
                if (count * 2 < infos.Count) count = infos.Count / 2 + 1;//至少保留一半
            } else {
                d段落 = f平均秒;
                if (infos.Count > 1024) count = infos.Count / 2;
                else count = infos.Count;
            }

            List<Info> keep = new List<Info>( );
            for (int i = 0; i < count; i++) keep.Add(infos[i]);

            Info[] arr = keep.OrderBy(a => a.n).ToArray( );

            List<float> list_typeI_pts_time = new List<float>( );

            if (arr.Length > 2) {
                int i = 0;
                for (; i < arr.Length; i++)
                    if (arr[i].pts_time > d段落) {
                        list_typeI_pts_time.Add((float)arr[0].pts_time);
                        i++;
                        break;
                    }

                for (; i < arr.Length; i++)
                    if (arr[i].pts_time - list_typeI_pts_time[list_typeI_pts_time.Count - 1] > d段落)
                        list_typeI_pts_time.Add((float)arr[i].pts_time);
            }
            return list_typeI_pts_time;
        }


    }

}
